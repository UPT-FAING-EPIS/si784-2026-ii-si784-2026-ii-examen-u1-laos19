import { useCallback, useEffect, useState } from 'react';
import { useApp } from '../components/AppContext.jsx';
import { Empty, Field, SongRow } from '../components/ui.jsx';
import { collectErrors, validateRequired } from '../lib/validation.js';

export function AddToPlaylist({ song, onClose }) {
  const { api, user, notify } = useApp();
  const [playlists, setPlaylists] = useState([]);

  useEffect(() => {
    api.playlists(user.id).then(setPlaylists).catch((err) => notify(err.message, 'error'));
  }, [api, user, notify]);

  const add = async (playlist) => {
    try {
      await api.addToPlaylist(playlist.id, song.id);
      notify(`“${song.title}” agregada a ${playlist.name}`, 'success');
      onClose();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <div className="modal" role="dialog" aria-modal="true">
      <div className="card modal-body small">
        <button type="button" className="icon-btn close" onClick={onClose} aria-label="Cerrar">✕</button>
        <h2>Agregar a playlist</h2>
        <p className="muted">{song.title}</p>
        {playlists.length === 0 ? (
          <Empty>Aún no tienes playlists. Créalas en “Mis playlists”.</Empty>
        ) : (
          <ul className="picker">
            {playlists.map((p) => (
              <li key={p.id}>
                <button type="button" className="btn ghost wide" onClick={() => add(p)}>
                  {p.name} <small>({p.songs.length})</small>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

export default function PlaylistsPage() {
  const { api, user, notify } = useApp();
  const [playlists, setPlaylists] = useState([]);
  const [form, setForm] = useState({ name: '', description: '', isPublic: false });
  const [errors, setErrors] = useState({});

  const load = useCallback(() => {
    api.playlists(user.id).then(setPlaylists).catch((err) => notify(err.message, 'error'));
  }, [api, user, notify]);

  useEffect(() => {
    load();
  }, [load]);

  const create = async (e) => {
    e.preventDefault();
    const found = collectErrors({
      name: () => validateRequired(form.name, 'El nombre', { max: 100 }),
      description: () => (form.description.length > 500 ? 'La descripción no debe superar 500 caracteres.' : null),
    });
    setErrors(found);
    if (Object.keys(found).length) return;
    try {
      await api.createPlaylist({ name: form.name.trim(), description: form.description.trim() || null, isPublic: form.isPublic });
      setForm({ name: '', description: '', isPublic: false });
      notify('Playlist creada', 'success');
      load();
    } catch (err) {
      setErrors(err.errors || {});
      notify(err.message, 'error');
    }
  };

  const remove = async (playlist, song) => {
    try {
      await api.removeFromPlaylist(playlist.id, song.id);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  const destroy = async (playlist) => {
    if (!globalThis.confirm(`¿Eliminar la playlist "${playlist.name}"?`)) return;
    try {
      await api.deletePlaylist(playlist.id);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <section>
      <header className="page-head">
        <h1>Mis playlists</h1>
      </header>
      <form className="card inline-form" onSubmit={create} noValidate>
        <Field label="Nombre" error={errors.name}>
          <input value={form.name} maxLength={100} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </Field>
        <Field label="Descripción" error={errors.description}>
          <input value={form.description} maxLength={500} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        <label className="check">
          <input type="checkbox" checked={form.isPublic} onChange={(e) => setForm({ ...form, isPublic: e.target.checked })} /> Pública
        </label>
        <button type="submit" className="btn">Crear playlist</button>
      </form>

      {playlists.length === 0 ? (
        <Empty>Aún no tienes playlists.</Empty>
      ) : (
        playlists.map((p) => (
          <article key={p.id} className="card playlist">
            <header>
              <div>
                <h2>{p.name}</h2>
                <span className="muted">
                  {p.description || 'Sin descripción'} · {p.songs.length} canciones {p.isPublic ? '· Pública' : ''}
                </span>
              </div>
              <button type="button" className="btn ghost danger" onClick={() => destroy(p)}>Eliminar</button>
            </header>
            {p.songs.length === 0 ? (
              <Empty>Agrega canciones desde el catálogo con ＋.</Empty>
            ) : (
              <ul className="list">
                {p.songs.map((s) => (
                  <SongRow key={s.id} song={s} onRemove={(song) => remove(p, song)} />
                ))}
              </ul>
            )}
          </article>
        ))
      )}
    </section>
  );
}
