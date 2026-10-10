using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the ticket list against a real (in-memory SQLite) database, so the filters are
/// translated to SQL for real and the count is the database's count.
/// </summary>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;

    private Customer _contoso = null!;
    private Customer _fabrikam = null!;
    private Category _billing = null!;
    private Category _technical = null!;
    private Agent _alex = null!;
    private Agent _sara = null!;

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();

        SeedReferenceData();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Priority_is_an_exact_match()
    {
        AddTicket("TCK-0001", priority: TicketPriority.High);
        AddTicket("TCK-0002", priority: TicketPriority.Low);
        AddTicket("TCK-0003", priority: TicketPriority.High);
        AddTicket("TCK-0004", priority: TicketPriority.Medium);

        var result = await Query(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        AddTicket("TCK-0001", customer: _contoso, category: _billing, priority: TicketPriority.High);
        AddTicket("TCK-0002", customer: _contoso, category: _technical, priority: TicketPriority.High);
        AddTicket("TCK-0003", customer: _fabrikam, category: _billing, priority: TicketPriority.High);
        AddTicket("TCK-0004", customer: _contoso, category: _billing, priority: TicketPriority.Low);

        var result = await Query(new TicketQuery
        {
            CustomerId = _contoso.Id,
            CategoryId = _billing.Id,
            Priority = TicketPriority.High
        });

        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_matches_the_customer_name()
    {
        AddTicket("TCK-0001", customer: _contoso, title: "Cannot log in");
        AddTicket("TCK-0002", customer: _fabrikam, title: "Cannot log in");

        var result = await Query(new TicketQuery { Search = "Contoso" });

        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
    }

    [Fact]
    public async Task Search_matches_the_title_and_the_reference()
    {
        AddTicket("TCK-0042", title: "Printer is on fire");
        AddTicket("TCK-0043", title: "Invoice total is wrong");

        var byTitle = await Query(new TicketQuery { Search = "Printer" });
        var byReference = await Query(new TicketQuery { Search = "TCK-0043" });

        Assert.Equal("TCK-0042", Assert.Single(byTitle.Items).Reference);
        Assert.Equal("TCK-0043", Assert.Single(byReference.Items).Reference);
    }

    [Fact]
    public async Task Search_is_trimmed_and_a_blank_search_is_ignored()
    {
        AddTicket("TCK-0001", customer: _contoso);
        AddTicket("TCK-0002", customer: _fabrikam);

        var trimmed = await Query(new TicketQuery { Search = "  Contoso  " });
        var blank = await Query(new TicketQuery { Search = "   " });

        Assert.Equal("TCK-0001", Assert.Single(trimmed.Items).Reference);
        Assert.Equal(2, blank.TotalCount);
    }

    [Fact]
    public async Task Unassigned_only_returns_tickets_with_no_agent()
    {
        AddTicket("TCK-0001", agent: _alex);
        AddTicket("TCK-0002");
        AddTicket("TCK-0003", agent: _sara);

        var result = await Query(new TicketQuery { UnassignedOnly = true });

        Assert.Equal("TCK-0002", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Assigned_agent_is_an_exact_match()
    {
        AddTicket("TCK-0001", agent: _alex);
        AddTicket("TCK-0002", agent: _sara);

        var result = await Query(new TicketQuery { AssignedAgentId = _sara.Id });

        Assert.Equal("TCK-0002", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Count_and_pages_describe_the_filtered_set_not_every_ticket()
    {
        for (var i = 1; i <= 5; i++)
        {
            AddTicket($"TCK-10{i:00}", customer: _contoso);
        }

        AddTicket("TCK-2001", customer: _fabrikam);
        AddTicket("TCK-2002", customer: _fabrikam);

        var result = await Query(new TicketQuery { CustomerId = _contoso.Id, PageSize = 2, Page = 3 });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Status_is_an_exact_match()
    {
        AddTicket("TCK-0001");
        AddTicket("TCK-0002", status: TicketStatus.InProgress);
        AddTicket("TCK-0003", status: TicketStatus.InProgress);

        var result = await Query(new TicketQuery { Status = TicketStatus.InProgress });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketStatus.InProgress, item.Status));
    }

    [Fact]
    public async Task Category_is_an_exact_match()
    {
        AddTicket("TCK-0001", category: _billing);
        AddTicket("TCK-0002", category: _technical);

        var result = await Query(new TicketQuery { CategoryId = _billing.Id });

        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Search_treats_wildcard_characters_as_plain_text(string search)
    {
        AddTicket("TCK-0001", title: "Cannot log in");
        AddTicket("TCK-0002", title: "Discount is 50% off");

        var result = await Query(new TicketQuery { Search = search });

        // "%" only matches the title that really contains a percent sign; "_" matches nothing.
        Assert.Equal(search == "%" ? 1 : 0, result.TotalCount);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_the_count_is_still_correct()
    {
        AddTicket("TCK-0001");
        AddTicket("TCK-0002");

        var result = await Query(new TicketQuery { Page = 5, PageSize = 20 });

        Assert.Empty(result.Items);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task No_filters_returns_every_ticket()
    {
        AddTicket("TCK-0001");
        AddTicket("TCK-0002");

        var result = await Query(new TicketQuery());

        Assert.Equal(2, result.TotalCount);
    }

    private Task<PagedResult<TicketListItemDto>> Query(TicketQuery query)
    {
        _db.SaveChanges();

        return new TicketQueries(_db, new FixedClock(), TestSlaPolicy.Default()).GetPagedAsync(query.Normalized(), CancellationToken.None);
    }

    private void SeedReferenceData()
    {
        _contoso = new Customer("Contoso", "it@contoso.test", null, CustomerTier.Premium, Now);
        _fabrikam = new Customer("Fabrikam", "it@fabrikam.test", null, CustomerTier.Standard, Now);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        _alex = new Agent("Alex Turner", "alex@northwind.test", maxOpenTickets: 10, Now);
        _sara = new Agent("Sara Lindqvist", "sara@northwind.test", maxOpenTickets: 6, Now);

        _db.AddRange(_contoso, _fabrikam, _billing, _technical, _alex, _sara);
        _db.SaveChanges();
    }

    private void AddTicket(
        string reference,
        Customer? customer = null,
        Category? category = null,
        Agent? agent = null,
        TicketPriority priority = TicketPriority.Medium,
        string title = "Something is not working",
        TicketStatus status = TicketStatus.New)
    {
        var ticket = Ticket.Raise(
            reference,
            title,
            "A description long enough to be realistic.",
            (customer ?? _contoso).Id,
            (category ?? _technical).Id,
            priority,
            Now);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, Now);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, Now);
        }

        _db.Add(ticket);
    }
}
