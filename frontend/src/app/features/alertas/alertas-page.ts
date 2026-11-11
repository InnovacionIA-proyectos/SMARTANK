import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import type { ColumnDef } from '@tanstack/angular-table';
import type { AlertaApi, EstadoAlerta, TipoAlerta } from '../../core/api/api-types';
import { AlertasService } from '../../core/servicios/alertas.service';
import { Badge } from '../../shared/ui/badge';
import { Card } from '../../shared/ui/card';
import { CeldaDef, DataTable } from '../../shared/ui/data-table';
import { Select } from '../../shared/ui/select';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { formatFecha } from '../../shared/utils/fecha';
import { GestionAlerta } from './gestion-alerta';

const TIPO_BADGE: Record<TipoAlerta, 'danger' | 'warning'> = {
  desviacion_consumo_carga: 'danger',
  desviacion_consumo: 'warning',
  estacion_cara: 'warning',
  ejecucion_parcial: 'warning',
};
// Etiqueta visible por tipo de alerta.
const TIPO_LABEL: Record<TipoAlerta, string> = {
  desviacion_consumo_carga: 'Desviación de consumo (carga)',
  desviacion_consumo: 'Desviación de consumo (rendimiento)',
  estacion_cara: 'Estación cara',
  ejecucion_parcial: 'Ejecución parcial',
};
const ESTADO_BADGE: Record<EstadoAlerta, 'info' | 'success' | 'neutral'> = {
  nueva: 'info',
  en_revision: 'info',
  gestionada: 'success',
  descartada: 'neutral',
};

/** Bandeja de Alertas: desviaciones de consumo y demás anomalías (Control de Flota / Admin). */
@Component({
  selector: 'app-alertas-page',
  imports: [
    Card,
    Badge,
    Select,
    DataTable,
    CeldaDef,
    GestionAlerta,
    LoadingState,
    ErrorState,
    EmptyState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block space-y-4' },
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="text-h1">Bandeja de Alertas</h1>
        <p class="text-body text-text-muted">
          Gestiona las desviaciones de consumo y demás anomalías de la flota.
        </p>
      </div>
      <app-select
        label="Filtrar por estado"
        [(value)]="estado"
        placeholder="Todas"
        [options]="estados"
        selectClass="w-48"
      />
    </div>

    <app-card class="p-0">
      @if (carga.loading()) {
        <app-loading-state mensaje="Cargando alertas…" />
      } @else if (carga.error()) {
        <app-error-state
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      } @else if (!carga.data() || carga.data()!.alertas.length === 0) {
        <app-empty-state
          titulo="Sin alertas"
          mensaje="No hay desviaciones para el filtro seleccionado."
        />
      } @else {
        <app-data-table
          [columns]="columnas"
          [data]="carga.data()!.alertas"
          [rowFlagged]="esNueva"
          emptyMessage="Sin alertas."
        >
          <ng-template appCelda="tipo" [appCeldaDe]="carga.data()!.alertas" let-alerta>
            <app-badge [variant]="tipoBadge[alerta.tipo]">{{ tipoLabel[alerta.tipo] }}</app-badge>
          </ng-template>
          <ng-template appCelda="estado" [appCeldaDe]="carga.data()!.alertas" let-alerta>
            <app-badge [variant]="estadoBadge[alerta.estado]">{{
              estadoTexto(alerta.estado)
            }}</app-badge>
          </ng-template>
          <ng-template appCelda="acciones" [appCeldaDe]="carga.data()!.alertas" let-alerta>
            <app-gestion-alerta [alerta]="alerta" (cambio)="carga.refetch()" />
          </ng-template>
        </app-data-table>
      }
    </app-card>
  `,
})
export class AlertasPage {
  private readonly alertas = inject(AlertasService);

  protected readonly estado = signal('');
  protected readonly carga = cargarApi(() => this.alertas.listar(this.estado()));

  protected readonly estados = [
    { value: 'nueva', label: 'Nuevas' },
    { value: 'en_revision', label: 'En revisión' },
    { value: 'gestionada', label: 'Gestionadas' },
    { value: 'descartada', label: 'Descartadas' },
  ];

  protected readonly tipoBadge = TIPO_BADGE;
  protected readonly tipoLabel = TIPO_LABEL;
  protected readonly estadoBadge = ESTADO_BADGE;

  protected readonly columnas: ColumnDef<AlertaApi, unknown>[] = [
    { accessorKey: 'placa', header: 'Placa' },
    { accessorKey: 'tipo', header: 'Tipo' },
    {
      accessorKey: 'magnitud_desviacion',
      header: 'Magnitud',
      cell: ({ row }) =>
        row.original.magnitud_desviacion != null ? String(row.original.magnitud_desviacion) : '—',
    },
    { accessorKey: 'estado', header: 'Estado' },
    {
      accessorKey: 'created_at',
      header: 'Fecha',
      cell: ({ row }) => formatFecha(row.original.created_at),
    },
    { id: 'acciones', header: '' },
  ];

  /** Las alertas nuevas se resaltan con fondo danger sutil. */
  protected readonly esNueva = (a: AlertaApi): boolean => a.estado === 'nueva';

  protected estadoTexto(estado: string): string {
    return estado.replace(/_/g, ' ');
  }
}
