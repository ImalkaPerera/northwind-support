import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { raisedTicket } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

describe('TriageResult', () => {
  it('shows the applied priority, the due date and the owner with their reasons', () => {
    render(<TriageResult ticket={raisedTicket()} onOpen={vi.fn()} onRaiseAnother={vi.fn()} />);

    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByText('The requested priority (High) was used.')).toBeInTheDocument();
    expect(screen.getByText('Due 8 hours after creation: High tickets get 8 hours.')).toBeInTheDocument();
  });

  it('says why a ticket is unassigned', () => {
    render(
      <TriageResult
        ticket={raisedTicket({
          assignedAgent: null,
          triage: {
            priorityReason: 'Set to Critical because the Security category always requires it.',
            slaWindowReason: 'Due 2 hours after creation: Critical tickets get 4 hours, shortened for a Premium customer.',
            assignmentReason: 'Left unassigned: no active agent has the Security specialization.',
          },
        })}
        onOpen={vi.fn()}
        onRaiseAnother={vi.fn()}
      />,
    );

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(
      screen.getByText('Left unassigned: no active agent has the Security specialization.'),
    ).toBeInTheDocument();
  });

  it('opens the ticket and lets the user raise another', async () => {
    const onOpen = vi.fn();
    const onRaiseAnother = vi.fn();
    const user = userEvent.setup();

    render(<TriageResult ticket={raisedTicket()} onOpen={onOpen} onRaiseAnother={onRaiseAnother} />);

    await user.click(screen.getByRole('button', { name: 'Open TCK-0012' }));
    await user.click(screen.getByRole('button', { name: 'Raise another' }));

    expect(onOpen).toHaveBeenCalledOnce();
    expect(onRaiseAnother).toHaveBeenCalledOnce();
  });
});
