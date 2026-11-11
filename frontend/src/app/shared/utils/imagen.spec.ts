import { dimensionesEscaladas } from './imagen';

describe('dimensionesEscaladas', () => {
  it('reduce respetando la proporción cuando el lado mayor excede el máximo', () => {
    expect(dimensionesEscaladas(4000, 3000, 1280)).toEqual({ ancho: 1280, alto: 960 });
    expect(dimensionesEscaladas(3000, 4000, 1280)).toEqual({ ancho: 960, alto: 1280 });
  });

  it('nunca amplía una imagen pequeña', () => {
    expect(dimensionesEscaladas(800, 600, 1280)).toEqual({ ancho: 800, alto: 600 });
  });
});
