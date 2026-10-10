using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The response-window rules: how long a ticket may stay unresolved, and how close to its due
/// date it counts as at risk. Built from configuration, so the numbers can change without
/// touching code.
/// </summary>
public sealed class SlaPolicy
{
    private readonly Dictionary<TicketPriority, double> _baseWindowHours;
    private readonly double _premiumCustomerMultiplier;
    private readonly double _minimumWindowHours;

    public SlaPolicy(
        IReadOnlyDictionary<TicketPriority, double> baseWindowHours,
        double premiumCustomerMultiplier,
        double minimumWindowHours,
        double atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(baseWindowHours);

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!baseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                throw new ArgumentException(
                    $"A base window greater than zero is required for priority {priority}.",
                    nameof(baseWindowHours));
            }
        }

        if (premiumCustomerMultiplier is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(premiumCustomerMultiplier), "The premium multiplier must be above 0 and at most 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minimumWindowHours);

        if (atRiskThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent), "The at-risk threshold must be between 0 and 100.");
        }

        _baseWindowHours = new Dictionary<TicketPriority, double>(baseWindowHours);
        _premiumCustomerMultiplier = premiumCustomerMultiplier;
        _minimumWindowHours = minimumWindowHours;
        AtRiskThresholdPercent = atRiskThresholdPercent;
    }

    /// <summary>A ticket is at risk once this share of its window, or less, is left.</summary>
    public double AtRiskThresholdPercent { get; }

    /// <summary>
    /// The response window for a priority and customer tier. Premium customers get the base
    /// window multiplied down, but never below the minimum, and never longer than the
    /// standard window.
    /// </summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var baseHours = _baseWindowHours[priority];

        var hours = tier == CustomerTier.Premium
            ? Math.Min(baseHours, Math.Max(baseHours * _premiumCustomerMultiplier, _minimumWindowHours))
            : baseHours;

        return TimeSpan.FromHours(hours);
    }

    /// <summary>When a ticket started at <paramref name="startUtc"/> is due.</summary>
    public DateTime DueAt(DateTime startUtc, TicketPriority priority, CustomerTier tier) =>
        startUtc + WindowFor(priority, tier);

    /// <summary>How much time left, or less, makes a ticket of this kind at risk.</summary>
    public TimeSpan AtRiskSpanFor(TicketPriority priority, CustomerTier tier) =>
        WindowFor(priority, tier) * (AtRiskThresholdPercent / 100);
}
