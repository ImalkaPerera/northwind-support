using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// A ticket with the rows that label it. The list is filtered and sorted on this shape and
/// projected afterwards, so a filter can look at the customer's tier or the agent without a
/// second query.
/// </summary>
internal sealed class TicketWithLabels
{
    public required Ticket Ticket { get; init; }

    public required Customer Customer { get; init; }

    public required Category Category { get; init; }

    public Agent? Agent { get; init; }
}
