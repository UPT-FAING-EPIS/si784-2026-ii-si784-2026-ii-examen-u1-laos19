import { useEffect, useState } from 'react';
import { useApp } from '../components/AppContext.jsx';
import { Empty, SongRow } from '../components/ui.jsx';
import { AddToPlaylist } from './PlaylistsPage.jsx';

export default function ForYouPage() {
  const { api, user, nowPlaying, notify } = useApp();
  const [recs, setRecs] = useState([]);
  const [history, setHistory] = useState([]);
  const [adding, setAdding] = useState(null);

  useEffect(() => {
    Promise.all([api.recommendations(user.id), api.history(user.id)])
      .then(([r, h]) => {
        setRecs(r);
        setHistory(h);
      })
      .catch((err) => notify(err.message, 'error'));
  }, [api, user, nowPlaying, notify]);

  return (
    <section>
      <header className="page-head">
        <h1>Para ti</h1>
        <p className="muted">Recomendaciones basadas en lo que escuchas.</p>
      </header>
      <div className="two-col">
        <article className="card">
          <h2>Recomendadas</h2>
          {recs.length === 0 ? (
            <Empty>Escucha algunas canciones para personalizar tus recomendaciones.</Empty>
          ) : (
            <ul className="list">
              {recs.map((r) => (
                <SongRow key={r.song.id} song={r.song} onAdd={setAdding} extra={<small className="reason">{r.reason}</small>} />
              ))}
            </ul>
          )}
        </article>
        <article className="card">
          <h2>Historial de reproducción</h2>
          {history.length === 0 ? (
            <Empty>Aún no has reproducido canciones.</Empty>
          ) : (
            <ul className="list">
              {history.map((h) => (
                <SongRow
                  key={h.id}
                  song={h.song}
                  extra={<small className="reason">{new Date(h.playedAt).toLocaleString('es-PE')}</small>}
                />
              ))}
            </ul>
          )}
        </article>
      </div>
      {adding && <AddToPlaylist song={adding} onClose={() => setAdding(null)} />}
    </section>
  );
}
