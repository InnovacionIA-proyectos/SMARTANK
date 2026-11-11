import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { Rol } from '../core/api/api-types';
import { AuthService } from '../core/auth/auth.service';
import { SidebarNav } from './sidebar-nav';

@Component({ template: '' })
class Vacio {}

@Component({ imports: [SidebarNav], template: '<app-sidebar-nav [colapsado]="colapsado" />' })
class Host {
  colapsado = false;
}

describe('SidebarNav', () => {
  async function montar(rol: Rol, colapsado = false) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Vacio }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    auth.login({ email: 'u@smartank.co', password: 'x' }).subscribe();
    http
      .expectOne('/api/auth/login')
      .flush({ usuario: { id: '1', email: 'u@smartank.co', nombre: null, rol } });

    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.colapsado = colapsado;
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const etiquetas = (raiz: HTMLElement) =>
    Array.from(raiz.querySelectorAll('a')).map((a) => a.textContent?.trim());

  it('el administrador ve todas las opciones en el orden del original', async () => {
    const raiz = await montar('admin');
    expect(etiquetas(raiz)).toEqual([
      'Panel de Despacho',
      'Recomendación de Tanqueo',
      'Seguimiento Control Combustible',
      'Bandeja de Alertas',
      'Recuperación de Margen',
      'Historial de Tanqueos',
    ]);
  });

  it('despacho no ve alertas ni tablero', async () => {
    const raiz = await montar('despacho');
    expect(etiquetas(raiz)).toEqual([
      'Panel de Despacho',
      'Recomendación de Tanqueo',
      'Seguimiento Control Combustible',
      'Historial de Tanqueos',
    ]);
  });

  it('control de flota ve alertas pero no el tablero; finanzas ve el tablero pero no alertas', async () => {
    expect(etiquetas(await montar('control_flota'))).toContain('Bandeja de Alertas');
    TestBed.resetTestingModule();
    const raiz = await montar('finanzas');
    expect(etiquetas(raiz)).toContain('Recuperación de Margen');
    expect(etiquetas(raiz)).not.toContain('Bandeja de Alertas');
  });

  it('colapsado muestra solo íconos con la etiqueta como title', async () => {
    const raiz = await montar('admin', true);
    const enlace = raiz.querySelector('a') as HTMLAnchorElement;
    expect(enlace.textContent?.trim()).toBe('');
    expect(enlace.getAttribute('title')).toBe('Panel de Despacho');
    expect(enlace.querySelector('svg')).not.toBeNull();
  });
});
