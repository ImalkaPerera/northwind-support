using FluentValidation;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Checks that both fields were sent. It says nothing about whether they are right: that is the
/// handler's job, and it answers the same way for every wrong pair.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);

        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}
