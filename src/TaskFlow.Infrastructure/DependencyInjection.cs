using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.BackgroundJobs;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Database (retry on transient faults) ----
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b =>
                    {
                        b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    })
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ---- Redis (distributed cache) or in-memory fallback ----
        var redis = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "taskflow:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        services.AddSingleton<ICacheService, DistributedCacheService>();

        // ---- Core services ----
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IProjectAccessService, ProjectAccessService>();

        // ---- Messaging: transactional outbox -> RabbitMQ (or in-memory) -> idempotent consumers ----
        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddSingleton<IEventDispatcher, EventDispatcher>();
        services.AddScoped<NotificationEventHandler>();
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<DueDateReminderService>();

        if (!string.IsNullOrWhiteSpace(configuration["RabbitMq:Uri"]))
        {
            services.AddSingleton<RabbitMqBroker>();
            services.AddSingleton<IMessageBroker>(sp => sp.GetRequiredService<RabbitMqBroker>());
            services.AddHostedService<RabbitMqConsumerService>();
        }
        else
        {
            services.AddSingleton<InMemoryBroker>();
            services.AddSingleton<IMessageBroker>(sp => sp.GetRequiredService<InMemoryBroker>());
            services.AddHostedService<InMemoryConsumerService>();
        }

        // ---- JWT authentication ----
        var jwtKey = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be configured and at least 32 characters long.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // Allow SignalR to pass the token via query string
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },

                    // Server-side revocation: logout (jti blacklist) and "sign out everywhere" (per-user cutoff)
                    OnTokenValidated = async context =>
                    {
                        var cache = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                        var principal = context.Principal!;

                        var jti = principal.FindFirstValue("jti");
                        if (jti is not null && await cache.ExistsAsync(CacheKeys.RevokedToken(jti)))
                        {
                            context.Fail("Token has been revoked.");
                            return;
                        }

                        if (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                            && long.TryParse(principal.FindFirstValue("iat"), out var iat))
                        {
                            var revokedAfter = await cache.GetAsync<long?>(CacheKeys.UserRevokedAfter(userId));
                            if (revokedAfter.HasValue && iat < revokedAfter.Value)
                                context.Fail("Session has been revoked.");
                        }
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
            .AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("ProjectManager", "Admin"));

        return services;
    }

    /// <summary>Applies migrations and (optionally) bootstraps the first admin from configuration.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await db.Database.MigrateAsync();

        var email = configuration["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin)) return;

        var admin = User.Create("System", "Admin", email, BCrypt.Net.BCrypt.HashPassword(password), UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogWarning("Seeded initial admin account {Email}. Change this password immediately.", email);
    }
}
