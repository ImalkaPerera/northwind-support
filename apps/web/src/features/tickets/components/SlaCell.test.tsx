import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

const statuses: { status: SlaStatus; text: string }[] = [
  { status: 'WithinSla', text: 'Within SLA' },
  { status: 'AtRisk', text: 'At risk' },
  { status: 'Breached', text: 'Breached' },
  { status: 'Met', text: 'Met' },
  { status: 'NotApplicable', text: 'No SLA' },
];

describe('SlaCell', () => {
  it.each(statuses)('says "$text" for $status', ({ status, text }) => {
    render(<SlaCell dueAtUtc="2026-09-24T16:12:00Z" slaStatus={status} />);

    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('renders every state in a way that can be told apart without colour', () => {
    const rendered = statuses.map(({ status }) => {
      const { container, unmount } = render(<SlaCell dueAtUtc={null} slaStatus={status} />);
      const badge = container.querySelector('.badge');
      const result = {
        text: badge?.textContent ?? '',
        tone: badge?.className ?? '',
        symbol: badge?.querySelector('[aria-hidden="true"]')?.textContent?.trim() ?? '',
      };
      unmount();

      return result;
    });

    // Each state has its own words and its own symbol, so colour is never the only signal.
    expect(new Set(rendered.map((r) => r.text)).size).toBe(statuses.length);
    expect(new Set(rendered.map((r) => r.symbol)).size).toBe(statuses.length);
    expect(rendered.every((r) => r.symbol.length > 0)).toBe(true);
  });

  it('hides the decorative symbol from screen readers', () => {
    const { container } = render(<SlaCell dueAtUtc={null} slaStatus="AtRisk" />);

    expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull();
  });
});
