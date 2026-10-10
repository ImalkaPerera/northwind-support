using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Saves escalation history to a real (in-memory SQLite) database and reads it back through a
/// second context, so what is checked is what the database stored.
/// </summary>
public sealed class TicketEscalationPersistenceTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<SupportDbContext> _options;

    private int _ticketId;
    private int _alexId;
    private int _saraId;

    public TicketEscalationPersistenceTests()
    {
        _connection.Open();

        _options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options;

        using var db = NewContext();
        db.Database.EnsureCreated();

        var customer = new Customer("Contoso", "it@contoso.test", null, CustomerTier.Premium, Now);
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var alex = new Agent("Alex Turner", "alex@northwind.test", 10, Now);
        var sara = new Agent("Sara Lindqvist", "sara@northwind.test", 6, Now);

        db.AddRange(customer, category, alex, sara);
        db.SaveChanges();

        var ticket = Ticket.Raise(
            "TCK-0001", "Invoice is wrong", "A description long enough to be realistic.",
            customer.Id, category.Id, TicketPriority.Low, Now, dueAtUtc: Now.AddHours(4));
        ticket.AssignTo(alex.Id, Now);

        db.Add(ticket);
        db.SaveChanges();

        _ticketId = ticket.Id;
        _alexId = alex.Id;
        _saraId = sara.Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task AnEscalation_IsStoredWithEverythingTheHistoryShows()
    {
        var later = Now.AddHours(1);

        using (var db = NewContext())
        {
            var ticket = await db.Set<Ticket>().SingleAsync(t => t.Id == _ticketId);
            var entry = ticket.Escalate("Customer is losing money", "alex", later.AddHours(4), _saraId, later);

            await new TicketRepository(db).AddEscalationAsync(entry, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        using var readBack = NewContext();
        var stored = await readBack.Set<TicketEscalation>().AsNoTracking().SingleAsync();
        var ticketAfter = await readBack.Set<Ticket>().AsNoTracking().SingleAsync();

        Assert.Equal(_ticketId, stored.TicketId);
        Assert.Equal(TicketPriority.Low, stored.FromPriority);
        Assert.Equal(TicketPriority.Medium, stored.ToPriority);
        Assert.Equal(_alexId, stored.FromAgentId);
        Assert.Equal(_saraId, stored.ToAgentId);
        Assert.Equal(Now.AddHours(4), stored.FromDueAtUtc);
        Assert.Equal(later.AddHours(4), stored.ToDueAtUtc);
        Assert.Equal("Customer is losing money", stored.Reason);
        Assert.Equal("alex", stored.EscalatedBy);
        Assert.Equal(later, stored.EscalatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.EscalatedAtUtc.Kind);

        // The ticket itself moved with it.
        Assert.Equal(TicketPriority.Medium, ticketAfter.Priority);
        Assert.Equal(_saraId, ticketAfter.AssignedAgentId);
    }

    [Fact]
    public async Task AnEscalation_CanRecordNoAgentBeforeOrAfter()
    {
        using (var db = NewContext())
        {
            var ticket = await db.Set<Ticket>().SingleAsync();
            ticket.AssignTo(null, Now);

            var entry = ticket.Escalate("Customer is losing money", "alex", Now.AddHours(4), newAgentId: null, Now);

            await new TicketRepository(db).AddEscalationAsync(entry, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        using var readBack = NewContext();
        var stored = await readBack.Set<TicketEscalation>().AsNoTracking().SingleAsync();

        Assert.Null(stored.FromAgentId);
        Assert.Null(stored.ToAgentId);
    }

    [Fact]
    public async Task ATicketWithHistory_CannotBeDeleted()
    {
        using (var db = NewContext())
        {
            var ticket = await db.Set<Ticket>().SingleAsync();
            var entry = ticket.Escalate("Customer is losing money", "alex", Now.AddHours(4), _alexId, Now);

            await new TicketRepository(db).AddEscalationAsync(entry, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        using var deleting = NewContext();
        var toDelete = await deleting.Set<Ticket>().SingleAsync();
        deleting.Remove(toDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleting.SaveChangesAsync());
    }

    [Fact]
    public async Task History_ForATicket_ComesBackNewestFirst()
    {
        using (var db = NewContext())
        {
            var ticket = await db.Set<Ticket>().SingleAsync();
            var repository = new TicketRepository(db);

            // Low becomes Medium, then High: two steps, so two entries.
            var first = ticket.Escalate("First reason here", "alex", Now.AddHours(30), _alexId, Now.AddHours(1));
            await repository.AddEscalationAsync(first, CancellationToken.None);

            var second = ticket.Escalate("Second reason here", "alex", Now.AddHours(10), _alexId, Now.AddHours(2));
            await repository.AddEscalationAsync(second, CancellationToken.None);

            await db.SaveChangesAsync();
        }

        using var readBack = NewContext();
        var history = await readBack.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(e => e.TicketId == _ticketId)
            .OrderByDescending(e => e.EscalatedAtUtc)
            .ToListAsync();

        Assert.Equal(["Second reason here", "First reason here"], history.Select(e => e.Reason));
    }

    private SupportDbContext NewContext() => new(_options);
}
