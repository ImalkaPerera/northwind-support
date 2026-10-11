import { useSyncExternalStore } from 'react';
import { getSession, subscribe, type Session } from './sessionStore';

/** The signed-in session, kept up to date: it becomes undefined the moment it is cleared. */
export function useSession(): Session | undefined {
  return useSyncExternalStore(subscribe, getSession);
}
