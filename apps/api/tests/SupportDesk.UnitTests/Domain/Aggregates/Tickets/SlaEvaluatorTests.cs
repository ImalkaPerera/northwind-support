using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    // An 8 hour window with a 25% threshold is at risk once 2 hours or less are left.
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);

    [Fact]
    public void Evaluate_OpenWithMoreThanTheThresholdLeft_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Now.AddHours(2).AddMinutes(1), null, Now, TwoHours));

    [Fact]
    public void Evaluate_OpenWithExactlyTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now.AddHours(2), null, Now, TwoHours));

    [Fact]
    public void Evaluate_OpenWithLessThanTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now.AddMinutes(1), null, Now, TwoHours));

    [Fact]
    public void Evaluate_OpenAndDueExactlyNow_IsAtRiskNotBreached() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now, null, Now, TwoHours));

    [Fact]
    public void Evaluate_OpenAndOneTickPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddTicks(-1), null, Now, TwoHours));

    [Fact]
    public void Evaluate_ResolvedExactlyAtDue_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1), TwoHours));

    [Fact]
    public void Evaluate_ResolvedTicketIsNeverAtRisk() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now.AddMinutes(10), Now, Now, TwoHours));

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicableEvenWithAThreshold() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now, TwoHours));
}
