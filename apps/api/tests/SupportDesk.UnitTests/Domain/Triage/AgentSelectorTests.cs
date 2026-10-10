using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectorTests
{
    private const int Billing = 2;
    private const int Technical = 3;

    private static AgentCandidate Agent(
        int id,
        int open = 0,
        int max = 10,
        bool active = true,
        params int[] specializations) =>
        new(id, $"Agent {id}", active, max, open, specializations.ToHashSet());

    [Fact]
    public void Select_ChoosesTheAgentWithTheFewestOpenTickets()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 5), Agent(2, open: 2), Agent(3, open: 4)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
    }

    [Fact]
    public void Select_WithATie_ChoosesTheLowestId()
    {
        var selection = AgentSelector.Select(
            [Agent(7, open: 3), Agent(4, open: 3), Agent(9, open: 3)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(4, selection.Agent?.Id);
    }

    [Fact]
    public void Select_GivesTheSameAnswerWhateverTheOrder()
    {
        var agents = new[] { Agent(1, open: 3), Agent(2, open: 3), Agent(3, open: 3) };

        var forward = AgentSelector.Select(agents, Technical, false);
        var backward = AgentSelector.Select(agents.Reverse(), Technical, false);

        Assert.Equal(forward.Agent?.Id, backward.Agent?.Id);
    }

    [Fact]
    public void Select_SkipsInactiveAgents()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 0, active: false), Agent(2, open: 8)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WhenASpecialistIsRequired_OnlyConsidersSpecialists()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 0), Agent(2, open: 6, specializations: Billing)], Billing, categoryRequiresSpecialist: true);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WhenNoSpecialistIsRequired_AnyActiveAgentWillDo()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 0), Agent(2, open: 6, specializations: Billing)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(1, selection.Agent?.Id);
    }

    [Fact]
    public void Select_AnAgentAtTheirLimitIsNotEligible()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 6, max: 6), Agent(2, open: 5, max: 6)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_AnAgentOneBelowTheirLimitIsEligible()
    {
        var selection = AgentSelector.Select([Agent(1, open: 5, max: 6)], Technical, false);

        Assert.Equal(1, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WithNoQualifiedAgent_ReturnsNoOwnerAndSaysWhy()
    {
        var selection = AgentSelector.Select(
            [Agent(1), Agent(2, active: false, specializations: Billing)], Billing, categoryRequiresSpecialist: true);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.NoQualifiedAgent, selection.Outcome);
    }

    [Fact]
    public void Select_WhenEveryQualifiedAgentIsFull_ReturnsNoOwnerAndSaysWhy()
    {
        var selection = AgentSelector.Select(
            [Agent(1, open: 6, max: 6, specializations: Billing)], Billing, categoryRequiresSpecialist: true);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.AllQualifiedAgentsFull, selection.Outcome);
    }

    [Fact]
    public void Select_WithNoAgentsAtAll_ReturnsNoOwner()
    {
        var selection = AgentSelector.Select([], Technical, categoryRequiresSpecialist: false);

        Assert.Null(selection.Agent);
    }

    [Fact]
    public void KeepOrSelect_KeepsTheCurrentAgentWhileTheyAreStillEligible()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 2, [Agent(1, open: 0), Agent(2, open: 5)], Technical, categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.KeptCurrent, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_DoesNotHoldTheEscalatedTicketAgainstItsOwnAgent()
    {
        // 6 of 6 includes the ticket being escalated, so the agent is really at 5 without it.
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1, [Agent(1, open: 6, max: 6), Agent(2, open: 0)], Technical, false);

        Assert.Equal(1, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.KeptCurrent, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_ReassignsWhenTheCurrentAgentIsNoLongerActive()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1, [Agent(1, open: 1, active: false), Agent(2, open: 3)], Technical, false);

        Assert.Equal(2, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_ReassignsWhenTheCurrentAgentLacksTheSpecialization()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1,
            [Agent(1, open: 1), Agent(2, open: 3, specializations: Billing)],
            Billing,
            categoryRequiresSpecialist: true);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void KeepOrSelect_WithNoCurrentAgent_SelectsLikeANewTicket()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: null, [Agent(1, open: 4), Agent(2, open: 1)], Technical, false);

        Assert.Equal(2, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_WhenNobodyIsEligible_ReturnsNoOwner()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1, [Agent(1, active: false)], Technical, categoryRequiresSpecialist: false);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.NoQualifiedAgent, selection.Outcome);
    }
}
