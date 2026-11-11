import { formatFecha } from './fecha';

describe('formatFecha', () => {
  it('formatea una fecha local como aaaa/MM/dd HH:mm', () => {
    expect(formatFecha(new Date(2026, 5, 17, 9, 5))).toBe('2026/06/17 09:05');
  });

  it('acepta epoch en milisegundos', () => {
    const d = new Date(2026, 0, 2, 23, 59);
    expect(formatFecha(d.getTime())).toBe('2026/01/02 23:59');
  });

  it('acepta cadenas ISO con hora local explícita', () => {
    expect(formatFecha('2026-03-04T07:08:00')).toBe('2026/03/04 07:08');
  });

  it('devuelve guion largo para nulos, vacíos y fechas inválidas', () => {
    expect(formatFecha(null)).toBe('—');
    expect(formatFecha(undefined)).toBe('—');
    expect(formatFecha('')).toBe('—');
    expect(formatFecha('no es una fecha')).toBe('—');
  });
});
