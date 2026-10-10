using System.Linq.Expressions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Turns an SLA status into a condition the database can evaluate. The status is derived from
/// the due date, the resolution time and "now", exactly as <see cref="SlaEvaluator"/> derives it
/// in memory, so the filter and the status shown on a row cannot disagree.
/// </summary>
/// <remarks>
/// The "at risk" span depends on the ticket's priority and the customer's tier. There are only
/// a handful of combinations, so each span is worked out here, in C#, and the database only
/// compares dates with constants. No date arithmetic runs in SQL.
/// </remarks>
internal static class SlaStatusFilter
{
    public static Expression<Func<TicketWithLabels, bool>> For(SlaStatus status, DateTime nowUtc, SlaPolicy policy)
    {
        var atRisk = AtRisk(nowUtc, policy);

        return status switch
        {
            SlaStatus.NotApplicable => x => x.Ticket.DueAtUtc == null,

            SlaStatus.Met => x =>
                x.Ticket.DueAtUtc != null
                && x.Ticket.ResolvedAtUtc != null
                && x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc,

            SlaStatus.Breached => x =>
                x.Ticket.DueAtUtc != null
                && ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc)
                    || (x.Ticket.ResolvedAtUtc == null && nowUtc > x.Ticket.DueAtUtc)),

            SlaStatus.AtRisk => And(OpenAndNotPastDue(nowUtc), atRisk),

            SlaStatus.WithinSla => And(OpenAndNotPastDue(nowUtc), Not(atRisk)),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.")
        };
    }

    private static Expression<Func<TicketWithLabels, bool>> OpenAndNotPastDue(DateTime nowUtc) =>
        x => x.Ticket.DueAtUtc != null
             && x.Ticket.ResolvedAtUtc == null
             && x.Ticket.DueAtUtc >= nowUtc;

    private static Expression<Func<TicketWithLabels, bool>> AtRisk(DateTime nowUtc, SlaPolicy policy)
    {
        Expression<Func<TicketWithLabels, bool>>? any = null;

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            foreach (var tier in Enum.GetValues<CustomerTier>())
            {
                var latestDue = nowUtc + policy.AtRiskSpanFor(priority, tier);
                var p = priority;
                var t = tier;

                Expression<Func<TicketWithLabels, bool>> combination = x =>
                    x.Ticket.Priority == p && x.Customer.Tier == t && x.Ticket.DueAtUtc <= latestDue;

                any = any is null ? combination : Or(any, combination);
            }
        }

        return any!;
    }

    private static Expression<Func<TicketWithLabels, bool>> And(
        Expression<Func<TicketWithLabels, bool>> left,
        Expression<Func<TicketWithLabels, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    private static Expression<Func<TicketWithLabels, bool>> Or(
        Expression<Func<TicketWithLabels, bool>> left,
        Expression<Func<TicketWithLabels, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    private static Expression<Func<TicketWithLabels, bool>> Not(Expression<Func<TicketWithLabels, bool>> inner) =>
        Expression.Lambda<Func<TicketWithLabels, bool>>(Expression.Not(inner.Body), inner.Parameters);

    private static Expression<Func<TicketWithLabels, bool>> Combine(
        Expression<Func<TicketWithLabels, bool>> left,
        Expression<Func<TicketWithLabels, bool>> right,
        Func<Expression, Expression, BinaryExpression> join)
    {
        // Both conditions are written over their own parameter; point them at one.
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<TicketWithLabels, bool>>(join(left.Body, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
