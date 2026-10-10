using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private const string Reason = "Customer is losing money every hour";

    private static EscalateTicketRequest Request() => new(Reason, "Alex Turner");

    private static AgentDto Agent(int id, string name, int open, int max = 10, bool active = true) =>
        new(id, name, $"{name}@example.com", active, max, open, []);

    private static Ticket OpenTicket(
        TicketPriority priority = TicketPriority.High,
        int? agentId = 1,
        TicketStatus status = TicketStatus.Open) =>
        new TicketBuilder()
            .WithPriority(priority)
            .AssignedTo(agentId)
            .WithStatus(status)
            .DueAt(FixedClock.DefaultNow.AddHours(8))
            .Build();

    [Fact]
    public async Task RaisesThePriorityOneLevelAndRecalculatesTheDueDateFromNow()
    {
        var context = new TicketCommandTestContext { AgentList = [Agent(1, "Alex", open: 3)] };
        context.CustomerIs(CustomerTier.Standard);
        context.Clock.Advance(TimeSpan.FromHours(3));
        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.High));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);

        // Critical gives a standard customer 4 hours, counted from the moment of escalation.
        Assert.Equal(context.Clock.UtcNow.AddHours(4), ticket.DueAtUtc);
        Assert.Equal(TicketPriority.Critical, result.Escalation.ToPriority);
        Assert.Equal(TicketPriority.High, result.Escalation.FromPriority);
    }

    [Fact]
    public async Task WritesOneHistoryEntryAndSavesOnce()
    {
        var context = new TicketCommandTestContext { AgentList = [Agent(1, "Alex", open: 3)] };
        var ticket = context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(Reason, context.AddedEscalation.Reason);
        Assert.Equal("Alex Turner", context.AddedEscalation.EscalatedBy);
        context.Tickets.Verify(
            t => t.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()),
            Times.Once());
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task KeepsTheCurrentAgentWhileTheyAreStillEligible()
    {
        // Agent 1 is "6 of 6" only because of this very ticket, so they keep it.
        var context = new TicketCommandTestContext
        {
            AgentList = [Agent(1, "Alex", open: 6, max: 6), Agent(2, "Sara", open: 0)]
        };
        var ticket = context.ExistingTicket(OpenTicket(agentId: 1));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(1, ticket.AssignedAgentId);
        Assert.StartsWith("Kept with", result.Decision.AssignmentReason);
    }

    [Fact]
    public async Task ReassignsWhenTheCurrentAgentIsNoLongerActive()
    {
        var context = new TicketCommandTestContext
        {
            AgentList = [Agent(1, "Alex", open: 1, active: false), Agent(2, "Sara", open: 2)]
        };
        var ticket = context.ExistingTicket(OpenTicket(agentId: 1));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(1, result.Escalation.FromAgent?.Id);
        Assert.Equal(2, result.Escalation.ToAgent?.Id);
        Assert.StartsWith("Reassigned", result.Decision.AssignmentReason);
    }

    [Fact]
    public async Task WithNobodyEligible_TheTicketBecomesUnassignedAndSaysWhy()
    {
        var context = new TicketCommandTestContext { AgentList = [Agent(1, "Alex", open: 1, active: false)] };
        var ticket = context.ExistingTicket(OpenTicket(agentId: 1));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(result.Escalation.ToAgent);
        Assert.StartsWith("Left unassigned", result.Decision.AssignmentReason);
    }

    [Fact]
    public async Task AnUnknownTicket_IsNotFound()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task ACriticalTicket_IsRejectedAndNothingIsSaved()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.Critical));

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task AResolvedOrClosedTicket_IsRejectedAndNothingIsSaved(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.Low, status: status));

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        context.VerifySaved(Times.Never());
    }
}
