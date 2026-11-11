import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  TemplateRef,
  computed,
  contentChildren,
  inject,
  input,
  signal,
} from '@angular/core';
import {
  type ColumnDef,
  FlexRender,
  type SortingState,
  createAngularTable,
  getCoreRowModel,
  getSortedRowModel,
} from '@tanstack/angular-table';
import { Icon } from './icon';
import { EmptyState } from './states';

/**
 * Plantilla de celda personalizada para una columna (por `id` o `accessorKey`):
 *
 *   <app-data-table [columns]="columnas" [data]="filas">
 *     <ng-template appCelda="estado" let-fila><app-badge>{{ fila.estado }}</app-badge></ng-template>
 *   </app-data-table>
 *
 * El contexto implícito es la fila original. Las columnas sin plantilla se
 * renderizan con la función `cell` de su definición (o el valor del accesor).
 */
@Directive({ selector: 'ng-template[appCelda]' })
export class CeldaDef<T = unknown> {
  readonly appCelda = input.required<string>();
  /**
   * Filas de la tabla. No se usa en ejecución: sirve para que el compilador infiera
   * el tipo de `let-fila` en la plantilla (`[appCeldaDe]="filas()"`).
   */
  readonly appCeldaDe = input<readonly T[]>([]);
  readonly plantilla = inject<TemplateRef<CeldaContexto<T>>>(TemplateRef);

  // Los parámetros solo existen para que el compilador infiera el tipo del contexto.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  static ngTemplateContextGuard<T>(dir: CeldaDef<T>, ctx: unknown): ctx is CeldaContexto<T> {
    return true;
  }
}

/** Contexto de una plantilla de celda: la fila original como valor implícito. */
export interface CeldaContexto<T> {
  $implicit: T;
}

/**
 * Tabla densa (36–40px/fila), columnas numéricas tabulares, orden por columna.
 * Lógica de tabla con TanStack Table (`@tanstack/angular-table`).
 */
@Component({
  selector: 'app-data-table',
  imports: [FlexRender, NgTemplateOutlet, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (data().length === 0) {
      <app-empty-state [mensaje]="emptyMessage() ?? 'No hay registros para mostrar.'" />
    } @else {
      <div class="overflow-x-auto rounded-lg border border-border">
        <table class="w-full border-collapse text-body tabular">
          <thead>
            @for (grupo of table.getHeaderGroups(); track grupo.id) {
              <tr class="border-b border-border bg-bg/50">
                @for (header of grupo.headers; track header.id) {
                  <th class="px-3 py-2 text-left text-caption font-semibold text-text-muted">
                    @if (!header.isPlaceholder) {
                      <button
                        type="button"
                        [disabled]="!header.column.getCanSort()"
                        (click)="alOrdenar(header.column.getToggleSortingHandler(), $event)"
                        [class]="
                          header.column.getCanSort()
                            ? 'flex items-center gap-1 hover:text-text-primary'
                            : 'flex items-center gap-1'
                        "
                      >
                        <ng-container
                          *flexRender="
                            header.column.columnDef.header;
                            props: header.getContext();
                            let titulo
                          "
                        >
                          {{ titulo }}
                        </ng-container>
                        @if (header.column.getCanSort()) {
                          @switch (header.column.getIsSorted()) {
                            @case ('asc') {
                              <app-icon name="chevron-up" [size]="14" />
                            }
                            @case ('desc') {
                              <app-icon name="chevron-down" [size]="14" />
                            }
                            @default {
                              <app-icon name="chevrons-up-down" [size]="14" class="opacity-40" />
                            }
                          }
                        }
                      </button>
                    }
                  </th>
                }
              </tr>
            }
          </thead>
          <tbody>
            @for (fila of table.getRowModel().rows; track fila.id) {
              <tr
                [class]="
                  'border-b border-border last:border-0 hover:bg-bg/60' +
                  (esMarcada(fila.original) ? ' bg-danger/5' : '')
                "
              >
                @for (celda of fila.getVisibleCells(); track celda.id) {
                  <td class="px-3 py-2 text-text-primary">
                    @if (plantillas().get(celda.column.id); as plantilla) {
                      <ng-container
                        *ngTemplateOutlet="plantilla; context: { $implicit: fila.original }"
                      />
                    } @else {
                      <ng-container
                        *flexRender="
                          celda.column.columnDef.cell;
                          props: celda.getContext();
                          let contenido
                        "
                      >
                        {{ contenido }}
                      </ng-container>
                    }
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class DataTable<TData> {
  readonly columns = input.required<ColumnDef<TData, unknown>[]>();
  readonly data = input.required<readonly TData[]>();
  /** Marca una fila con fondo danger sutil (filas con alerta). */
  readonly rowFlagged = input<((fila: TData) => boolean) | undefined>(undefined);
  readonly emptyMessage = input<string | undefined>(undefined);

  private readonly celdas = contentChildren(CeldaDef);
  protected readonly plantillas = computed(
    () => new Map(this.celdas().map((c) => [c.appCelda(), c.plantilla] as const)),
  );

  private readonly sorting = signal<SortingState>([]);

  protected readonly table = createAngularTable<TData>(() => ({
    data: this.data() as TData[],
    columns: this.columns(),
    state: { sorting: this.sorting() },
    onSortingChange: (actualizacion) =>
      this.sorting.update((s) =>
        typeof actualizacion === 'function' ? actualizacion(s) : actualizacion,
      ),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  }));

  protected esMarcada(fila: TData): boolean {
    return this.rowFlagged()?.(fila) ?? false;
  }

  protected alOrdenar(manejador: ((evento: unknown) => void) | undefined, evento: Event): void {
    manejador?.(evento);
  }
}
