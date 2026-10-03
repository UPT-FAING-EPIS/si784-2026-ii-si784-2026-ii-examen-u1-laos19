import { formatDuration } from '../lib/validation.js';
import { useApp } from './AppContext.jsx';

export function Field({ label, error, children, hint }) {
  return (
    <label className={`field ${error ? 'has-error' : ''}`}>
      <span className="field-label">{label}</span>
      {children}
      {hint && !error && <small className="hint">{hint}</small>}
      {error && <small className="error-text" role="alert">{error}</small>}
    </label>
  );
}

export function Empty({ children }) {
  return <p className="empty">{children}</p>;
}

export function SongRow({ song, onAdd, onRemove, extra }) {
  const { play, nowPlaying } = useApp();
  const active = nowPlaying?.id === song.id;
  return (
    <li className={`song-row ${active ? 'active' : ''}`}>
      <button type="button" className="icon-btn play" onClick={() => play(song)} aria-label={`Reproducir ${song.title}`}>
        {active ? '♪' : '▶'}
      </button>
      <div className="song-meta">
        <strong>{song.title}</strong>
        <span>
          {song.artistName ?? song.subtitle}
          {song.albumTitle ? ` · ${song.albumTitle}` : ''}
        </span>
      </div>
      {song.isPremium && <span className="badge premium">Premium</span>}
      {extra}
      <span className="duration">{formatDuration(song.durationSeconds)}</span>
      {onAdd && (
        <button type="button" className="icon-btn" onClick={() => onAdd(song)} aria-label="Agregar a playlist" title="Agregar a playlist">
          ＋
        </button>
      )}
      {onRemove && (
        <button type="button" className="icon-btn danger" onClick={() => onRemove(song)} aria-label="Quitar" title="Quitar">
          ✕
        </button>
      )}
    </li>
  );
}
