import { construirParams } from './params';

describe('construirParams', () => {
  it('omite undefined, null y cadenas vacías', () => {
    const p = construirParams({ a: 'x', b: undefined, c: null, d: '', e: 0, f: false });
    expect(p.keys().sort()).toEqual(['a', 'e', 'f']);
    expect(p.get('e')).toBe('0');
    expect(p.get('f')).toBe('false');
  });

  it('sin filtros devuelve parámetros vacíos', () => {
    expect(construirParams().keys()).toEqual([]);
    expect(construirParams({}).toString()).toBe('');
  });
});
