using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void WindowFor_StandardCustomer_IsTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), TestSlaPolicy.Default().WindowFor(priority, CustomerTier.Standard));

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void WindowFor_PremiumCustomer_IsHalfTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), TestSlaPolicy.Default().WindowFor(priority, CustomerTier.Premium));

    [Fact]
    public void WindowFor_PremiumCustomer_IsNeverShorterThanTheMinimum()
    {
        // 4 h * 0.1 would be 24 minutes; the floor keeps it at one hour.
        var policy = TestSlaPolicy.Create(premiumMultiplier: 0.1, minimumWindowHours: 1);

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumCustomer_ExactlyAtTheMinimum_IsTheMinimum()
    {
        // 4 h * 0.25 is exactly the one hour floor.
        var policy = TestSlaPolicy.Create(premiumMultiplier: 0.25, minimumWindowHours: 1);

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumCustomer_IsNeverLongerThanStandard()
    {
        // A minimum above the base window must not stretch a premium window past the standard one.
        var policy = TestSlaPolicy.Create(minimumWindowHours: 10);

        Assert.Equal(TimeSpan.FromHours(4), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_UsesTheConfiguredValues()
    {
        var policy = new SlaPolicy(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = 100,
                [TicketPriority.Medium] = 50,
                [TicketPriority.High] = 20,
                [TicketPriority.Critical] = 10
            },
            premiumCustomerMultiplier: 0.8,
            minimumWindowHours: 1,
            atRiskThresholdPercent: 25);

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(8), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void DueAt_IsTheStartPlusTheWindow() =>
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(8),
            TestSlaPolicy.Default().DueAt(FixedClock.DefaultNow, TicketPriority.High, CustomerTier.Standard));

    [Fact]
    public void AtRiskSpanFor_IsTheThresholdShareOfTheWindow() =>
        Assert.Equal(
            TimeSpan.FromHours(2),
            TestSlaPolicy.Default().AtRiskSpanFor(TicketPriority.High, CustomerTier.Standard));

    [Fact]
    public void Constructor_WithAMissingPriority_IsRejected()
    {
        var incomplete = new Dictionary<TicketPriority, double> { [TicketPriority.Low] = 72 };

        Assert.Throws<ArgumentException>(() => new SlaPolicy(incomplete, 0.5, 1, 25));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Constructor_WithAnInvalidMultiplier_IsRejected(double multiplier) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSlaPolicy.Create(premiumMultiplier: multiplier));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_WithAnInvalidThreshold_IsRejected(double percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSlaPolicy.Create(atRiskPercent: percent));
}
