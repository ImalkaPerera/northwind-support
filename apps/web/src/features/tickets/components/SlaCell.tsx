import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

interface SlaLabel {
  text: string;
  tone: BadgeTone;
  /** A symbol as well as a colour, so the state is not carried by colour alone. */
  symbol: string;
}

const slaLabels: Record<SlaStatus, SlaLabel> = {
  WithinSla: { text: 'Within SLA', tone: 'info', symbol: '●' },
  AtRisk: { text: 'At risk', tone: 'warning', symbol: '▲' },
  Breached: { text: 'Breached', tone: 'danger', symbol: '✕' },
  Met: { text: 'Met', tone: 'success', symbol: '✓' },
  NotApplicable: { text: 'No SLA', tone: 'neutral', symbol: '–' },
};

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/**
 * The one SLA indicator: the due date and where the ticket stands against it. Used by the
 * ticket list and the ticket detail page.
 */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  const label = slaLabels[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={label.tone}>
        <span aria-hidden="true">{label.symbol} </span>
        {label.text}
      </Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
