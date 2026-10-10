import type { TicketDetail } from '../../types/api';

/** The same limits the API enforces, so a request that would be rejected is never sent. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;
export const ESCALATED_BY_MAX_LENGTH = 100;

export interface EscalationInput {
  reason: string;
  escalatedBy: string;
}

export interface EscalationErrors {
  reason?: string;
  escalatedBy?: string;
}

/** Checks an escalation the way the server does: lengths are counted after trimming. */
export function validateEscalation({ reason, escalatedBy }: EscalationInput): EscalationErrors {
  const errors: EscalationErrors = {};
  const trimmedReason = reason.trim();
  const trimmedActor = escalatedBy.trim();

  if (trimmedReason.length < REASON_MIN_LENGTH) {
    errors.reason = `Give a reason of at least ${REASON_MIN_LENGTH} characters.`;
  } else if (trimmedReason.length > REASON_MAX_LENGTH) {
    errors.reason = `Keep the reason to ${REASON_MAX_LENGTH} characters or fewer.`;
  }

  if (trimmedActor.length === 0) {
    errors.escalatedBy = 'Say who is escalating this ticket.';
  } else if (trimmedActor.length > ESCALATED_BY_MAX_LENGTH) {
    errors.escalatedBy = `Keep this to ${ESCALATED_BY_MAX_LENGTH} characters or fewer.`;
  }

  return errors;
}

/**
 * Why a ticket cannot be escalated, or undefined when it can. The server makes the final
 * decision; this only stops the screen offering something that will be refused.
 */
export function escalationBlockedReason(
  ticket: Pick<TicketDetail, 'status' | 'priority'>,
): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `This ticket is ${ticket.status} and cannot be escalated.`;
  }

  if (ticket.priority === 'Critical') {
    return 'This ticket is already Critical and cannot be escalated further.';
  }

  return undefined;
}
