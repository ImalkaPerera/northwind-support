namespace SupportDesk.Application.Exceptions;

/// <summary>
/// The caller could not be authenticated. Mapped to 401. The message is deliberately generic,
/// so it never says whether the email or the password was the wrong part.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
