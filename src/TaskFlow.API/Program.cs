using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using TaskFlow.API;
using TaskFlow.API.Hubs;
using TaskFlow.API.Middleware;
using TaskFlow.API.Realtime;
using TaskFlow.Application;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure;
using TaskFlow.Infrastructure.Persistence;

// Allow unspecified DateTime kinds in Npgsql
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .CreateLogger();

builder.Host.UseSerilog();

// Layer registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

// API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

// Native .NET OpenAPI (replaces Swashbuckle for .NET 9+)
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "TaskFlow API",
            Version = "v1",
            Description = "Production-grade task management API"
        };
        return Task.CompletedTask;
    });
});

// CORS
var origins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

// Trust the reverse proxy (nginx) so rate limiting / audit see the real client IP
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Rate limiting: global per-user/IP budget + strict policy for credential endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please slow down." }, ct);
    };

    static string Key(HttpContext c) =>
        c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? c.Connection.RemoteIpAddress?.ToString() ?? "anon";

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(Key(ctx), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:RequestsPerMinute", 600),
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0
        }));

    options.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthRequestsPerMinute", 20),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// SignalR (Redis backplane when configured => scale-out across API instances)
var signalR = builder.Services.AddSignalR();
var redis = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redis))
    signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("taskflow"));

// Health checks: /health = liveness, /health/ready = dependencies
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("postgres", tags: ["ready"])
    .AddCheck<CacheHealthCheck>("cache", tags: ["ready"])
    .AddCheck<BrokerHealthCheck>("broker", tags: ["ready"]);

var app = builder.Build();

// Apply migrations (+ optional admin seed) on startup
await app.Services.InitializeDatabaseAsync(builder.Configuration);

// Middleware pipeline
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TaskFlow API")
               .WithTheme(Scalar.AspNetCore.ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(Scalar.AspNetCore.ScalarTarget.JavaScript, Scalar.AspNetCore.ScalarClient.Fetch);
    });
    app.UseHttpsRedirection();
}
else
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TaskFlowHub>("/hubs/taskflow");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithTags("Health").DisableRateLimiting();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), e.Value.Description })
        });
    },
    // Degraded (cache/broker down) still serves traffic; only an unhealthy DB takes the pod out of rotation.
    ResultStatusCodes = { [HealthStatus.Degraded] = StatusCodes.Status200OK }
}).DisableRateLimiting();

try
{
    Log.Information("Starting TaskFlow API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
