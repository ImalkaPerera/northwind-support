using System.Globalization;
using Microsoft.Extensions.Configuration;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Builds the <see cref="SlaPolicy"/> from the <c>Sla</c> configuration section, so the windows,
/// the premium multiplier, the floor and the at-risk threshold change without touching code.
/// A missing or malformed value stops the application at startup instead of failing later.
/// </summary>
public static class SlaPolicyFactory
{
    public const string SectionName = "Sla";

    public static SlaPolicy FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' configuration section is missing. See appsettings.json.");
        }

        var windows = section.GetSection("BaseWindowHours");

        var baseWindowHours = new Dictionary<TicketPriority, double>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            baseWindowHours[priority] = Read(windows.GetSection(priority.ToString()));
        }

        return new SlaPolicy(
            baseWindowHours,
            premiumCustomerMultiplier: Read(section.GetSection("PremiumCustomerMultiplier")),
            minimumWindowHours: Read(section.GetSection("MinimumWindowHours")),
            atRiskThresholdPercent: Read(section.GetSection("AtRiskThresholdPercent")));
    }

    private static double Read(IConfigurationSection value)
    {
        if (!double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            throw new InvalidOperationException(
                $"The configuration value '{value.Path}' is missing or is not a number.");
        }

        return number;
    }
}
