import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { authApi } from '../api/auth';
import { routes } from '../routes';
import { clearSession, getSession, saveSession } from './sessionStore';

vi.mock('../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    me: vi.fn(),
  },
}));

// The pages behind the guard load data; these tests only care about getting to them.
vi.mock('../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
    getTicket: vi.fn().mockReturnValue(new Promise(() => undefined)),
    getEscalations: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock('../api/agents', () => ({ agentsApi: { getAgents: vi.fn().mockResolvedValue([]) } }));
vi.mock('../api/categories', () => ({ categoriesApi: { getCategories: vi.fn().mockResolvedValue([]) } }));
vi.mock('../api/customers', () => ({ customersApi: { getCustomers: vi.fn().mockResolvedValue([]) } }));

const login = vi.mocked(authApi.login);

const signedIn = () =>
  saveSession({
    token: 'signed-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
  });

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });

  render(<RouterProvider router={router} />);

  return router;
}

async function fillAndSubmit(email: string, password: string) {
  const user = userEvent.setup();

  if (email) {
    await user.type(screen.getByLabelText('Email'), email);
  }

  if (password) {
    await user.type(screen.getByLabelText('Password'), password);
  }

  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('protected routes', () => {
  afterEach(() => {
    clearSession();
    vi.clearAllMocks();
  });

  it('sends a visitor with no session to the login page', async () => {
    const router = renderAt('/tickets');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('returns the visitor to the page they wanted after signing in', async () => {
    login.mockResolvedValue({
      token: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });

    const router = renderAt('/tickets/12?tab=history');
    await screen.findByRole('heading', { name: 'Sign in' });

    await fillAndSubmit('sara@example.test', 'LocalDev-Password-1');

    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets/12'));
    expect(router.state.location.search).toBe('?tab=history');
  });

  it('goes to the ticket list when there was no page to return to', async () => {
    login.mockResolvedValue({
      token: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });

    const router = renderAt('/login');

    await fillAndSubmit('sara@example.test', 'LocalDev-Password-1');

    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets'));
  });

  it('shows the signed-in name, and signing out clears the session and leaves the page', async () => {
    signedIn();
    const user = userEvent.setup();
    const router = renderAt('/tickets');

    expect(await screen.findByText('Sara Lindqvist')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(getSession()).toBeUndefined();
  });

  it('goes back to login when the session is cleared, as a 401 does', async () => {
    signedIn();
    const router = renderAt('/tickets');
    await screen.findByText('Sara Lindqvist');

    clearSession();

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });
});

describe('login page', () => {
  afterEach(() => {
    clearSession();
    vi.clearAllMocks();
  });

  it('does not call the API for an empty or malformed form', async () => {
    renderAt('/login');

    await fillAndSubmit('not-an-email', '');

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('shows a clear error and keeps the user on the page when the credentials are rejected', async () => {
    login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    const router = renderAt('/login');

    await fillAndSubmit('sara@example.test', 'wrong-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is not correct.');
    expect(router.state.location.pathname).toBe('/login');
    expect(getSession()).toBeUndefined();
  });

  it('shows a pending state and disables the button while signing in', async () => {
    login.mockReturnValue(new Promise(() => undefined));
    renderAt('/login');

    await fillAndSubmit('sara@example.test', 'LocalDev-Password-1');

    expect(await screen.findByRole('button', { name: 'Signing in...' })).toBeDisabled();
  });

  it('has labelled fields', () => {
    renderAt('/login');

    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email');
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('keeps a signed-in visitor away from the login page', async () => {
    signedIn();
    const router = renderAt('/login');

    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets'));
  });
});
