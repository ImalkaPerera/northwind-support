using Microsoft.Extensions.Configuration;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class SlaPolicyFactoryTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["Sla:BaseWindowHours:Low"] = "72",
        ["Sla:BaseWindowHours:Medium"] = "24",
        ["Sla:BaseWindowHours:High"] = "8",
        ["Sla:BaseWindowHours:Critical"] = "4",
        ["Sla:PremiumCustomerMultiplier"] = "0.5",
        ["Sla:MinimumWindowHours"] = "1",
        ["Sla:AtRiskThresholdPercent"] = "25"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ReadsTheWindowsAndThresholdsFromConfiguration()
    {
        var values = Valid();
        values["Sla:BaseWindowHours:Critical"] = "2";
        values["Sla:AtRiskThresholdPercent"] = "10";

        var policy = SlaPolicyFactory.FromConfiguration(Configuration(values));

        Assert.Equal(TimeSpan.FromHours(2), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
        Assert.Equal(10, policy.AtRiskThresholdPercent);
    }

    [Fact]
    public void ReadsNumbersWhateverTheCurrentCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var policy = SlaPolicyFactory.FromConfiguration(Configuration(Valid()));

            Assert.Equal(TimeSpan.FromHours(12), policy.WindowFor(TicketPriority.Medium, CustomerTier.Premium));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void AMissingSection_FailsWithAClearMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.FromConfiguration(Configuration([])));

        Assert.Contains("Sla", exception.Message);
    }

    [Fact]
    public void AMissingValue_NamesTheSettingThatIsWrong()
    {
        var values = Valid();
        values.Remove("Sla:BaseWindowHours:High");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.FromConfiguration(Configuration(values)));

        Assert.Contains("Sla:BaseWindowHours:High", exception.Message);
    }

    [Fact]
    public void ANonNumericValue_IsRejected()
    {
        var values = Valid();
        values["Sla:MinimumWindowHours"] = "one hour";

        Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.FromConfiguration(Configuration(values)));
    }
}
