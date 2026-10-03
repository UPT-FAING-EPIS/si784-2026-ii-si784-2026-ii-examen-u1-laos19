import { NavLink, useNavigate } from 'react-router-dom';
import { useApp } from './AppContext.jsx';

export function Layout({ children }) {
  const { user, isAdmin, logout, nowPlaying, toast } = useApp();
  const navigate = useNavigate();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <img src="/favicon.svg" alt="" width="28" height="28" />
          <span>MusicStream</span>
        </div>
        <nav>
          <NavLink to="/" end>Catálogo</NavLink>
          {user && <NavLink to="/para-ti">Para ti</NavLink>}
          {user && <NavLink to="/playlists">Mis playlists</NavLink>}
          {user && <NavLink to="/suscripcion">Mi suscripción</NavLink>}
          {isAdmin && <NavLink to="/admin">Administración</NavLink>}
        </nav>
        <div className="account">
          {user ? (
            <>
              <span className="who">{user.fullName}</span>
              <small>{user.email}</small>
              <button type="button"
                className="btn ghost"
                onClick={() => {
                  logout();
                  navigate('/');
                }}
              >
                Cerrar sesión
              </button>
            </>
          ) : (
            <NavLink className="btn" to="/login">
              Iniciar sesión
            </NavLink>
          )}
        </div>
      </aside>
      <main className="content">{children}</main>
      <footer className="player">
        {nowPlaying ? (
          <>
            <div className="np">
              <strong>{nowPlaying.title}</strong>
              <span>{nowPlaying.artistName}</span>
            </div>
            <audio key={nowPlaying.streamUrl} src={nowPlaying.streamUrl} controls autoPlay preload="none">
              <track kind="captions" />
            </audio>
          </>
        ) : (
          <span className="muted">Selecciona una canción para reproducir</span>
        )}
      </footer>
      {toast && (
        <div className={`toast ${toast.kind}`} role="status">
          {toast.message}
        </div>
      )}
    </div>
  );
}
