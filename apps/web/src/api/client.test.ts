import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession } from '../auth/sessionStore';
import { ApiError, request } from './client';

const fetchMock = vi.fn();

function lastHeaders(): Record<string, string> {
  const [, init] = fetchMock.mock.calls[fetchMock.mock.calls.length - 1] as [string, RequestInit];

  return init.headers as Record<string, string>;
}

const signIn = () =>
  saveSession({
    token: 'signed-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.test' },
  });

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
    clearSession();
  });

  it('attaches the token to a call when signed in', async () => {
    signIn();
    fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));

    await request('/tickets');

    expect(lastHeaders().Authorization).toBe('Bearer signed-token');
  });

  it('sends no Authorization header when signed out', async () => {
    fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));

    await request('/auth/login', { method: 'POST', body: '{}' });

    expect(lastHeaders().Authorization).toBeUndefined();
  });

  it('clears the session when the server answers 401', async () => {
    signIn();
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', status: 401 }), { status: 401 }),
    );

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getSession()).toBeUndefined();
  });

  it('keeps the session for other failures', async () => {
    signIn();
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Conflict', status: 409 }), { status: 409 }),
    );

    await expect(request('/tickets/1/escalate', { method: 'POST' })).rejects.toBeInstanceOf(ApiError);

    expect(getSession()?.token).toBe('signed-token');
  });
});
