// Validaciones compartidas del frontend. Reflejan las reglas del backend (DataAnnotations).

function isEmail(v) {
  // Validación sin expresiones regulares con backtracking (evita ReDoS).
  if (/\s/.test(v)) return false;
  const parts = v.split('@');
  if (parts.length !== 2) return false;
  const [local, domain] = parts;
  if (!local || !domain) return false;
  const dot = domain.lastIndexOf('.');
  return dot > 0 && dot < domain.length - 1 && !domain.startsWith('.') && !domain.includes('..');
}

export function validateEmail(value) {
  const v = (value ?? '').trim();
  if (!v) return 'El correo es obligatorio.';
  if (v.length > 150) return 'El correo no debe superar 150 caracteres.';
  if (!isEmail(v)) return 'Ingrese un correo válido.';
  return null;
}

export function validatePassword(value) {
  const v = value ?? '';
  if (!v) return 'La contraseña es obligatoria.';
  if (v.length < 8) return 'La contraseña debe tener al menos 8 caracteres.';
  if (v.length > 100) return 'La contraseña no debe superar 100 caracteres.';
  if (!/[A-Za-z]/.test(v) || !/\d/.test(v)) return 'La contraseña debe contener letras y números.';
  return null;
}

export function validateRequired(value, label, { min = 1, max = 200 } = {}) {
  const v = (value ?? '').toString().trim();
  if (!v) return `${label} es obligatorio.`;
  if (v.length < min) return `${label} debe tener al menos ${min} caracteres.`;
  if (v.length > max) return `${label} no debe superar ${max} caracteres.`;
  return null;
}

export function validateRange(value, label, min, max) {
  const n = Number(value);
  if (value === '' || value === null || value === undefined || Number.isNaN(n)) return `${label} debe ser numérico.`;
  if (n < min || n > max) return `${label} debe estar entre ${min} y ${max}.`;
  return null;
}

export function validateHttpsUrl(value, label, { required = false } = {}) {
  const v = (value ?? '').trim();
  if (!v) return required ? `${label} es obligatoria.` : null;
  try {
    const url = new URL(v);
    if (url.protocol !== 'https:') return `${label} debe usar HTTPS.`;
  } catch {
    return `${label} no es una URL válida.`;
  }
  return null;
}

export function validateDateRange(from, to) {
  if (!from || !to) return 'Ambas fechas son obligatorias.';
  const f = new Date(from);
  const t = new Date(to);
  if (Number.isNaN(f.getTime()) || Number.isNaN(t.getTime())) return 'Fechas inválidas.';
  if (f > t) return 'La fecha inicial debe ser anterior a la final.';
  if ((t - f) / 86400000 > 366) return 'El periodo máximo es de un año.';
  return null;
}

/** Ejecuta un mapa { campo: () => error|null } y devuelve sólo los errores. */
export function collectErrors(rules) {
  const errors = {};
  for (const [field, rule] of Object.entries(rules)) {
    const msg = rule();
    if (msg) errors[field] = msg;
  }
  return errors;
}

export function formatDuration(seconds) {
  const s = Math.max(0, Math.floor(Number(seconds) || 0));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

export function formatMoney(amount) {
  return `S/ ${Number(amount || 0).toFixed(2)}`;
}

export function formatDate(value) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('es-PE', { year: 'numeric', month: 'short', day: 'numeric' });
}
