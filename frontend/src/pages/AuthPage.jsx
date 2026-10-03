import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useApp } from '../components/AppContext.jsx';
import { Field } from '../components/ui.jsx';
import { collectErrors, validateEmail, validatePassword, validateRequired } from '../lib/validation.js';

export default function AuthPage() {
  const { api, login, user, notify } = useApp();
  const navigate = useNavigate();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ email: '', password: '', fullName: '' });
  const [errors, setErrors] = useState({});
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to="/" replace />;

  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    const rules = {
      email: () => validateEmail(form.email),
      password: () => (mode === 'register' ? validatePassword(form.password) : validateRequired(form.password, 'La contraseña')),
    };
    if (mode === 'register') rules.fullName = () => validateRequired(form.fullName, 'El nombre', { min: 3, max: 120 });
    const found = collectErrors(rules);
    setErrors(found);
    if (Object.keys(found).length) return;

    setBusy(true);
    try {
      const auth =
        mode === 'login'
          ? await api.login({ email: form.email.trim(), password: form.password })
          : await api.register({ email: form.email.trim(), password: form.password, fullName: form.fullName.trim() });
      login(auth);
      notify(`¡Bienvenido, ${auth.user.fullName}!`, 'success');
      navigate('/');
    } catch (err) {
      setErrors(err.errors || {});
      notify(err.message, 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="auth card">
      <h1>{mode === 'login' ? 'Iniciar sesión' : 'Crear cuenta'}</h1>
      <p className="muted">
        {mode === 'login' ? 'Accede para escuchar música y gestionar tu suscripción.' : 'Tu cuenta inicia con el plan Free.'}
      </p>
      <form onSubmit={submit} noValidate>
        {mode === 'register' && (
          <Field label="Nombre completo" error={errors.fullName}>
            <input value={form.fullName} onChange={set('fullName')} maxLength={120} autoComplete="name" />
          </Field>
        )}
        <Field label="Correo" error={errors.email}>
          <input type="email" value={form.email} onChange={set('email')} maxLength={150} autoComplete="email" />
        </Field>
        <Field
          label="Contraseña"
          error={errors.password}
          hint={mode === 'register' ? 'Mínimo 8 caracteres, con letras y números.' : undefined}
        >
          <input
            type="password"
            value={form.password}
            onChange={set('password')}
            maxLength={100}
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          />
        </Field>
        <button className="btn" disabled={busy}>
          {busy ? 'Procesando…' : mode === 'login' ? 'Entrar' : 'Registrarme'}
        </button>
      </form>
      <button
        className="link"
        onClick={() => {
          setMode(mode === 'login' ? 'register' : 'login');
          setErrors({});
        }}
      >
        {mode === 'login' ? '¿No tienes cuenta? Regístrate' : '¿Ya tienes cuenta? Inicia sesión'}
      </button>
    </section>
  );
}
