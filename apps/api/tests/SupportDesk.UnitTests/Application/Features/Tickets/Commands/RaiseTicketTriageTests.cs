using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>Triage when a ticket is raised: priority, due date and owner are decided by the rules.</summary>
public class RaiseTicketTriageTests
{
    private const int BillingCategoryId = 2;

    private static CreateTicketRequest Request(TicketPriority? priority = null, int categoryId = 3) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, categoryId, priority);

    private static AgentDto Agent(
        int id,
        string name,
        int open,
        int max = 10,
        bool active = true,
        params int[] specializations) =>
        new(
            id,
            name,
            $"{name}@example.com",
            active,
            max,
            open,
            specializations.Select(s => new CategorySummaryDto(s, $"Category {s}")).ToList());

    [Fact]
    public async Task ACategoryThatForcesCritical_AlwaysGivesACriticalTicket()
    {
        var context = new TicketCommandTestContext();
        context.CategoryIs(requiresSpecialist: false, forcesCritical: true, name: "Security");

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Contains("Security", result.Triage.PriorityReason);
    }

    [Fact]
    public async Task ThePremiumWindowIsHalfOfTheBaseWindow()
    {
        var context = new TicketCommandTestContext();
        context.CustomerIs(CustomerTier.Premium);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket!.DueAtUtc);
        Assert.Contains("Premium", result.Triage.SlaWindowReason);
    }

    [Fact]
    public async Task AStandardCustomerGetsTheFullBaseWindow()
    {
        var context = new TicketCommandTestContext();
        context.CustomerIs(CustomerTier.Standard);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(8), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task TheTicketGoesToTheEligibleAgentWithTheFewestOpenTickets()
    {
        var context = new TicketCommandTestContext
        {
            AgentList = [Agent(1, "Alex", open: 5), Agent(2, "Sara", open: 2), Agent(3, "Ben", open: 4)]
        };

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
        Assert.Contains("Sara", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task ASpecialistCategoryOnlyGoesToASpecialist()
    {
        var context = new TicketCommandTestContext
        {
            AgentList =
            [
                Agent(1, "Alex", open: 0),
                Agent(2, "Sara", open: 5, specializations: BillingCategoryId)
            ]
        };
        context.CategoryIs(requiresSpecialist: true, forcesCritical: false, name: "Billing");

        await context.RaiseTicket.HandleAsync(Request(categoryId: BillingCategoryId), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
    }

    [Fact]
    public async Task WithNoEligibleAgent_TheTicketIsStillRaisedUnassignedWithAReason()
    {
        var context = new TicketCommandTestContext
        {
            AgentList = [Agent(1, "Alex", open: 10, max: 10), Agent(2, "Sara", open: 0, active: false)]
        };

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.StartsWith("Left unassigned", result.Triage.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task ASpecialistCategoryWithNoSpecialist_SaysSo()
    {
        var context = new TicketCommandTestContext { AgentList = [Agent(1, "Alex", open: 0)] };
        context.CategoryIs(requiresSpecialist: true, forcesCritical: false, name: "Billing");

        var result = await context.RaiseTicket.HandleAsync(Request(categoryId: BillingCategoryId), CancellationToken.None);

        Assert.Contains("Billing specialization", result.Triage.AssignmentReason);
    }
}
