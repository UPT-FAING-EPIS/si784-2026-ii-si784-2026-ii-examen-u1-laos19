import { useCallback, useEffect, useState } from 'react';
import { useApp } from '../components/AppContext.jsx';
import { Empty, SongRow } from '../components/ui.jsx';
import { AddToPlaylist } from './PlaylistsPage.jsx';

const TYPES = [
  { value: '', label: 'Todo' },
  { value: 'song', label: 'Canciones' },
  { value: 'album', label: 'Álbumes' },
  { value: 'artist', label: 'Artistas' },
];

export default function CatalogPage() {
  const { api, user, notify } = useApp();
  const [filters, setFilters] = useState({ search: '', type: '', genre: '' });
  const [page, setPage] = useState(1);
  const [result, setResult] = useState({ items: [], total: 0, pageSize: 20 });
  const [genres, setGenres] = useState([]);
  const [detail, setDetail] = useState(null);
  const [adding, setAdding] = useState(null);
  const [searchError, setSearchError] = useState(null);

  const load = useCallback(async () => {
    try {
      setResult(await api.catalog({ ...filters, page, pageSize: 20 }));
    } catch (err) {
      notify(err.message, 'error');
    }
  }, [api, filters, page, notify]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    api.genres().then(setGenres).catch(() => setGenres([]));
  }, [api]);

  const onSearch = (e) => {
    const value = e.target.value;
    setSearchError(value.length > 100 ? 'La búsqueda no debe superar 100 caracteres.' : null);
    if (value.length <= 100) {
      setPage(1);
      setFilters({ ...filters, search: value });
    }
  };

  const open = async (item) => {
    try {
      setDetail(await api.detail(item.id));
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  const pages = Math.max(1, Math.ceil(result.total / result.pageSize));

  return (
    <section>
      <header className="page-head">
        <h1>Catálogo musical</h1>
        <p className="muted">Explora artistas, álbumes y canciones.</p>
      </header>

      <div className="filters">
        <div className="search">
          <input placeholder="Buscar por título o artista…" value={filters.search} onChange={onSearch} aria-label="Buscar" />
          {searchError && <small className="error-text">{searchError}</small>}
        </div>
        <div className="chips">
          {TYPES.map((t) => (
            <button
              key={t.value}
              className={`chip ${filters.type === t.value ? 'on' : ''}`}
              onClick={() => {
                setPage(1);
                setFilters({ ...filters, type: t.value });
              }}
            >
              {t.label}
            </button>
          ))}
        </div>
        <select
          value={filters.genre}
          aria-label="Género"
          onChange={(e) => {
            setPage(1);
            setFilters({ ...filters, genre: e.target.value });
          }}
        >
          <option value="">Todos los géneros</option>
          {genres.map((g) => (
            <option key={g} value={g}>{g}</option>
          ))}
        </select>
      </div>

      {result.items.length === 0 ? (
        <Empty>No hay resultados para tu búsqueda.</Empty>
      ) : (
        <ul className="grid">
          {result.items.map((item) => (
            <li key={item.id} className={`tile ${item.type}`}>
              {item.type === 'song' ? (
                <ul className="list compact">
                  <SongRow song={item} onAdd={user ? setAdding : undefined} />
                </ul>
              ) : (
                <button className="tile-body" onClick={() => open(item)}>
                  <span className="tile-type">{item.type === 'album' ? 'Álbum' : 'Artista'}</span>
                  <strong>{item.title}</strong>
                  <span className="muted">{item.subtitle}</span>
                  <span className="genre">{item.genre}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {pages > 1 && (
        <div className="pager">
          <button className="btn ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</button>
          <span>Página {page} de {pages}</span>
          <button className="btn ghost" disabled={page >= pages} onClick={() => setPage(page + 1)}>Siguiente</button>
        </div>
      )}

      {detail && (
        <div className="modal" role="dialog" aria-modal="true">
          <div className="card modal-body">
            <button className="icon-btn close" onClick={() => setDetail(null)} aria-label="Cerrar">✕</button>
            {detail.type === 'album' && (
              <>
                <span className="tile-type">Álbum · {detail.album.releaseYear}</span>
                <h2>{detail.album.title}</h2>
                <p className="muted">{detail.album.artistName}</p>
                <ul className="list">
                  {detail.album.songs.map((s) => (
                    <SongRow key={s.id} song={s} onAdd={user ? setAdding : undefined} />
                  ))}
                </ul>
              </>
            )}
            {detail.type === 'artist' && (
              <>
                <span className="tile-type">Artista · {detail.artist.genre}</span>
                <h2>{detail.artist.name}</h2>
                <p className="muted">{detail.artist.bio}</p>
                <h3>Álbumes</h3>
                <ul className="inline">
                  {detail.artist.albums.map((a) => (
                    <li key={a.id}>{a.title} ({a.releaseYear})</li>
                  ))}
                </ul>
                <h3>Canciones</h3>
                <ul className="list">
                  {detail.artist.topSongs.map((s) => (
                    <SongRow key={s.id} song={s} onAdd={user ? setAdding : undefined} />
                  ))}
                </ul>
              </>
            )}
          </div>
        </div>
      )}

      {adding && <AddToPlaylist song={adding} onClose={() => setAdding(null)} />}
    </section>
  );
}
