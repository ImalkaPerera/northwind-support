using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>What triage needs to know about a new ticket.</summary>
public sealed record NewTicketTriageInput(
    TicketPriority? RequestedPriority,
    int CategoryId,
    string CategoryName,
    bool CategoryRequiresSpecialist,
    bool CategoryForcesCritical,
    CustomerTier CustomerTier,
    DateTime NowUtc);

/// <summary>What triage needs to know about a ticket that is being escalated.</summary>
public sealed record EscalationTriageInput(
    Ticket Ticket,
    string CategoryName,
    bool CategoryRequiresSpecialist,
    CustomerTier CustomerTier,
    DateTime NowUtc);

/// <summary>The priority, due date and owner chosen for a ticket, with the reasons.</summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    DateTime DueAtUtc,
    AgentCandidate? Agent,
    TicketTriageDto Reasons);

/// <summary>
/// Applies the triage rules to a new ticket and to an escalation, so both go through the same
/// priority, due-date and owner logic. The rules themselves live in the domain; this puts them
/// together and explains the result.
/// </summary>
public sealed class TicketTriage(SlaPolicy policy)
{
    public TriageDecision ForNewTicket(NewTicketTriageInput input, IEnumerable<AgentCandidate> agents)
    {
        var priority = PriorityResolver.Resolve(input.RequestedPriority, input.CategoryForcesCritical);
        var dueAtUtc = policy.DueAt(input.NowUtc, priority.Priority, input.CustomerTier);
        var selection = AgentSelector.Select(agents, input.CategoryId, input.CategoryRequiresSpecialist);

        var reasons = new TicketTriageDto(
            PriorityReason(priority, input.CategoryName),
            SlaWindowReason(priority.Priority, input.CustomerTier, "creation"),
            AssignmentReason(selection, input.CategoryName, input.CategoryRequiresSpecialist, reassigned: false));

        return new TriageDecision(priority.Priority, dueAtUtc, selection.Agent, reasons);
    }

    /// <exception cref="SupportDesk.Domain.Exceptions.BusinessRuleViolationException">
    /// The ticket cannot be escalated.
    /// </exception>
    public TriageDecision ForEscalation(EscalationTriageInput input, IEnumerable<AgentCandidate> agents)
    {
        var ticket = input.Ticket;
        var from = ticket.Priority;
        var to = ticket.PriorityAfterEscalation();

        var dueAtUtc = policy.DueAt(input.NowUtc, to, input.CustomerTier);

        var selection = AgentSelector.KeepOrSelect(
            ticket.AssignedAgentId, agents, ticket.CategoryId, input.CategoryRequiresSpecialist);

        var reasons = new TicketTriageDto(
            $"Raised from {from} to {to}.",
            SlaWindowReason(to, input.CustomerTier, "escalation"),
            AssignmentReason(selection, input.CategoryName, input.CategoryRequiresSpecialist, reassigned: true));

        return new TriageDecision(to, dueAtUtc, selection.Agent, reasons);
    }

    private static string PriorityReason(PriorityDecision decision, string categoryName)
    {
        if (decision.ForcedByCategory)
        {
            return $"Set to Critical because the {categoryName} category always requires it.";
        }

        return decision.WasDefaulted
            ? $"No priority was requested, so the default ({decision.Priority}) was used."
            : $"The requested priority ({decision.Priority}) was used.";
    }

    private string SlaWindowReason(TicketPriority priority, CustomerTier tier, string startsAt)
    {
        var window = policy.WindowFor(priority, tier);
        var standardWindow = policy.WindowFor(priority, CustomerTier.Standard);

        var reason = $"Due {Describe(window)} after {startsAt}: {priority} tickets get {Describe(standardWindow)}";

        return window < standardWindow
            ? $"{reason}, shortened for a Premium customer."
            : $"{reason}.";
    }

    private static string AssignmentReason(
        AgentSelection selection,
        string categoryName,
        bool categoryRequiresSpecialist,
        bool reassigned)
    {
        switch (selection.Outcome)
        {
            case SelectionOutcome.KeptCurrent:
                return $"Kept with {selection.Agent!.FullName}, who is still eligible.";

            case SelectionOutcome.Selected:
                var verb = reassigned ? "Reassigned" : "Assigned";
                return $"{verb} to {selection.Agent!.FullName}: the eligible agent with the fewest open tickets " +
                       $"({selection.Agent.OpenTicketCount}).";

            case SelectionOutcome.AllQualifiedAgentsFull:
                return "Left unassigned: every eligible agent is at their open-ticket limit.";

            default:
                return categoryRequiresSpecialist
                    ? $"Left unassigned: no active agent has the {categoryName} specialization."
                    : "Left unassigned: there are no active agents.";
        }
    }

    private static string Describe(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        return $"{minutes} minutes";
    }
}

/// <summary>Maps the read model of an agent to what the selection rules need.</summary>
internal static class AgentCandidateMapping
{
    public static AgentCandidate ToCandidate(this AgentDto agent) =>
        new(
            agent.Id,
            agent.FullName,
            agent.IsActive,
            agent.MaxOpenTickets,
            agent.OpenTicketCount,
            agent.Specializations.Select(s => s.Id).ToHashSet());
}
