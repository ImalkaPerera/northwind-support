using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA status filter against a real (in-memory SQLite) database. With the default policy a
/// High ticket for a standard customer has an 8 hour window, so it is at risk with 2 hours or
/// less left; a Critical ticket for a premium customer has 2 hours, at risk with 30 minutes left.
/// </summary>
public sealed class TicketQueriesSlaFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;

    private readonly Customer _standard = new("Fabrikam", "it@fabrikam.test", null, CustomerTier.Standard, Now);
    private readonly Customer _premium = new("Contoso", "it@contoso.test", null, CustomerTier.Premium, Now);
    private readonly Category _category = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);

    private int _next = 1;

    public TicketQueriesSlaFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();

        _db.AddRange(_standard, _premium, _category);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task NotApplicable_IsATicketWithNoDueDate()
    {
        var none = Add(due: null);
        Add(due: Now.AddHours(5));

        Assert.Equal([none], await References(SlaStatus.NotApplicable));
    }

    [Fact]
    public async Task WithinSla_IsOpenWithMoreThanTheThresholdLeft()
    {
        var comfortable = Add(due: Now.AddHours(3));
        Add(due: Now.AddHours(1));

        Assert.Equal([comfortable], await References(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task AtRisk_IncludesExactlyTheThreshold()
    {
        var atThreshold = Add(due: Now.AddHours(2));
        var justOver = Add(due: Now.AddHours(2).AddMinutes(1));

        var atRisk = await References(SlaStatus.AtRisk);

        Assert.Contains(atThreshold, atRisk);
        Assert.DoesNotContain(justOver, atRisk);
    }

    [Fact]
    public async Task AtRisk_IncludesATicketDueExactlyNow()
    {
        var dueNow = Add(due: Now);

        Assert.Equal([dueNow], await References(SlaStatus.AtRisk));
    }

    [Fact]
    public async Task Breached_IsOpenAndPastDue()
    {
        var late = Add(due: Now.AddMinutes(-1));
        Add(due: Now);

        Assert.Equal([late], await References(SlaStatus.Breached));
    }

    [Fact]
    public async Task Breached_AlsoIncludesAResolvedTicketThatWasLate()
    {
        var lateResolved = Add(due: Now.AddHours(-3), resolvedAt: Now.AddHours(-1));

        Assert.Equal([lateResolved], await References(SlaStatus.Breached));
    }

    [Fact]
    public async Task Met_IsResolvedOnOrBeforeTheDueDate()
    {
        var onTime = Add(due: Now.AddHours(-1), resolvedAt: Now.AddHours(-2));
        var exactlyOnDue = Add(due: Now.AddHours(-1), resolvedAt: Now.AddHours(-1));
        Add(due: Now.AddHours(-3), resolvedAt: Now.AddHours(-1));

        var met = await References(SlaStatus.Met);

        Assert.Equal(2, met.Count);
        Assert.Contains(onTime, met);
        Assert.Contains(exactlyOnDue, met);
    }

    [Fact]
    public async Task AResolvedTicketIsNeverAtRiskOrWithin()
    {
        Add(due: Now.AddMinutes(30), resolvedAt: Now.AddMinutes(-10));

        Assert.Empty(await References(SlaStatus.AtRisk));
        Assert.Empty(await References(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task TheTierDecidesTheAtRiskWindow()
    {
        // 40 minutes left. A standard Critical ticket has a 4 hour window, so it is at risk with
        // under an hour left. A premium one has 2 hours, so it is only at risk under 30 minutes.
        var standard = Add(due: Now.AddMinutes(40), priority: TicketPriority.Critical, premium: false);
        var premium = Add(due: Now.AddMinutes(40), priority: TicketPriority.Critical, premium: true);

        Assert.Equal([standard], await References(SlaStatus.AtRisk));
        Assert.Equal([premium], await References(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task ThePriorityDecidesTheAtRiskWindow()
    {
        // 3 hours left. A Medium ticket has a 24 hour window (at risk under 6 hours), so it is at
        // risk. A High ticket has 8 hours (at risk under 2 hours), so it is not.
        var medium = Add(due: Now.AddHours(3), priority: TicketPriority.Medium);
        var high = Add(due: Now.AddHours(3), priority: TicketPriority.High);

        Assert.Equal([medium], await References(SlaStatus.AtRisk));
        Assert.Equal([high], await References(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task TheCountAndThePagesDescribeTheFilteredSet()
    {
        for (var i = 0; i < 5; i++)
        {
            Add(due: Now.AddMinutes(-10 - i));
        }

        Add(due: Now.AddHours(5));

        var page = await Query(new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 2, Page = 3 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task ItCombinesWithTheOtherFilters()
    {
        var premiumLate = Add(due: Now.AddMinutes(-5), premium: true);
        Add(due: Now.AddMinutes(-5), premium: false);

        var result = await Query(new TicketQuery { SlaStatus = SlaStatus.Breached, CustomerId = _premium.Id });

        Assert.Equal(premiumLate, Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task EveryStatusFilterAgreesWithTheStatusShownOnTheRow()
    {
        // A spread of tickets around every boundary, for both tiers and several priorities.
        foreach (var premium in new[] { false, true })
        {
            foreach (var priority in Enum.GetValues<TicketPriority>())
            {
                foreach (var minutes in new[] { -300, -1, 0, 1, 29, 30, 31, 59, 60, 61, 119, 120, 121, 400, 1500 })
                {
                    Add(due: Now.AddMinutes(minutes), priority: priority, premium: premium);
                    Add(due: Now.AddMinutes(minutes), priority: priority, premium: premium, resolvedAt: Now.AddMinutes(-5));
                }
            }
        }

        Add(due: null);

        var everything = await Query(new TicketQuery { PageSize = TicketQuery.MaxPageSize });
        var shown = new List<TicketListItemDto>(everything.Items);

        for (var page = 2; page <= everything.TotalPages; page++)
        {
            shown.AddRange((await Query(new TicketQuery { PageSize = TicketQuery.MaxPageSize, Page = page })).Items);
        }

        Assert.Equal(everything.TotalCount, shown.Count);

        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            var expected = shown.Where(t => t.SlaStatus == status).Select(t => t.Reference).Order().ToList();
            var actual = (await ReferencesAllPages(status)).Order().ToList();

            Assert.Equal(expected, actual);
        }

        // Every status was actually exercised, so the comparison above means something.
        Assert.All(Enum.GetValues<SlaStatus>(), status => Assert.Contains(shown, t => t.SlaStatus == status));
    }

    private string Add(
        DateTime? due,
        DateTime? resolvedAt = null,
        TicketPriority priority = TicketPriority.High,
        bool premium = false)
    {
        var reference = $"TCK-{_next++:D4}";

        var ticket = Ticket.Raise(
            reference,
            "Something is not working",
            "A description long enough to be realistic.",
            (premium ? _premium : _standard).Id,
            _category.Id,
            priority,
            Now.AddDays(-10),
            due);

        if (resolvedAt is { } resolved)
        {
            ticket.ChangeStatus(TicketStatus.Resolved, resolved);
        }

        _db.Add(ticket);

        return reference;
    }

    private async Task<List<string>> References(SlaStatus status) =>
        (await Query(new TicketQuery { SlaStatus = status, PageSize = TicketQuery.MaxPageSize }))
        .Items.Select(t => t.Reference).ToList();

    private async Task<List<string>> ReferencesAllPages(SlaStatus status)
    {
        var first = await Query(new TicketQuery { SlaStatus = status, PageSize = TicketQuery.MaxPageSize });
        var all = first.Items.Select(t => t.Reference).ToList();

        for (var page = 2; page <= first.TotalPages; page++)
        {
            var next = await Query(new TicketQuery { SlaStatus = status, PageSize = TicketQuery.MaxPageSize, Page = page });
            all.AddRange(next.Items.Select(t => t.Reference));
        }

        return all;
    }

    private async Task<PagedResult<TicketListItemDto>> Query(TicketQuery query)
    {
        _db.SaveChanges();

        return await new TicketQueries(_db, new FixedClock(), TestSlaPolicy.Default())
            .GetPagedAsync(query.Normalized(), CancellationToken.None);
    }
}
