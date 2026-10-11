import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import { useSession } from '../../../auth/useSession';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  validateEscalation,
  type EscalationErrors,
} from '../escalationRules';

interface EscalationPanelProps {
  ticket: TicketDetail;
  /** Called with the updated ticket and the history entry once the server has accepted it. */
  onEscalated: (result: EscalationResult) => void;
}

/**
 * Raises a ticket one priority level, with a reason. Invalid input is stopped here and never
 * reaches the API, but the server's answer is still shown if it refuses (for example when
 * the ticket changed since the page loaded).
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const session = useSession();
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [serverError, setServerError] = useState<string>();

  const blockedReason = escalationBlockedReason(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setServerError(undefined);

    const validationErrors = validateEscalation(reason);
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
      });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 400) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setServerError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  if (blockedReason) {
    return (
      <div className="card">
        <h2>Escalate</h2>
        <p className="field__hint">{blockedReason}</p>
      </div>
    );
  }

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Escalate</h2>

      <p className="field__hint">
        Raises the priority one level, restarts the due date from now and re-checks the owner.
      </p>

      <Field
        id="escalate-reason"
        label="Reason"
        error={errors.reason}
        hint={`${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            value={reason}
            disabled={isSaving}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </Field>

      {session && (
        <p className="field__hint">Recorded as {session.user.fullName}.</p>
      )}

      {serverError && (
        <p className="field__error" role="alert">
          {serverError}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={isSaving}>
          {isSaving ? 'Escalating...' : 'Escalate ticket'}
        </button>
      </div>
    </form>
  );
}

/** Maps the API's per-field messages onto the two fields of this form. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const errors: EscalationErrors = {};

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);

    if (key === 'reason') {
      errors.reason = messages[0];
    }
  }

  return errors;
}
