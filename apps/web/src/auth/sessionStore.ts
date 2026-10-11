import type { CurrentUser } from '../types/api';

/**
 * The signed-in session: the token, when it expires, and who it is for.
 *
 * It is kept in sessionStorage. That means it is gone when the tab is closed and is not shared
 * with other tabs, which is safer than localStorage. The trade-off is that any script running
 * on the page can read it, so an XSS bug would expose it; an httpOnly cookie would avoid that
 * but needs server changes the assignment does not ask for.
 */
export interface Session {
  token: string;
  expiresAtUtc: string;
  user: CurrentUser;
}

const STORAGE_KEY = 'supportdesk.session';

type Listener = () => void;

const listeners = new Set<Listener>();

// getSnapshot must return the same object while nothing changed, so the parsed value is cached
// against the raw string it came from.
let cachedRaw: string | null | undefined;
let cachedSession: Session | undefined;

function readRaw(): string | null {
  try {
    return window.sessionStorage.getItem(STORAGE_KEY);
  } catch {
    // Storage can be unavailable (some private modes); the app then simply never stays signed in.
    return null;
  }
}

function parse(raw: string | null): Session | undefined {
  if (!raw) {
    return undefined;
  }

  try {
    const value = JSON.parse(raw) as Partial<Session>;

    if (typeof value.token === 'string' && typeof value.expiresAtUtc === 'string' && value.user) {
      return value as Session;
    }
  } catch {
    // A damaged value is treated as no session.
  }

  return undefined;
}

function notify() {
  listeners.forEach((listener) => listener());
}

/** The current session, or undefined when signed out or when the token has expired. */
export function getSession(): Session | undefined {
  const raw = readRaw();

  if (raw !== cachedRaw) {
    cachedRaw = raw;
    cachedSession = parse(raw);
  }

  if (cachedSession && new Date(cachedSession.expiresAtUtc).getTime() <= Date.now()) {
    // Expired on the client already: do not send a token the server will refuse.
    try {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to remove.
    }

    cachedRaw = null;
    cachedSession = undefined;
  }

  return cachedSession;
}

export function saveSession(session: Session): void {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // See readRaw.
  }

  notify();
}

export function clearSession(): void {
  try {
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // See readRaw.
  }

  notify();
}

/** For useSyncExternalStore: re-renders whatever reads the session when it changes. */
export function subscribe(listener: Listener): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}
