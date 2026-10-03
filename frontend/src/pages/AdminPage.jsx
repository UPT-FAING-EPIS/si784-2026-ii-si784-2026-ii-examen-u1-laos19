import { useCallback, useEffect, useState } from 'react';
import { useApp } from '../components/AppContext.jsx';
import { Empty, Field } from '../components/ui.jsx';
import {
  collectErrors,
  formatDate,
  formatMoney,
  validateDateRange,
  validateHttpsUrl,
  validateRange,
  validateRequired,
} from '../lib/validation.js';

const TABS = ['Planes', 'Usuarios', 'Suscripciones', 'Catálogo', 'Reportes'];

export default function AdminPage() {
  const [tab, setTab] = useState(TABS[0]);
  return (
    <section>
      <header className="page-head">
        <h1>Panel de administración</h1>
      </header>
      <div className="chips tabs">
        {TABS.map((t) => (
          <button key={t} className={`chip ${tab === t ? 'on' : ''}`} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </div>
      {tab === 'Planes' && <PlansAdmin />}
      {tab === 'Usuarios' && <UsersAdmin />}
      {tab === 'Suscripciones' && <SubscriptionsAdmin />}
      {tab === 'Catálogo' && <CatalogAdmin />}
      {tab === 'Reportes' && <ReportsAdmin />}
    </section>
  );
}

const EMPTY_PLAN = { name: '', description: '', price: '0', durationDays: '30', maxPlaylists: '10', allowsPremiumContent: false, isActive: true };

function PlansAdmin() {
  const { api, notify } = useApp();
  const [plans, setPlans] = useState([]);
  const [form, setForm] = useState(EMPTY_PLAN);
  const [editing, setEditing] = useState(null);
  const [errors, setErrors] = useState({});

  const load = useCallback(() => api.plans(true).then(setPlans).catch((e) => notify(e.message, 'error')), [api, notify]);
  useEffect(() => {
    load();
  }, [load]);

  const save = async (e) => {
    e.preventDefault();
    const found = collectErrors({
      name: () => validateRequired(form.name, 'El nombre', { min: 2, max: 80 }),
      price: () => validateRange(form.price, 'El precio', 0, 10000),
      durationDays: () => validateRange(form.durationDays, 'La duración', 1, 3650),
      maxPlaylists: () => validateRange(form.maxPlaylists, 'Máx. playlists', 1, 1000),
    });
    setErrors(found);
    if (Object.keys(found).length) return;
    const body = {
      ...form,
      price: Number(form.price),
      durationDays: Number(form.durationDays),
      maxPlaylists: Number(form.maxPlaylists),
    };
    try {
      if (editing) await api.updatePlan(editing, body);
      else await api.createPlan(body);
      notify('Plan guardado', 'success');
      setForm(EMPTY_PLAN);
      setEditing(null);
      load();
    } catch (err) {
      setErrors(err.errors || {});
      notify(err.message, 'error');
    }
  };

  const edit = (p) => {
    setEditing(p.id);
    setForm({ ...p, price: String(p.price), durationDays: String(p.durationDays), maxPlaylists: String(p.maxPlaylists) });
  };

  const deactivate = async (p) => {
    try {
      await api.deactivatePlan(p.id);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  const set = (k) => (e) => setForm({ ...form, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value });

  return (
    <div className="two-col">
      <form className="card" onSubmit={save} noValidate>
        <h2>{editing ? 'Editar plan' : 'Nuevo plan'}</h2>
        <Field label="Nombre" error={errors.name}><input value={form.name} onChange={set('name')} maxLength={80} /></Field>
        <Field label="Descripción" error={errors.description}><input value={form.description} onChange={set('description')} maxLength={500} /></Field>
        <div className="row">
          <Field label="Precio (S/)" error={errors.price}><input type="number" min="0" step="0.01" value={form.price} onChange={set('price')} /></Field>
          <Field label="Duración (días)" error={errors.durationDays}><input type="number" min="1" value={form.durationDays} onChange={set('durationDays')} /></Field>
          <Field label="Máx. playlists" error={errors.maxPlaylists}><input type="number" min="1" value={form.maxPlaylists} onChange={set('maxPlaylists')} /></Field>
        </div>
        <label className="check"><input type="checkbox" checked={form.allowsPremiumContent} onChange={set('allowsPremiumContent')} /> Contenido premium</label>
        <label className="check"><input type="checkbox" checked={form.isActive} onChange={set('isActive')} /> Activo</label>
        <div className="row">
          <button className="btn">Guardar</button>
          {editing && <button type="button" className="btn ghost" onClick={() => { setEditing(null); setForm(EMPTY_PLAN); }}>Cancelar</button>}
        </div>
      </form>
      <div className="card">
        <h2>Planes</h2>
        <table>
          <thead><tr><th>Nombre</th><th>Precio</th><th>Días</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            {plans.map((p) => (
              <tr key={p.id}>
                <td>{p.name}</td>
                <td>{formatMoney(p.price)}</td>
                <td>{p.durationDays}</td>
                <td>{p.isActive ? 'Activo' : 'Inactivo'}</td>
                <td className="actions">
                  <button className="link" onClick={() => edit(p)}>Editar</button>
                  {p.isActive && !p.isFree && <button className="link danger" onClick={() => deactivate(p)}>Desactivar</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function UsersAdmin() {
  const { api, user, notify } = useApp();
  const [users, setUsers] = useState([]);
  const [search, setSearch] = useState('');

  const load = useCallback(() => api.users(search).then(setUsers).catch((e) => notify(e.message, 'error')), [api, search, notify]);
  useEffect(() => {
    load();
  }, [load]);

  const update = async (u, data) => {
    try {
      await api.updateUser(u.id, data);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <div className="card">
      <input placeholder="Buscar usuario…" value={search} maxLength={100} onChange={(e) => setSearch(e.target.value)} aria-label="Buscar usuario" />
      <table>
        <thead><tr><th>Usuario</th><th>Rol</th><th>Estado</th><th>Registro</th><th></th></tr></thead>
        <tbody>
          {users.map((u) => (
            <tr key={u.id}>
              <td>{u.fullName}<br /><small className="muted">{u.email}</small></td>
              <td>{u.role}</td>
              <td>{u.isActive ? 'Activo' : 'Deshabilitado'}</td>
              <td>{formatDate(u.createdAt)}</td>
              <td className="actions">
                {u.id !== user.id && (
                  <>
                    <button className="link" onClick={() => update(u, { isActive: !u.isActive })}>{u.isActive ? 'Deshabilitar' : 'Habilitar'}</button>
                    <button className="link" onClick={() => update(u, { role: u.role === 'Admin' ? 'User' : 'Admin' })}>
                      {u.role === 'Admin' ? 'Quitar admin' : 'Hacer admin'}
                    </button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SubscriptionsAdmin() {
  const { api, notify } = useApp();
  const [status, setStatus] = useState('');
  const [subs, setSubs] = useState([]);

  const load = useCallback(() => api.allSubscriptions(status).then(setSubs).catch((e) => notify(e.message, 'error')), [api, status, notify]);
  useEffect(() => {
    load();
  }, [load]);

  const cancel = async (s) => {
    try {
      await api.cancelSubscription(s.id);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <div className="card">
      <select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Estado">
        <option value="">Todos los estados</option>
        <option value="Active">Activas</option>
        <option value="Cancelled">Canceladas</option>
        <option value="Expired">Expiradas</option>
      </select>
      {subs.length === 0 ? <Empty>Sin suscripciones.</Empty> : (
        <table>
          <thead><tr><th>Usuario</th><th>Plan</th><th>Inicio</th><th>Fin</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            {subs.map((s) => (
              <tr key={s.id}>
                <td>{s.userEmail}</td>
                <td>{s.planName}</td>
                <td>{formatDate(s.startDate)}</td>
                <td>{formatDate(s.endDate)}</td>
                <td>{s.status}</td>
                <td>{s.status === 'Active' && <button className="link danger" onClick={() => cancel(s)}>Cancelar</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function CatalogAdmin() {
  const { api, notify } = useApp();
  const [artists, setArtists] = useState([]);
  const [artist, setArtist] = useState({ name: '', genre: '' });
  const [song, setSong] = useState({ title: '', artistId: '', genre: '', durationSeconds: '180', audioUrl: '', isPremium: false });
  const [errors, setErrors] = useState({});

  const load = useCallback(() => api.catalog({ type: 'artist', pageSize: 100 }).then((r) => setArtists(r.items)).catch((e) => notify(e.message, 'error')), [api, notify]);
  useEffect(() => {
    load();
  }, [load]);

  const saveArtist = async (e) => {
    e.preventDefault();
    const found = collectErrors({
      artistName: () => validateRequired(artist.name, 'El nombre', { max: 120 }),
      artistGenre: () => validateRequired(artist.genre, 'El género', { min: 2, max: 60 }),
    });
    setErrors(found);
    if (Object.keys(found).length) return;
    try {
      await api.createArtist({ name: artist.name.trim(), genre: artist.genre.trim() });
      setArtist({ name: '', genre: '' });
      notify('Artista creado', 'success');
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  const saveSong = async (e) => {
    e.preventDefault();
    const found = collectErrors({
      title: () => validateRequired(song.title, 'El título', { max: 150 }),
      artistId: () => (song.artistId ? null : 'Seleccione un artista.'),
      genre: () => validateRequired(song.genre, 'El género', { min: 2, max: 60 }),
      durationSeconds: () => validateRange(song.durationSeconds, 'La duración', 1, 3600),
      audioUrl: () => validateHttpsUrl(song.audioUrl, 'La URL de audio', { required: true }),
    });
    setErrors(found);
    if (Object.keys(found).length) return;
    try {
      await api.createSong({ ...song, durationSeconds: Number(song.durationSeconds), title: song.title.trim(), audioUrl: song.audioUrl.trim() });
      setSong({ ...song, title: '', audioUrl: '' });
      notify('Canción creada', 'success');
    } catch (err) {
      setErrors(err.errors || {});
      notify(err.message, 'error');
    }
  };

  const removeArtist = async (a) => {
    if (!globalThis.confirm(`¿Eliminar a ${a.title} y todo su contenido?`)) return;
    try {
      await api.deleteCatalogItem(a.id);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <div className="two-col">
      <div>
        <form className="card" onSubmit={saveArtist} noValidate>
          <h2>Nuevo artista</h2>
          <Field label="Nombre" error={errors.artistName}><input value={artist.name} maxLength={120} onChange={(e) => setArtist({ ...artist, name: e.target.value })} /></Field>
          <Field label="Género" error={errors.artistGenre}><input value={artist.genre} maxLength={60} onChange={(e) => setArtist({ ...artist, genre: e.target.value })} /></Field>
          <button className="btn">Crear artista</button>
        </form>
        <div className="card">
          <h2>Artistas</h2>
          <ul className="list">
            {artists.map((a) => (
              <li key={a.id} className="song-row">
                <div className="song-meta"><strong>{a.title}</strong><span>{a.genre}</span></div>
                <button className="icon-btn danger" onClick={() => removeArtist(a)} aria-label="Eliminar">✕</button>
              </li>
            ))}
          </ul>
        </div>
      </div>
      <form className="card" onSubmit={saveSong} noValidate>
        <h2>Nueva canción</h2>
        <Field label="Título" error={errors.title}><input value={song.title} maxLength={150} onChange={(e) => setSong({ ...song, title: e.target.value })} /></Field>
        <Field label="Artista" error={errors.artistId}>
          <select value={song.artistId} onChange={(e) => setSong({ ...song, artistId: e.target.value })}>
            <option value="">Seleccione…</option>
            {artists.map((a) => <option key={a.id} value={a.id}>{a.title}</option>)}
          </select>
        </Field>
        <div className="row">
          <Field label="Género" error={errors.genre}><input value={song.genre} maxLength={60} onChange={(e) => setSong({ ...song, genre: e.target.value })} /></Field>
          <Field label="Duración (s)" error={errors.durationSeconds}><input type="number" min="1" max="3600" value={song.durationSeconds} onChange={(e) => setSong({ ...song, durationSeconds: e.target.value })} /></Field>
        </div>
        <Field label="URL de audio (HTTPS)" error={errors.audioUrl}><input value={song.audioUrl} maxLength={500} onChange={(e) => setSong({ ...song, audioUrl: e.target.value })} /></Field>
        <label className="check"><input type="checkbox" checked={song.isPremium} onChange={(e) => setSong({ ...song, isPremium: e.target.checked })} /> Premium</label>
        <button className="btn">Crear canción</button>
      </form>
    </div>
  );
}

function toInputDate(d) {
  return d.toISOString().slice(0, 10);
}

function ReportsAdmin() {
  const { api, notify } = useApp();
  const today = new Date();
  const [form, setForm] = useState({ type: 'Usage', from: toInputDate(new Date(today.getTime() - 30 * 86400000)), to: toInputDate(today) });
  const [error, setError] = useState(null);
  const [reports, setReports] = useState([]);
  const [selected, setSelected] = useState(null);

  const load = useCallback(() => api.reports().then(setReports).catch((e) => notify(e.message, 'error')), [api, notify]);
  useEffect(() => {
    load();
  }, [load]);

  const generate = async (e) => {
    e.preventDefault();
    const msg = validateDateRange(form.from, form.to);
    setError(msg);
    if (msg) return;
    try {
      const report = await api.generateReport({
        type: form.type,
        from: new Date(`${form.from}T00:00:00Z`).toISOString(),
        to: new Date(`${form.to}T23:59:59Z`).toISOString(),
      });
      setSelected(report);
      load();
    } catch (err) {
      notify(err.message, 'error');
    }
  };

  return (
    <div className="two-col">
      <div>
        <form className="card" onSubmit={generate} noValidate>
          <h2>Generar reporte</h2>
          <Field label="Tipo">
            <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
              <option value="Usage">Uso (reproducciones)</option>
              <option value="Subscriptions">Suscripciones</option>
              <option value="Revenue">Ingresos</option>
            </select>
          </Field>
          <div className="row">
            <Field label="Desde" error={error}><input type="date" value={form.from} onChange={(e) => setForm({ ...form, from: e.target.value })} /></Field>
            <Field label="Hasta"><input type="date" value={form.to} onChange={(e) => setForm({ ...form, to: e.target.value })} /></Field>
          </div>
          <button className="btn">Generar</button>
        </form>
        <div className="card">
          <h2>Reportes generados</h2>
          {reports.length === 0 ? <Empty>Sin reportes.</Empty> : (
            <ul className="picker">
              {reports.map((r) => (
                <li key={r.id}>
                  <button className="btn ghost wide" onClick={() => setSelected(r)}>
                    {r.type} · {formatDate(r.periodStart)} – {formatDate(r.periodEnd)}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
      <div className="card">
        <h2>Resultado</h2>
        {selected ? <ReportView report={selected} /> : <Empty>Genera o selecciona un reporte.</Empty>}
      </div>
    </div>
  );
}

function ReportView({ report }) {
  const r = report.result;
  return (
    <div className="report">
      <p className="muted">{report.type} · generado {new Date(report.generatedAt).toLocaleString('es-PE')}</p>
      <div className="stats">
        {Object.entries(r)
          .filter(([, v]) => typeof v === 'number')
          .map(([k, v]) => (
            <div key={k} className="stat">
              <span>{k}</span>
              <strong>{k.toLowerCase().includes('revenue') ? formatMoney(v) : v}</strong>
            </div>
          ))}
      </div>
      {Object.entries(r)
        .filter(([, v]) => Array.isArray(v))
        .map(([k, rows]) => (
          <div key={k}>
            <h3>{k}</h3>
            {rows.length === 0 ? <Empty>Sin datos.</Empty> : (
              <table>
                <thead><tr>{Object.keys(rows[0]).filter((c) => !c.endsWith('Id')).map((c) => <th key={c}>{c}</th>)}</tr></thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={JSON.stringify(row)}>
                      {Object.entries(row).filter(([c]) => !c.endsWith('Id')).map(([c, v]) => <td key={c}>{String(v)}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        ))}
    </div>
  );
}
