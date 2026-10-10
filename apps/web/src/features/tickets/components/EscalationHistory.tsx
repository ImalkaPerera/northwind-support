import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations: TicketEscalation[] | undefined;
  isLoading: boolean;
  error: string | undefined;
  onRetry: () => void;
}

/** What each escalation changed, who asked for it, why, and when. Newest first. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {isLoading && !escalations && <Spinner label="Loading history" />}

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {escalations && escalations.length === 0 && (
        <EmptyState title="No escalations yet" description="Escalations are recorded here." />
      )}

      {escalations && escalations.length > 0 && (
        <ol className="history">
          {escalations.map((entry) => (
            <li key={entry.id} className="history__item">
              <p className="history__line">
                Priority <PriorityBadge priority={entry.fromPriority} /> to{' '}
                <PriorityBadge priority={entry.toPriority} />
              </p>

              <p className="history__line">
                Owner: {entry.fromAgent?.fullName ?? 'Unassigned'} to{' '}
                {entry.toAgent?.fullName ?? 'Unassigned'}
              </p>

              <p className="history__line">
                Due: {formatDateTime(entry.fromDueAtUtc)} to {formatDateTime(entry.toDueAtUtc)}
              </p>

              <p className="history__reason">{entry.reason}</p>

              <p className="history__meta">
                {entry.escalatedBy} on {formatDateTime(entry.escalatedAtUtc)}
              </p>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
