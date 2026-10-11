import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { escalationResult, ticketDetail } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
  },
}));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);

async function fill(reason: string) {
  const user = userEvent.setup();

  if (reason) {
    await user.type(screen.getByLabelText('Reason'), reason);
  }

  await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('blocks a reason that is too short and never calls the API', async () => {
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fill('abc');

    expect(screen.getByText(/at least 5 characters/i)).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('treats a reason that is only spaces as too short', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill('        ');

    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('sends the trimmed values and reports the result', async () => {
    const result = escalationResult();
    escalateTicket.mockResolvedValue(result);
    const onEscalated = vi.fn();

    render(<EscalationPanel ticket={ticketDetail({ id: 12 })} onEscalated={onEscalated} />);

    await fill('  Customer is losing money  ');

    // Only the reason is sent: who is escalating comes from the signed-in agent's token.
    expect(escalateTicket).toHaveBeenCalledWith(12, { reason: 'Customer is losing money' });
    expect(onEscalated).toHaveBeenCalledWith(result);
  });

  it('shows a 409 from the server and leaves the ticket unchanged', async () => {
    escalateTicket.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
    );
    const onEscalated = vi.fn();

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fill('Customer is losing money');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ticket TCK-0012 is already Critical and cannot be escalated further.',
    );
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows the server field messages for a 400', async () => {
    escalateTicket.mockRejectedValue(
      new ApiError(400, 'One or more validation errors occurred.', {
        errors: { Reason: ['The reason must be at least 5 characters.'] },
      }),
    );

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill('Customer is losing money');

    expect(await screen.findByText('The reason must be at least 5 characters.')).toBeInTheDocument();
  });

  it('shows a pending state and disables the button while saving', async () => {
    escalateTicket.mockReturnValue(new Promise(() => undefined));

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill('Customer is losing money');

    const button = await screen.findByRole('button', { name: 'Escalating...' });
    expect(button).toBeDisabled();
  });

  it('does not offer escalation for a Critical ticket', () => {
    render(<EscalationPanel ticket={ticketDetail({ priority: 'Critical' })} onEscalated={vi.fn()} />);

    expect(screen.getByText(/already Critical/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });

  it.each(['Resolved', 'Closed'] as const)('does not offer escalation for a %s ticket', (status) => {
    render(<EscalationPanel ticket={ticketDetail({ status })} onEscalated={vi.fn()} />);

    expect(screen.getByText(new RegExp(`is ${status}`))).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });
});
