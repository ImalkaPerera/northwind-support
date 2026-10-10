import { formatDateTime } from '../../../lib/format';
import type { RaisedTicket } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface TriageResultProps {
  ticket: RaisedTicket;
  onOpen: () => void;
  onRaiseAnother: () => void;
}

/** What the system decided for a ticket that was just raised, and why. */
export function TriageResult({ ticket, onOpen, onRaiseAnother }: TriageResultProps) {
  const { triage } = ticket;

  return (
    <div className="card notice" role="status">
      <p>
        Raised <strong>{ticket.reference}</strong> for {ticket.customer.name}.
      </p>

      <dl className="triage">
        <div>
          <dt>Priority</dt>
          <dd>
            <PriorityBadge priority={ticket.priority} />
            <span className="summary__note">{triage.priorityReason}</span>
          </dd>
        </div>

        <div>
          <dt>Due</dt>
          <dd>
            {formatDateTime(ticket.dueAtUtc)}
            <span className="summary__note">{triage.slaWindowReason}</span>
          </dd>
        </div>

        <div>
          <dt>Assigned to</dt>
          <dd>
            {ticket.assignedAgent?.fullName ?? 'Unassigned'}
            <span className="summary__note">{triage.assignmentReason}</span>
          </dd>
        </div>
      </dl>

      <div className="button-row">
        <button type="button" className="button button--primary" onClick={onOpen}>
          Open {ticket.reference}
        </button>
        <button type="button" className="button button--ghost" onClick={onRaiseAnother}>
          Raise another
        </button>
      </div>
    </div>
  );
}
