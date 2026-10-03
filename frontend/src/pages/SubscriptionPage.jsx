import { useCallback, useEffect, useState } from 'react';
import { useApp } from '../components/AppContext.jsx';
import { formatDate, formatMoney } from '../lib/validation.js';

export default function SubscriptionPage() {
  const { api, user, notify } = useApp();
  const [current, setCurrent] = useState(null);
  const [plans, setPlans] = useState([]);
  const [autoRenew, setAutoRenew] = useState(true);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setPlans(await api.plans());
      setCurrent(await api.subscription(user.id));
    } catch (err) {
      if (err.status === 404) setCurrent(null);
      else notify(err.message, 'error');
    }
  }, [api, user, notify]);

  useEffect(() => {
    load();
  }, [load]);

  const subscribe = async (plan) => {
    const renewing = current?.planId === plan.id;
    const verb = renewing ? 'Renovar' : 'Suscribirte a';
    if (!globalThis.confirm(`${verb} ${plan.name} por ${formatMoney(plan.price)}?`)) return;
    setBusy(true);
    try {
      await api.subscribe({ planId: plan.id, autoRenew });
      notify(renewing ? 'Suscripción renovada' : `Ahora tienes ${plan.name}`, 'success');
      await load();
    } catch (err) {
      notify(err.message, 'error');
    } finally {
      setBusy(false);
    }
  };

  const cancel = async () => {
    if (!current || !globalThis.confirm('¿Cancelar tu suscripción actual?')) return;
    setBusy(true);
    try {
      await api.cancelSubscription(current.id);
      notify('Suscripción cancelada', 'success');
      await load();
    } catch (err) {
      notify(err.message, 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section>
      <header className="page-head">
        <h1>Mi suscripción</h1>
      </header>
      <article className="card current">
        {current ? (
          <>
            <div>
              <span className="tile-type">Plan actual</span>
              <h2>{current.planName}</h2>
              <p className="muted">
                Vigente del {formatDate(current.startDate)} al {formatDate(current.endDate)} ·{' '}
                {current.autoRenew ? 'Renovación automática' : 'Sin renovación automática'}
              </p>
            </div>
            <button className="btn ghost danger" onClick={cancel} disabled={busy}>Cancelar suscripción</button>
          </>
        ) : (
          <p>No tienes una suscripción activa. Elige un plan para continuar.</p>
        )}
      </article>

      <label className="check">
        <input type="checkbox" checked={autoRenew} onChange={(e) => setAutoRenew(e.target.checked)} /> Renovación automática
      </label>

      <div className="plans">
        {plans.map((p) => {
          const isCurrent = current?.planId === p.id;
          return (
            <article key={p.id} className={`card plan ${isCurrent ? 'selected' : ''}`}>
              <h3>{p.name}</h3>
              <p className="price">
                {p.isFree ? 'Gratis' : formatMoney(p.price)} <small>/ {p.durationDays} días</small>
              </p>
              <p className="muted">{p.description}</p>
              <ul className="features">
                <li>{p.allowsPremiumContent ? '✓ Contenido premium' : '✕ Sin contenido premium'}</li>
                <li>✓ Hasta {p.maxPlaylists} playlists</li>
              </ul>
              <button className="btn wide" disabled={busy} onClick={() => subscribe(p)}>
                {isCurrent ? 'Renovar' : 'Elegir plan'}
              </button>
            </article>
          );
        })}
      </div>
    </section>
  );
}
