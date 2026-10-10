import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { escalation } from '../../../test/fixtures';
import { EscalationHistory } from './EscalationHistory';

describe('EscalationHistory', () => {
  it('shows what changed, who did it and why', () => {
    render(
      <EscalationHistory
        escalations={[escalation()]}
        isLoading={false}
        error={undefined}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByText('Customer is losing money every hour')).toBeInTheDocument();
    expect(screen.getByText(/Owner: Sara Lindqvist to Alex Turner/)).toBeInTheDocument();
    expect(screen.getByText(/Alex Turner on/)).toBeInTheDocument();
  });

  it('shows Unassigned where a ticket had no owner', () => {
    render(
      <EscalationHistory
        escalations={[escalation({ fromAgent: null, toAgent: null })]}
        isLoading={false}
        error={undefined}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByText(/Owner: Unassigned to Unassigned/)).toBeInTheDocument();
  });

  it('says when there is no history', () => {
    render(<EscalationHistory escalations={[]} isLoading={false} error={undefined} onRetry={vi.fn()} />);

    expect(screen.getByText('No escalations yet')).toBeInTheDocument();
  });

  it('shows an error with a retry', () => {
    render(
      <EscalationHistory
        escalations={undefined}
        isLoading={false}
        error="Unable to load the escalation history."
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load the escalation history.');
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
