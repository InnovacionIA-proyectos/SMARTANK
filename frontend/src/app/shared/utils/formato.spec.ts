import { MESES, MESES_OPCIONES, cap, cop, copExacto, copMoneda, entero, plata } from './formato';

describe('formato', () => {
  it('cap pone la primera letra en mayúscula', () => {
    expect(cap('terpel')).toBe('Terpel');
    expect(cap('')).toBe('');
  });

  it('cop redondea y usa separador de miles es-CO', () => {
    expect(cop(1234567.6)).toBe('$1.234.568');
    expect(cop(0)).toBe('$0');
  });

  it('copExacto conserva el valor tal cual', () => {
    expect(copExacto(75000)).toBe('$75.000');
  });

  it('plata abrevia montos desde un millón', () => {
    expect(plata(1_043_000_000)).toBe('$1.043 mill.');
    expect(plata(2_500_000)).toBe('$2,5 mill.');
    expect(plata(-3_000_000)).toBe('$-3 mill.');
    expect(plata(950_000)).toBe('$950.000');
  });

  it('copMoneda usa la moneda COP sin decimales', () => {
    const texto = copMoneda(12500);
    expect(texto).toContain('12.500');
    expect(texto).not.toContain(',');
  });

  it('entero formatea o devuelve guion si no hay valor', () => {
    expect(entero(null)).toBe('—');
    expect(entero(undefined)).toBe('—');
    expect(entero(1234567.891)).toBe('1.234.568');
    expect(entero(1234567.891, 1)).toBe('1.234.567,9');
  });

  it('MESES y MESES_OPCIONES cubren enero a diciembre', () => {
    expect(MESES[1]).toBe('Enero');
    expect(MESES[12]).toBe('Diciembre');
    expect(MESES_OPCIONES).toHaveLength(12);
    expect(MESES_OPCIONES[0]).toEqual({ value: '1', label: 'Enero' });
  });
});
