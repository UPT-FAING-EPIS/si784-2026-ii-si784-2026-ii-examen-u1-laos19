import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, buildQuery, createApi, loadSession, parseError, request, saveSession } from './api.js';

function jsonResponse(status, body) {
  return { ok: status < 400, status, json: async () => body };
}

afterEach(() => {
  vi.restoreAllMocks();
  sessionStorage.clear();
});

describe('api', () => {
  it('construye query strings omitiendo vacíos', () => {
    expect(buildQuery({ a: 1, b: '', c: null, d: 'x y' })).toBe('?a=1&d=x+y');
    expect(buildQuery({})).toBe('');
  });

  it('guarda y recupera la sesión vigente', () => {
    const future = new Date(Date.now() + 60000).toISOString();
    saveSession({ token: 't', expiresAt: future, user: { id: '1' } });
    expect(loadSession().token).toBe('t');
    saveSession({ token: 't', expiresAt: new Date(Date.now() - 1000).toISOString() });
    expect(loadSession()).toBeNull();
    saveSession(null);
    expect(loadSession()).toBeNull();
    sessionStorage.setItem('musicstream.session', '{bad json');
    expect(loadSession()).toBeNull();
  });

  it('traduce errores de validación', async () => {
    const err = await parseError(jsonResponse(400, { title: 'Bad', errors: { Email: ['inválido'] } }));
    expect(err).toBeInstanceOf(ApiError);
    expect(err.errors.email).toBe('inválido');
    expect((await parseError({ status: 401, json: async () => { throw new Error('x'); } })).message).toMatch(/sesión/);
    expect((await parseError(jsonResponse(403, {}))).message).toMatch(/permisos/);
    expect((await parseError(jsonResponse(429, {}))).message).toMatch(/intentos/);
  });

  it('envía token y cuerpo JSON', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));
    vi.stubGlobal('fetch', fetchMock);
    const res = await request('/x', { method: 'POST', body: { a: 1 }, token: 'abc' });
    expect(res).toEqual({ ok: true });
    const [url, opts] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/x');
    expect(opts.headers.Authorization).toBe('Bearer abc');
    expect(opts.body).toBe('{"a":1}');
  });

  it('maneja 204 y errores', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce({ ok: true, status: 204 }).mockResolvedValueOnce(jsonResponse(404, { title: 'No' })));
    const api = createApi('tok');
    expect(await api.deletePlaylist('1')).toBeNull();
    await expect(api.detail('2')).rejects.toThrow('No');
  });

  it('expone todos los endpoints', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, {}));
    vi.stubGlobal('fetch', fetchMock);
    const api = createApi('tok');
    await Promise.all([
      api.register({}), api.login({}), api.catalog({ search: 'a' }), api.genres(), api.play('1'),
      api.plans(true), api.createPlan({}), api.updatePlan('1', {}), api.deactivatePlan('1'),
      api.subscription('u'), api.subscribe({}), api.cancelSubscription('s'), api.allSubscriptions('Active'),
      api.playlists('u'), api.createPlaylist({}), api.addToPlaylist('p', 's'), api.removeFromPlaylist('p', 's'),
      api.history('u'), api.recommendations('u'), api.users('x'), api.updateUser('u', {}),
      api.reports(), api.generateReport({}), api.createArtist({}), api.createSong({}), api.deleteCatalogItem('1'),
    ]);
    expect(fetchMock).toHaveBeenCalledTimes(26);
  });
});
