using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using BC = BCrypt.Net.BCrypt;

namespace TaskFlow.Application.Features.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _context;
    private readonly AuthTokenIssuer _issuer;

    public LoginCommandHandler(IApplicationDbContext context, AuthTokenIssuer issuer)
    {
        _context = context;
        _issuer = issuer;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same message for unknown user / inactive / bad password => no account enumeration.
        const string invalid = "Invalid email or password.";
        if (user is null || !user.IsActive) return Result<AuthResponse>.Failure(invalid, 401);

        if (user.IsLockedOut)
            return Result<AuthResponse>.Failure("Account temporarily locked due to repeated failed logins. Try again later.", 423);

        if (!BC.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(MaxFailedAttempts, LockoutDuration);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<AuthResponse>.Failure(invalid, 401);
        }

        user.RegisterSuccessfulLogin();
        var (response, _) = _issuer.Issue(user);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<AuthResponse>.Success(response);
    }
}
