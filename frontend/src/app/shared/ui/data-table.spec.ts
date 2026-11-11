import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ColumnDef } from '@tanstack/angular-table';
import { CeldaDef, DataTable } from './data-table';

interface Fila {
  placa: string;
  galones: number | null;
  estado: 'nueva' | 'gestionada';
}

@Component({
  imports: [DataTable, CeldaDef],
  template: `
    <app-data-table
      [columns]="columnas"
      [data]="filas()"
      [rowFlagged]="marcada"
      emptyMessage="Sin filas de prueba."
    >
      <ng-template appCelda="estado" [appCeldaDe]="filas()" let-fila>
        <span class="estado-personalizado">{{ fila.estado.toUpperCase() }}</span>
      </ng-template>
    </app-data-table>
  `,
})
class HostTabla {
  readonly filas = signal<Fila[]>([
    { placa: 'BBB222', galones: 20, estado: 'gestionada' },
    { placa: 'AAA111', galones: 5, estado: 'nueva' },
    { placa: 'CCC333', galones: null, estado: 'nueva' },
  ]);

  readonly columnas: ColumnDef<Fila, unknown>[] = [
    { accessorKey: 'placa', header: 'Placa' },
    {
      accessorKey: 'galones',
      header: 'Galones',
      cell: ({ row }) => (row.original.galones != null ? `${row.original.galones} gal` : '—'),
    },
    { accessorKey: 'estado', header: 'Estado' },
    { id: 'acciones', header: '' },
  ];

  readonly marcada = (f: Fila) => f.estado === 'nueva';
}

describe('DataTable', () => {
  async function montar() {
    const fixture = TestBed.createComponent(HostTabla);
    fixture.detectChanges();
    await fixture.whenStable();
    const raiz = fixture.nativeElement as HTMLElement;
    return { fixture, raiz, host: fixture.componentInstance };
  }

  const textoCeldas = (raiz: HTMLElement, columna: number) =>
    Array.from(raiz.querySelectorAll('tbody tr')).map((tr) =>
      tr.querySelectorAll('td')[columna].textContent?.trim(),
    );

  it('renderiza encabezados y filas con la función cell de cada columna', async () => {
    const { raiz } = await montar();
    const encabezados = Array.from(raiz.querySelectorAll('thead th')).map((th) =>
      th.textContent?.trim(),
    );
    expect(encabezados.slice(0, 3)).toEqual(['Placa', 'Galones', 'Estado']);
    expect(textoCeldas(raiz, 0)).toEqual(['BBB222', 'AAA111', 'CCC333']);
    expect(textoCeldas(raiz, 1)).toEqual(['20 gal', '5 gal', '—']);
  });

  it('usa la plantilla de celda personalizada de la columna indicada', async () => {
    const { raiz } = await montar();
    const celdas = raiz.querySelectorAll('td .estado-personalizado');
    expect(Array.from(celdas).map((c) => c.textContent)).toEqual(['GESTIONADA', 'NUEVA', 'NUEVA']);
  });

  it('marca las filas señaladas por rowFlagged', async () => {
    const { raiz } = await montar();
    const marcadas = Array.from(raiz.querySelectorAll('tbody tr')).map((tr) =>
      tr.classList.contains('bg-danger/5'),
    );
    expect(marcadas).toEqual([false, true, true]);
  });

  it('ordena ascendente y luego descendente al pulsar el encabezado', async () => {
    const { fixture, raiz } = await montar();
    const boton = raiz.querySelectorAll<HTMLButtonElement>('thead th button')[0];

    boton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(textoCeldas(raiz, 0)).toEqual(['AAA111', 'BBB222', 'CCC333']);

    boton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(textoCeldas(raiz, 0)).toEqual(['CCC333', 'BBB222', 'AAA111']);
  });

  it('las columnas sin accesor no son ordenables', async () => {
    const { raiz } = await montar();
    const botones = raiz.querySelectorAll<HTMLButtonElement>('thead th button');
    expect(botones[0].disabled).toBe(false);
    expect(botones[3].disabled).toBe(true);
  });

  it('se actualiza al cambiar los datos y muestra el estado vacío sin filas', async () => {
    const { fixture, raiz, host } = await montar();
    host.filas.set([{ placa: 'ZZZ999', galones: 1, estado: 'nueva' }]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(textoCeldas(raiz, 0)).toEqual(['ZZZ999']);

    host.filas.set([]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(raiz.querySelector('table')).toBeNull();
    expect(raiz.textContent).toContain('Sin filas de prueba.');
  });
});
