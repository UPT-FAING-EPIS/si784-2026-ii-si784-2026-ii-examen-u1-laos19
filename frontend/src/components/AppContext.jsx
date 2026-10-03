import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { createApi, loadSession, saveSession } from '../lib/api.js';

const AppContext = createContext(null);

export function AppProvider({ children }) {
  const [session, setSession] = useState(() => loadSession());
  const [nowPlaying, setNowPlaying] = useState(null);
  const [toast, setToast] = useState(null);

  const api = useMemo(() => createApi(session?.token), [session]);

  const notify = useCallback((message, kind = 'info') => {
    setToast({ message, kind, id: Date.now() });
    setTimeout(() => setToast((t) => (t && t.message === message ? null : t)), 4000);
  }, []);

  const login = useCallback((auth) => {
    const s = { token: auth.token, expiresAt: auth.expiresAt, user: auth.user };
    saveSession(s);
    setSession(s);
  }, []);

  const logout = useCallback(() => {
    saveSession(null);
    setSession(null);
    setNowPlaying(null);
  }, []);

  const play = useCallback(
    async (song) => {
      if (!session) {
        notify('Inicia sesión para reproducir música.', 'error');
        return;
      }
      try {
        const res = await api.play(song.id);
        setNowPlaying({ ...res, id: song.id });
      } catch (err) {
        notify(err.message, 'error');
      }
    },
    [api, session, notify],
  );

  const value = useMemo(
    () => ({
      session,
      user: session?.user ?? null,
      isAdmin: session?.user?.role === 'Admin',
      api,
      login,
      logout,
      nowPlaying,
      play,
      toast,
      notify,
    }),
    [session, api, login, logout, nowPlaying, play, toast, notify],
  );

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useApp() {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error('useApp debe usarse dentro de AppProvider');
  return ctx;
}
