import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  const CLAVE = 'smartank-theme';

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  it('arranca en claro si no hay preferencia guardada ni del sistema', () => {
    const tema = TestBed.inject(ThemeService);
    TestBed.tick();
    expect(tema.tema()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('respeta la preferencia guardada con la clave smartank-theme', () => {
    localStorage.setItem(CLAVE, 'dark');
    const tema = TestBed.inject(ThemeService);
    TestBed.tick();
    expect(tema.tema()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('alternar cambia el tema, aplica la clase .dark y persiste la elección', () => {
    const tema = TestBed.inject(ThemeService);
    TestBed.tick();

    tema.alternar();
    TestBed.tick();
    expect(tema.tema()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem(CLAVE)).toBe('dark');

    tema.alternar();
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(localStorage.getItem(CLAVE)).toBe('light');
  });

  it('ignora valores guardados inválidos', () => {
    localStorage.setItem(CLAVE, 'azul');
    const tema = TestBed.inject(ThemeService);
    expect(['light', 'dark']).toContain(tema.tema());
  });
});
