import { describe, expect, it } from 'vitest';
import {
  collectErrors,
  formatDate,
  formatDuration,
  formatMoney,
  validateDateRange,
  validateEmail,
  validateHttpsUrl,
  validatePassword,
  validateRange,
  validateRequired,
} from './validation.js';

describe('validation', () => {
  it('valida correos', () => {
    expect(validateEmail('')).toMatch(/obligatorio/);
    expect(validateEmail('ana@')).toMatch(/válido/);
    expect(validateEmail('ana@dominio')).toMatch(/válido/);
    expect(validateEmail('a b@x.pe')).toMatch(/válido/);
    expect(validateEmail('a@@x.pe')).toMatch(/válido/);
    expect(validateEmail(`${'a'.repeat(150)}@x.pe`)).toMatch(/150/);
    expect(validateEmail('ana@upt.pe')).toBeNull();
  });

  it('valida contraseñas', () => {
    expect(validatePassword('')).toMatch(/obligatoria/);
    expect(validatePassword('abc1')).toMatch(/8/);
    expect(validatePassword('abcdefgh')).toMatch(/letras y números/);
    expect(validatePassword('x'.repeat(101) + '1')).toMatch(/100/);
    expect(validatePassword('segura123')).toBeNull();
  });

  it('valida requeridos y rangos', () => {
    expect(validateRequired('  ', 'Nombre')).toMatch(/obligatorio/);
    expect(validateRequired('ab', 'Nombre', { min: 3 })).toMatch(/al menos 3/);
    expect(validateRequired('abcd', 'Nombre', { max: 3 })).toMatch(/superar 3/);
    expect(validateRequired('abc', 'Nombre')).toBeNull();
    expect(validateRange('', 'Precio', 0, 10)).toMatch(/numérico/);
    expect(validateRange('11', 'Precio', 0, 10)).toMatch(/entre/);
    expect(validateRange('5', 'Precio', 0, 10)).toBeNull();
  });

  it('valida URLs HTTPS', () => {
    expect(validateHttpsUrl('', 'URL')).toBeNull();
    expect(validateHttpsUrl('', 'URL', { required: true })).toMatch(/obligatoria/);
    expect(validateHttpsUrl('no-url', 'URL')).toMatch(/válida/);
    expect(validateHttpsUrl('ftp://x.pe/a.mp3', 'URL')).toMatch(/HTTPS/);
    expect(validateHttpsUrl('https://x.pe/a.mp3', 'URL')).toBeNull();
  });

  it('valida rangos de fechas', () => {
    expect(validateDateRange('', '2026-01-01')).toMatch(/obligatorias/);
    expect(validateDateRange('x', 'y')).toMatch(/inválidas/);
    expect(validateDateRange('2026-02-01', '2026-01-01')).toMatch(/anterior/);
    expect(validateDateRange('2024-01-01', '2026-01-01')).toMatch(/un año/);
    expect(validateDateRange('2026-01-01', '2026-02-01')).toBeNull();
  });

  it('agrupa errores y formatea', () => {
    expect(collectErrors({ a: () => 'x', b: () => null })).toEqual({ a: 'x' });
    expect(formatDuration(185)).toBe('3:05');
    expect(formatDuration('x')).toBe('0:00');
    expect(formatMoney(19.9)).toBe('S/ 19.90');
    expect(formatDate(null)).toBe('—');
    expect(formatDate('invalid')).toBe('—');
    expect(formatDate('2026-01-15T00:00:00Z')).not.toBe('—');
  });
});
