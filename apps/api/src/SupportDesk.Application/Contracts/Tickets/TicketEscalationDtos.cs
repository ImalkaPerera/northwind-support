using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Why triage decided what it did, in words a team lead can read.</summary>
public sealed record TicketTriageDto(string PriorityReason, string SlaWindowReason, string AssignmentReason);

/// <summary>The newly raised ticket together with the decision that was applied to it.</summary>
public sealed class RaisedTicketDto : TicketDetailDto
{
    public RaisedTicketDto(TicketDetailDto ticket, TicketTriageDto triage)
        : base(ticket)
    {
        Triage = triage;
    }

    public TicketTriageDto Triage { get; }
}

/// <summary>
/// Body of POST /api/tickets/{id}/escalate. Who is escalating is never sent: it is the agent the
/// token belongs to.
/// </summary>
public sealed record EscalateTicketRequest(string Reason);

/// <summary>One entry of a ticket's escalation history.</summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>The updated ticket and the history entry that recorded the escalation.</summary>
public sealed record EscalationResultDto(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation,
    TicketTriageDto Decision);
