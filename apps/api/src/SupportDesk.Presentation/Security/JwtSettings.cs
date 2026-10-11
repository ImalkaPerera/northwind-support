using System.Globalization;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// How tokens are signed and checked. Read once at startup from the <c>Jwt</c> section, so a
/// missing or weak signing key stops the application instead of failing on the first sign-in.
/// </summary>
/// <remarks>
/// The signing key is never written in code. In development it comes from
/// <c>appsettings.Development.json</c>; anywhere else it must come from the environment
/// (<c>Jwt__SigningKey</c>, listed in <c>.env.example</c>).
/// </remarks>
public sealed record JwtSettings(string SigningKey, string Issuer, string Audience, TimeSpan Lifetime)
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 wants a key at least as long as its 256-bit output.</summary>
    public const int MinimumKeyBytes = 32;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var signingKey = section["SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                $"The signing key '{SectionName}:SigningKey' is missing. Set it in the environment as " +
                "Jwt__SigningKey (see .env.example). It is never committed.");
        }

        if (Encoding.UTF8.GetByteCount(signingKey) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"The signing key '{SectionName}:SigningKey' must be at least {MinimumKeyBytes} bytes long.");
        }

        var issuer = Required(section, "Issuer");
        var audience = Required(section, "Audience");

        if (!double.TryParse(section["LifetimeMinutes"], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            || minutes <= 0)
        {
            throw new InvalidOperationException(
                $"The token lifetime '{SectionName}:LifetimeMinutes' is missing or is not a number above zero.");
        }

        return new JwtSettings(signingKey, issuer, audience, TimeSpan.FromMinutes(minutes));
    }

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    private static string Required(IConfigurationSection section, string key) =>
        string.IsNullOrWhiteSpace(section[key])
            ? throw new InvalidOperationException($"The setting '{SectionName}:{key}' is missing.")
            : section[key]!;
}
