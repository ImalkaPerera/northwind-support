using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA policy the assignment describes (the Sla section of appsettings.Development.json),
/// so tests do not each repeat the numbers.
/// </summary>
public static class TestSlaPolicy
{
    public static SlaPolicy Default() => Create();

    public static SlaPolicy Create(
        double premiumMultiplier = 0.5,
        double minimumWindowHours = 1,
        double atRiskPercent = 25) =>
        new(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = 72,
                [TicketPriority.Medium] = 24,
                [TicketPriority.High] = 8,
                [TicketPriority.Critical] = 4
            },
            premiumMultiplier,
            minimumWindowHours,
            atRiskPercent);
}
