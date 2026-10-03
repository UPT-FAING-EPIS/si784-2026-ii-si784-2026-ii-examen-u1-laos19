// Cliente HTTP mínimo para la API de MusicStream.

const BASE_URL = (import.meta.env.VITE_API_URL || '/api').replace(/\/$/, '');
const TOKEN_KEY = 'musicstream.session';

export class ApiError extends Error {
  constructor(status, message, errors) {
    super(message);
    this.status = status;
    this.errors = errors || {};
  }
}

export function loadSession() {
  try {
    const raw = sessionStorage.getItem(TOKEN_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw);
    if (!session?.token || new Date(session.expiresAt) <= new Date()) return null;
    return session;
  } catch {
    return null;
  }
}

export function saveSession(session) {
  try {
    if (session) sessionStorage.setItem(TOKEN_KEY, JSON.stringify(session));
    else sessionStorage.removeItem(TOKEN_KEY);
  } catch {
    /* almacenamiento no disponible: la sesión sólo vive en memoria */
  }
}

export function buildQuery(params) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params || {})) {
    if (v !== undefined && v !== null && v !== '') qs.append(k, String(v));
  }
  const s = qs.toString();
  return s ? `?${s}` : '';
}

export async function parseError(response) {
  let body = null;
  try {
    body = await response.json();
  } catch {
    body = null;
  }
  const errors = {};
  if (body?.errors) {
    for (const [k, v] of Object.entries(body.errors)) {
      const key = k.charAt(0).toLowerCase() + k.slice(1);
      errors[key] = Array.isArray(v) ? v.join(' ') : String(v);
    }
  }
  let message = body?.title || `Error ${response.status}`;
  if (response.status === 401 && !body?.title) message = 'Debe iniciar sesión.';
  if (response.status === 403 && !body?.title) message = 'No tiene permisos para esta acción.';
  if (response.status === 429) message = 'Demasiados intentos, espere un momento.';
  return new ApiError(response.status, message, errors);
}

export async function request(path, { method = 'GET', body, token, query } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  const response = await fetch(BASE_URL + path + buildQuery(query), {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) throw await parseError(response);
  if (response.status === 204) return null;
  return response.json();
}

export function createApi(token) {
  const call = (path, opts) => request(path, { ...opts, token });
  return {
    register: (data) => request('/auth/register', { method: 'POST', body: data }),
    login: (data) => request('/auth/login', { method: 'POST', body: data }),
    catalog: (params) => call('/music', { query: params }),
    genres: () => call('/music/genres'),
    detail: (id) => call(`/music/${id}`),
    play: (id) => call(`/music/${id}/play`, { method: 'POST' }),
    plans: (includeInactive) => call('/plans', { query: { includeInactive } }),
    createPlan: (data) => call('/plans', { method: 'POST', body: data }),
    updatePlan: (id, data) => call(`/plans/${id}`, { method: 'PUT', body: data }),
    deactivatePlan: (id) => call(`/plans/${id}`, { method: 'DELETE' }),
    subscription: (userId) => call(`/subscriptions/${userId}`),
    subscribe: (data) => call('/subscriptions', { method: 'POST', body: data }),
    cancelSubscription: (id) => call(`/subscriptions/${id}`, { method: 'DELETE' }),
    allSubscriptions: (status) => call('/subscriptions', { query: { status } }),
    playlists: (userId) => call(`/playlists/${userId}`),
    createPlaylist: (data) => call('/playlists', { method: 'POST', body: data }),
    addToPlaylist: (id, songId) => call(`/playlists/${id}/songs`, { method: 'POST', body: { songId } }),
    removeFromPlaylist: (id, songId) => call(`/playlists/${id}/songs/${songId}`, { method: 'DELETE' }),
    deletePlaylist: (id) => call(`/playlists/${id}`, { method: 'DELETE' }),
    history: (userId) => call(`/history/${userId}`),
    recommendations: (userId) => call(`/recommendations/${userId}`),
    users: (search) => call('/users', { query: { search } }),
    updateUser: (id, data) => call(`/users/${id}`, { method: 'PATCH', body: data }),
    reports: () => call('/reports'),
    generateReport: (data) => call('/reports', { method: 'POST', body: data }),
    createArtist: (data) => call('/music/artists', { method: 'POST', body: data }),
    createSong: (data) => call('/music/songs', { method: 'POST', body: data }),
    deleteCatalogItem: (id) => call(`/music/${id}`, { method: 'DELETE' }),
  };
}
