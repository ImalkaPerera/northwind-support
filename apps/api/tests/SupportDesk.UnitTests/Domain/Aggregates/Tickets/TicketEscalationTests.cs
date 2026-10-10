using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow.AddHours(5);
    private static readonly DateTime NewDue = Now.AddHours(4);

    private static Ticket OpenTicket(TicketPriority priority = TicketPriority.Medium, int? agentId = 3) =>
        new TicketBuilder()
            .WithPriority(priority)
            .AssignedTo(agentId)
            .DueAt(FixedClock.DefaultNow.AddHours(24))
            .Build();

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesThePriorityByExactlyOneLevel(TicketPriority from, TicketPriority to)
    {
        var ticket = OpenTicket(from);

        ticket.Escalate("Customer is losing money", "alex", NewDue, newAgentId: 3, Now);

        Assert.Equal(to, ticket.Priority);
    }

    [Fact]
    public void Escalate_SetsTheNewDueDateAndOwner()
    {
        var ticket = OpenTicket(agentId: 3);

        ticket.Escalate("Customer is losing money", "alex", NewDue, newAgentId: 7, Now);

        Assert.Equal(NewDue, ticket.DueAtUtc);
        Assert.Equal(7, ticket.AssignedAgentId);
        Assert.Equal(Now, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_RecordsWhatChanged()
    {
        var ticket = OpenTicket(TicketPriority.High, agentId: 3);
        var previousDue = ticket.DueAtUtc;

        var entry = ticket.Escalate("Customer is losing money", "alex", NewDue, newAgentId: 7, Now);

        Assert.Equal(TicketPriority.High, entry.FromPriority);
        Assert.Equal(TicketPriority.Critical, entry.ToPriority);
        Assert.Equal(3, entry.FromAgentId);
        Assert.Equal(7, entry.ToAgentId);
        Assert.Equal(previousDue, entry.FromDueAtUtc);
        Assert.Equal(NewDue, entry.ToDueAtUtc);
        Assert.Equal("Customer is losing money", entry.Reason);
        Assert.Equal("alex", entry.EscalatedBy);
        Assert.Equal(Now, entry.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_OfAnUnassignedTicket_RecordsNoPreviousAgent()
    {
        var ticket = OpenTicket(agentId: null);

        var entry = ticket.Escalate("Customer is losing money", "alex", NewDue, newAgentId: null, Now);

        Assert.Null(entry.FromAgentId);
        Assert.Null(entry.ToAgentId);
    }

    [Fact]
    public void Escalate_TrimsTheReasonAndTheActor()
    {
        var entry = OpenTicket().Escalate("  Customer is losing money  ", "  alex  ", NewDue, 3, Now);

        Assert.Equal("Customer is losing money", entry.Reason);
        Assert.Equal("alex", entry.EscalatedBy);
    }

    [Fact]
    public void Escalate_ACriticalTicket_IsRejectedAndNothingChanges()
    {
        var ticket = OpenTicket(TicketPriority.Critical);
        var due = ticket.DueAtUtc;

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer is losing money", "alex", NewDue, 3, Now));

        Assert.Contains("Critical", exception.Message);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(due, ticket.DueAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_AResolvedOrClosedTicket_IsRejectedAndSaysWhich(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer is losing money", "alex", NewDue, null, Now));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("shrt")]
    public void Escalate_WithAReasonThatIsTooShort_IsRejected(string reason) =>
        Assert.Throws<ArgumentException>(() => OpenTicket().Escalate(reason, "alex", NewDue, 3, Now));

    [Fact]
    public void Escalate_WithAFiveCharacterReason_IsAccepted() =>
        Assert.Equal(
            "12345",
            OpenTicket().Escalate("12345", "alex", NewDue, 3, Now).Reason);

    [Fact]
    public void Escalate_WithAReasonOverTheLimit_IsRejected() =>
        Assert.Throws<ArgumentException>(
            () => OpenTicket().Escalate(new string('x', TicketEscalation.MaxReasonLength + 1), "alex", NewDue, 3, Now));

    [Fact]
    public void Escalate_WithAReasonAtTheLimit_IsAccepted() =>
        Assert.Equal(
            TicketEscalation.MaxReasonLength,
            OpenTicket().Escalate(new string('x', TicketEscalation.MaxReasonLength), "alex", NewDue, 3, Now).Reason.Length);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_WithoutWhoDidIt_IsRejected(string escalatedBy) =>
        Assert.Throws<ArgumentException>(
            () => OpenTicket().Escalate("Customer is losing money", escalatedBy, NewDue, 3, Now));

    [Fact]
    public void Raise_WithADueDate_StartsWithIt()
    {
        var due = FixedClock.DefaultNow.AddHours(8);

        var ticket = Ticket.Raise("TCK-0050", "Title", "Description", 1, 1, TicketPriority.High, FixedClock.DefaultNow, due);

        Assert.Equal(due, ticket.DueAtUtc);
    }
}
