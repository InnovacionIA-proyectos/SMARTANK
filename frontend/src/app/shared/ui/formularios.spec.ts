import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Badge } from './badge';
import { Button } from './button';
import { Input } from './input';
import { Modal } from './modal';
import { Select } from './select';

@Component({
  imports: [Input, Select],
  template: `
    <app-input label="Placa" [(value)]="placa" inputClass="w-40" [required]="true" />
    <app-input label="Correo" [(value)]="correo" error="Correo inválido" />
    <app-select
      label="Estado"
      [(value)]="estado"
      placeholder="Todos"
      [options]="opciones"
      selectClass="w-44"
    />
  `,
})
class HostFormulario {
  readonly placa = signal('');
  readonly correo = signal('x');
  readonly estado = signal('en_ruta');
  readonly opciones = [
    { value: 'programado', label: 'Programado' },
    { value: 'en_ruta', label: 'En ruta' },
  ];
}

describe('Input y Select', () => {
  async function montar() {
    const fixture = TestBed.createComponent(HostFormulario);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, raiz: fixture.nativeElement as HTMLElement, host: fixture.componentInstance };
  }

  it('asocia el label con el input y propaga lo escrito al modelo', async () => {
    const { fixture, raiz, host } = await montar();
    const input = raiz.querySelectorAll<HTMLInputElement>('input')[0];
    const label = raiz.querySelectorAll('label')[0];

    expect(label.textContent?.trim()).toBe('Placa');
    expect(label.getAttribute('for')).toBe(input.id);
    expect(input.required).toBe(true);
    expect(input.classList.contains('w-40')).toBe(true);

    input.value = 'SNU123';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(host.placa()).toBe('SNU123');
  });

  it('muestra el error y marca aria-invalid', async () => {
    const { raiz } = await montar();
    const input = raiz.querySelectorAll<HTMLInputElement>('input')[1];
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.classList.contains('border-danger')).toBe(true);
    expect(raiz.textContent).toContain('Correo inválido');
  });

  it('el select refleja el valor del modelo, incluye el placeholder y propaga el cambio', async () => {
    const { fixture, raiz, host } = await montar();
    const select = raiz.querySelector('select') as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.textContent?.trim())).toEqual([
      'Todos',
      'Programado',
      'En ruta',
    ]);
    expect(select.value).toBe('en_ruta');

    select.value = 'programado';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(host.estado()).toBe('programado');

    host.estado.set('');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(select.value).toBe('');
  });
});

@Component({
  imports: [Modal, Button, Badge],
  template: `
    <app-modal [open]="abierto()" title="Bitácora" (closed)="abierto.set(false)">
      <p class="contenido">Detalle</p>
      <div footer><button appButton variant="secondary" id="cerrar-pie">Cerrar</button></div>
    </app-modal>
    <app-badge variant="success" id="insignia">Sí</app-badge>
    <button appButton variant="success" size="lg" id="boton-exito">Autorizar</button>
  `,
})
class HostModal {
  readonly abierto = signal(false);
}

describe('Modal, Badge y Button', () => {
  async function montar() {
    const fixture = TestBed.createComponent(HostModal);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, raiz: fixture.nativeElement as HTMLElement, host: fixture.componentInstance };
  }

  it('el modal solo se renderiza abierto y expone rol dialog con su título', async () => {
    const { fixture, raiz, host } = await montar();
    expect(raiz.querySelector('[role="dialog"]')).toBeNull();

    host.abierto.set(true);
    fixture.detectChanges();
    await fixture.whenStable();
    const dialogo = raiz.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialogo.getAttribute('aria-modal')).toBe('true');
    expect(dialogo.getAttribute('aria-label')).toBe('Bitácora');
    expect(raiz.querySelector('.contenido')?.textContent).toBe('Detalle');
    expect(raiz.querySelector('#cerrar-pie')).not.toBeNull();
  });

  it('se cierra con Escape, con el botón de cierre y con clic en el fondo, no al hacer clic dentro', async () => {
    const { fixture, raiz, host } = await montar();
    const abrir = async () => {
      host.abierto.set(true);
      fixture.detectChanges();
      await fixture.whenStable();
    };

    await abrir();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(host.abierto()).toBe(false);

    await abrir();
    (raiz.querySelector('[aria-label="Cerrar"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(host.abierto()).toBe(false);

    await abrir();
    (raiz.querySelector('[role="dialog"]') as HTMLElement).click();
    fixture.detectChanges();
    expect(host.abierto()).toBe(true);

    (raiz.querySelector('.fixed') as HTMLElement).click();
    fixture.detectChanges();
    expect(host.abierto()).toBe(false);
  });

  it('badge y botón aplican las clases de su variante', async () => {
    const { raiz } = await montar();
    const insignia = raiz.querySelector('#insignia') as HTMLElement;
    expect(insignia.className).toContain('bg-success/15');
    expect(insignia.className).toContain('rounded-full');

    const boton = raiz.querySelector('#boton-exito') as HTMLElement;
    expect(boton.className).toContain('bg-success');
    expect(boton.className).toContain('h-12');
  });
});
