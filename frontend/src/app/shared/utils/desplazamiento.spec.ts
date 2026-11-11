import { DESPLAZAMIENTO_OPCIONES, tipoDesplazamiento } from './desplazamiento';

describe('tipoDesplazamiento', () => {
  it('es vacío cuando empieza con 32 y mide 10 caracteres', () => {
    expect(tipoDesplazamiento('3212345678')).toBe('vacio');
  });

  it('ignora espacios alrededor', () => {
    expect(tipoDesplazamiento('  3212345678  ')).toBe('vacio');
  });

  it('es cargado si la longitud no es 10 o no empieza con 32', () => {
    expect(tipoDesplazamiento('321234567')).toBe('cargado');
    expect(tipoDesplazamiento('32123456789')).toBe('cargado');
    expect(tipoDesplazamiento('3012345678')).toBe('cargado');
  });

  it('trata nulos y vacíos como cargado', () => {
    expect(tipoDesplazamiento(null)).toBe('cargado');
    expect(tipoDesplazamiento(undefined)).toBe('cargado');
    expect(tipoDesplazamiento('')).toBe('cargado');
  });

  it('expone las opciones del filtro en el orden de la UI', () => {
    expect(DESPLAZAMIENTO_OPCIONES.map((o) => o.value)).toEqual(['cargado', 'vacio']);
  });
});
