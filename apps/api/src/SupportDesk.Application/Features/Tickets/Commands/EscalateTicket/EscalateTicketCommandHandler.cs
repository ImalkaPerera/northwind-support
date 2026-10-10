using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Raises a ticket's priority one level, recalculates its due date from now, re-checks its
/// owner, and records who asked and why.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="SupportDesk.Domain.Exceptions.BusinessRuleViolationException">
    /// The ticket is Resolved, Closed or already Critical.
    /// </exception>
    public async Task<EscalationResultDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Rejected before anything else is looked up.
        ticket.EnsureCanBeEscalated();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var allAgents = await agents.GetAllAsync(ct);

        var decision = triage.ForEscalation(
            new EscalationTriageInput(ticket, category.Name, category.RequiresSpecialist, customer.Tier, now),
            allAgents.Select(a => a.ToCandidate()));

        var entry = ticket.Escalate(request.Reason, request.EscalatedBy, decision.DueAtUtc, decision.Agent?.Id, now);

        await tickets.AddEscalationAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {From} to {To} by {EscalatedBy}.",
            ticket.Reference, entry.FromPriority, entry.ToPriority, entry.EscalatedBy);

        AgentSummaryDto? Summary(int? agentId) =>
            allAgents.Where(a => a.Id == agentId).Select(a => new AgentSummaryDto(a.Id, a.FullName)).FirstOrDefault();

        var escalation = new TicketEscalationDto(
            entry.Id,
            entry.TicketId,
            entry.FromPriority,
            entry.ToPriority,
            Summary(entry.FromAgentId),
            Summary(entry.ToAgentId),
            entry.FromDueAtUtc,
            entry.ToDueAtUtc,
            entry.Reason,
            entry.EscalatedBy,
            entry.EscalatedAtUtc);

        return new EscalationResultDto(await getTicket.HandleAsync(id, ct), escalation, decision.Reasons);
    }
}
