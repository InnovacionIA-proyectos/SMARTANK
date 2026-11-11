import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import type { ColumnDef } from '@tanstack/angular-table';
import type { EventoTanqueoApi, EventosFiltros } from '../../core/api/api-types';
import { EventosService } from '../../core/servicios/eventos.service';
import { Badge } from '../../shared/ui/badge';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { CeldaDef, DataTable } from '../../shared/ui/data-table';
import { Input } from '../../shared/ui/input';
import { Modal } from '../../shared/ui/modal';
import { Select } from '../../shared/ui/select';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { formatFecha } from '../../shared/utils/fecha';
import { copMoneda, entero } from '../../shared/utils/formato';

/** Etiqueta y color del proveedor/red de la EDS (terpel/distracom). */
const PROVEEDOR: Record<string, { label: string; variant: 'info' | 'warning' }> = {
  terpel: { label: 'Terpel', variant: 'info' },
  distracom: { label: 'Distracom', variant: 'warning' },
};

interface Filtros {
  placa: string;
  proveedor: string;
  desde: string;
  hasta: string;
}
const VACIO: Filtros = { placa: '', proveedor: '', desde: '', hasta: '' };

/** Historial de tanqueos (Terpel/Distracom) con filtros y detalle de la bitácora del conductor. */
@Component({
  selector: 'app-historial-page',
  imports: [
    Card,
    Badge,
    Button,
    Input,
    Select,
    Modal,
    DataTable,
    CeldaDef,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <div>
      <h1 class="text-h1">Historial de Tanqueos</h1>
      <p class="text-body text-text-muted">
        Consulta los tanqueos registrados con los proveedores Terpel y Distracom. Filtra por placa,
        proveedor y rango de fechas.
      </p>
    </div>

    <app-card>
      <form (submit)="buscar($event)" class="flex flex-wrap items-end gap-3">
        <app-input label="Placa" [(value)]="placa" placeholder="Ej. SNU123" inputClass="w-40" />
        <app-select
          label="Proveedor"
          [(value)]="proveedor"
          placeholder="Todos"
          [options]="proveedores"
          selectClass="w-44"
        />
        <app-input label="Desde" type="date" [(value)]="desde" inputClass="w-44" />
        <app-input label="Hasta" type="date" [(value)]="hasta" inputClass="w-44" />
        <div class="flex gap-2">
          <button appButton type="submit">Buscar</button>
          <button appButton type="button" variant="secondary" (click)="limpiar()">Limpiar</button>
        </div>
      </form>
    </app-card>

    <app-card class="p-0">
      @if (carga.loading()) {
        <app-loading-state mensaje="Cargando tanqueos…" />
      } @else if (carga.error()) {
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      } @else if (!carga.data() || carga.data()!.eventos.length === 0) {
        <app-empty-state
          titulo="Sin tanqueos"
          mensaje="No hay tanqueos para los filtros seleccionados."
        />
      } @else {
        <app-data-table
          [columns]="columnas"
          [data]="carga.data()!.eventos"
          emptyMessage="Sin tanqueos."
        >
          <ng-template appCelda="programa" [appCeldaDe]="carga.data()!.eventos" let-evento>
            <!-- Programa de flota del vehículo al tanquear (código S&T, p. ej. "TM_MED-CTG SYT"). -->
            @if (evento.programa) {
              {{ evento.programa }}
            } @else {
              <span class="text-text-muted">—</span>
            }
          </ng-template>
          <ng-template appCelda="proveedor" [appCeldaDe]="carga.data()!.eventos" let-evento>
            @if (metaProveedor(evento.proveedor); as meta) {
              <app-badge [variant]="meta.variant">{{ meta.label }}</app-badge>
            } @else {
              <span class="text-text-muted">—</span>
            }
          </ng-template>
          <ng-template appCelda="autorizado" [appCeldaDe]="carga.data()!.eventos" let-evento>
            <!-- ¿El tanqueo real casó con una autorización de la app (placa+estación+fecha)? -->
            @if (evento.autorizado) {
              <app-badge variant="success">Sí</app-badge>
            } @else {
              <span class="text-text-muted">No</span>
            }
          </ng-template>
          <ng-template appCelda="bitacora" [appCeldaDe]="carga.data()!.eventos" let-evento>
            <button
              appButton
              size="sm"
              variant="secondary"
              type="button"
              (click)="bitacora.set(evento)"
            >
              Ver
            </button>
          </ng-template>
        </app-data-table>
      }
    </app-card>

    <app-modal [open]="!!bitacora()" (closed)="bitacora.set(null)" title="Bitácora del tanqueo">
      @if (bitacora(); as b) {
        <dl class="grid grid-cols-2 gap-x-4 gap-y-3">
          @for (dato of datosBitacora(b); track dato.label) {
            <div>
              <dt class="text-caption text-text-muted">{{ dato.label }}</dt>
              <dd class="text-body text-text-primary">{{ dato.valor }}</dd>
            </div>
          }
          <div class="col-span-2">
            <dt class="text-caption text-text-muted">Foto del surtidor</dt>
            <dd class="mt-1">
              @if (b.foto_surtidor_url) {
                <a
                  [href]="b.foto_surtidor_url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="text-secondary underline"
                >
                  Abrir foto del surtidor
                </a>
              } @else {
                <span class="text-text-muted">Sin foto</span>
              }
            </dd>
          </div>
        </dl>
      }
    </app-modal>
  `,
})
export class HistorialPage {
  private readonly eventos = inject(EventosService);

  // Borrador del formulario vs. filtros aplicados (los que disparan la consulta).
  protected readonly placa = signal('');
  protected readonly proveedor = signal('');
  protected readonly desde = signal('');
  protected readonly hasta = signal('');
  private readonly aplicados = signal<Filtros>(VACIO);

  // Tanqueo seleccionado para ver su bitácora (lo que cargó el conductor).
  protected readonly bitacora = signal<EventoTanqueoApi | null>(null);

  protected readonly carga = cargarApi(() => {
    const f = this.aplicados();
    const filtros: EventosFiltros = {
      placa: f.placa.trim(),
      proveedor: f.proveedor,
      desde: f.desde,
      hasta: f.hasta,
    };
    return this.eventos.listar(filtros);
  });

  protected readonly proveedores = [
    { value: 'terpel', label: 'Terpel' },
    { value: 'distracom', label: 'Distracom' },
  ];

  protected readonly columnas: ColumnDef<EventoTanqueoApi, unknown>[] = [
    {
      accessorKey: 'ocurrido_at',
      header: 'Fecha',
      cell: ({ row }) => formatFecha(row.original.ocurrido_at),
    },
    { accessorKey: 'placa', header: 'Placa' },
    { accessorKey: 'programa', header: 'Programa' },
    { accessorKey: 'proveedor', header: 'Proveedor' },
    { accessorKey: 'estacion_nombre', header: 'Estación' },
    {
      accessorKey: 'estacion_ruta',
      header: 'Ruta',
      cell: ({ row }) => row.original.estacion_ruta ?? '—',
    },
    {
      accessorKey: 'galones_cargados',
      header: 'Galones',
      cell: ({ row }) => row.original.galones_cargados.toFixed(1),
    },
    {
      accessorKey: 'precio_pagado',
      header: 'Precio/galón',
      cell: ({ row }) =>
        row.original.precio_pagado != null ? copMoneda(row.original.precio_pagado) : '—',
    },
    {
      accessorKey: 'odometro_evento',
      header: 'Odómetro (km)',
      cell: ({ row }) => entero(row.original.odometro_evento, 3),
    },
    { id: 'autorizado', header: 'Autorizado' },
    { id: 'bitacora', header: 'Bitácora' },
  ];

  protected metaProveedor(p: string | null) {
    return p ? PROVEEDOR[p] : undefined;
  }

  protected datosBitacora(b: EventoTanqueoApi): { label: string; valor: string }[] {
    return [
      { label: 'Fecha', valor: formatFecha(b.ocurrido_at) },
      { label: 'Placa', valor: b.placa },
      { label: 'Programa', valor: b.programa ?? '—' },
      { label: 'Estación', valor: b.estacion_nombre },
      { label: 'Galones cargados', valor: b.galones_cargados.toFixed(1) },
      {
        label: 'Precio/galón',
        valor: b.precio_pagado != null ? copMoneda(b.precio_pagado) : '—',
      },
      { label: 'Autorizado (app)', valor: b.autorizado ? 'Sí' : 'No' },
      {
        label: 'Precio techo autorizado',
        valor: b.precio_techo != null ? copMoneda(b.precio_techo) : '—',
      },
      {
        label: 'Odómetro',
        valor: b.odometro_evento != null ? `${entero(b.odometro_evento, 3)} km` : '—',
      },
      { label: 'Nivel antes', valor: b.nivel_antes != null ? `${b.nivel_antes} gal` : '—' },
      { label: 'Nivel después', valor: b.nivel_despues != null ? `${b.nivel_despues} gal` : '—' },
    ];
  }

  protected buscar(evento: Event): void {
    evento.preventDefault();
    this.aplicados.set({
      placa: this.placa(),
      proveedor: this.proveedor(),
      desde: this.desde(),
      hasta: this.hasta(),
    });
  }

  protected limpiar(): void {
    this.placa.set('');
    this.proveedor.set('');
    this.desde.set('');
    this.hasta.set('');
    this.aplicados.set(VACIO);
  }
}
