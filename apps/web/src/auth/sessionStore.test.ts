import { afterEach, describe, expect, it } from 'vitest';
import { clearSession, getSession, saveSession, subscribe, type Session } from './sessionStore';

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString();

const session = (overrides: Partial<Session> = {}): Session => ({
  token: 'signed-token',
  expiresAtUtc: inOneHour(),
  user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
  ...overrides,
});

describe('sessionStore', () => {
  afterEach(() => {
    clearSession();
    window.sessionStorage.clear();
  });

  it('keeps the session for the tab and reads it back', () => {
    saveSession(session());

    expect(getSession()?.user.fullName).toBe('Sara Lindqvist');
    expect(window.sessionStorage.getItem('supportdesk.session')).toContain('signed-token');
  });

  it('is gone after it is cleared', () => {
    saveSession(session());

    clearSession();

    expect(getSession()).toBeUndefined();
    expect(window.sessionStorage.getItem('supportdesk.session')).toBeNull();
  });

  it('treats an expired token as signed out and does not keep it', () => {
    saveSession(session({ expiresAtUtc: new Date(Date.now() - 1000).toISOString() }));

    expect(getSession()).toBeUndefined();
    expect(window.sessionStorage.getItem('supportdesk.session')).toBeNull();
  });

  it('treats damaged storage as signed out', () => {
    window.sessionStorage.setItem('supportdesk.session', '{not json');

    expect(getSession()).toBeUndefined();
  });

  it('tells subscribers when the session changes', () => {
    let calls = 0;
    const unsubscribe = subscribe(() => {
      calls += 1;
    });

    saveSession(session());
    clearSession();
    unsubscribe();
    saveSession(session());

    expect(calls).toBe(2);
  });

  it('returns the same object while nothing changed', () => {
    saveSession(session());

    expect(getSession()).toBe(getSession());
  });
});
