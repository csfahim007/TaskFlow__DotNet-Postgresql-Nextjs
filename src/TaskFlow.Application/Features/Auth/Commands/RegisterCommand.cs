using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Entities;
using BC = BCrypt.Net.BCrypt;

namespace TaskFlow.Application.Features.Auth.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
) : IRequest<Result<AuthResponse>>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly AuthTokenIssuer _issuer;

    public RegisterCommandHandler(IApplicationDbContext context, AuthTokenIssuer issuer)
    {
        _context = context;
        _issuer = issuer;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists) return Result<AuthResponse>.Failure("Email is already registered.", 409);

        var user = User.Create(request.FirstName.Trim(), request.LastName.Trim(), email, BC.HashPassword(request.Password));
        user.RegisterSuccessfulLogin();
        _context.Users.Add(user);

        var (response, _) = _issuer.Issue(user);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<AuthResponse>.Success(response);
    }
}
