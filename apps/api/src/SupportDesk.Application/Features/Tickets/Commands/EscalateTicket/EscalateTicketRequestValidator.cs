using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Checks an escalation request before it reaches the handler, so a bad request is a 400 and
/// never gets as far as the domain. The limits come from the entity, which stores them.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(reason => reason is null || reason.Trim().Length >= TicketEscalation.MinReasonLength)
            .WithMessage($"The reason must be at least {TicketEscalation.MinReasonLength} characters.")
            .Must(reason => reason is null || reason.Trim().Length <= TicketEscalation.MaxReasonLength)
            .WithMessage($"The reason must be at most {TicketEscalation.MaxReasonLength} characters.");
    }
}
