namespace SupportDesk.Domain.Triage;

/// <summary>An agent as the selection rules see them: just the facts the rules need.</summary>
/// <param name="OpenTicketCount">Tickets that are not Resolved or Closed.</param>
/// <param name="SpecializationCategoryIds">Categories this agent is a specialist in.</param>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlySet<int> SpecializationCategoryIds);

/// <summary>Why selection ended the way it did.</summary>
public enum SelectionOutcome
{
    /// <summary>An agent was chosen.</summary>
    Selected,

    /// <summary>The ticket's current agent is still eligible, so they keep it.</summary>
    KeptCurrent,

    /// <summary>No active agent has the specialization the category requires.</summary>
    NoQualifiedAgent,

    /// <summary>There are qualified agents, but every one is at their open-ticket limit.</summary>
    AllQualifiedAgentsFull
}

/// <summary>The result of choosing an owner. Having no agent is a normal outcome, not an error.</summary>
public sealed record AgentSelection(AgentCandidate? Agent, SelectionOutcome Outcome);

/// <summary>
/// Chooses the owner of a ticket. An agent is eligible when they are active, have the category's
/// specialization where it is required, and are strictly below their open-ticket limit. Among
/// the eligible, the one with the fewest open tickets wins, and the lowest id breaks a tie, so
/// the same input always gives the same answer.
/// </summary>
public static class AgentSelector
{
    public static AgentSelection Select(
        IEnumerable<AgentCandidate> candidates,
        int categoryId,
        bool categoryRequiresSpecialist)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var qualified = candidates
            .Where(agent => IsQualified(agent, categoryId, categoryRequiresSpecialist))
            .ToList();

        if (qualified.Count == 0)
        {
            return new AgentSelection(null, SelectionOutcome.NoQualifiedAgent);
        }

        var chosen = qualified
            .Where(HasRoom)
            .OrderBy(agent => agent.OpenTicketCount)
            .ThenBy(agent => agent.Id)
            .FirstOrDefault();

        return chosen is null
            ? new AgentSelection(null, SelectionOutcome.AllQualifiedAgentsFull)
            : new AgentSelection(chosen, SelectionOutcome.Selected);
    }

    /// <summary>
    /// Re-checks an owner after an escalation: the current agent keeps the ticket while they
    /// are still eligible, otherwise the ticket is reassigned like a new one.
    /// </summary>
    /// <remarks>
    /// The ticket being escalated is already counted in its owner's open tickets, so it is
    /// not held against them here. Otherwise an agent at their limit would lose every ticket
    /// they were already working on.
    /// </remarks>
    public static AgentSelection KeepOrSelect(
        int? currentAgentId,
        IEnumerable<AgentCandidate> candidates,
        int categoryId,
        bool categoryRequiresSpecialist)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var list = candidates.ToList();

        var current = currentAgentId is { } id ? list.FirstOrDefault(agent => agent.Id == id) : null;

        if (current is not null)
        {
            var withoutThisTicket = current with { OpenTicketCount = Math.Max(0, current.OpenTicketCount - 1) };

            if (IsQualified(withoutThisTicket, categoryId, categoryRequiresSpecialist) && HasRoom(withoutThisTicket))
            {
                return new AgentSelection(current, SelectionOutcome.KeptCurrent);
            }
        }

        return Select(list, categoryId, categoryRequiresSpecialist);
    }

    private static bool IsQualified(AgentCandidate agent, int categoryId, bool categoryRequiresSpecialist) =>
        agent.IsActive
        && (!categoryRequiresSpecialist || agent.SpecializationCategoryIds.Contains(categoryId));

    private static bool HasRoom(AgentCandidate agent) => agent.OpenTicketCount < agent.MaxOpenTickets;
}
