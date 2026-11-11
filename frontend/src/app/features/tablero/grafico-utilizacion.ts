import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import type { Chart } from 'chart.js';
import type { UtilizacionEdsApi } from '../../core/api/api-types';
import { ThemeService } from '../../core/theme/theme.service';
import { cap } from '../../shared/utils/formato';

// Paleta de respaldo para proveedores sin color asignado.
const PALETA = [
  '#0154BA',
  '#069E49',
  '#B45309',
  '#6D28D9',
  '#0891B2',
  '#DB2777',
  '#CA8A04',
  '#475569',
];
// Color por proveedor (red de la EDS).
const COLOR_PROVEEDOR: Record<string, string> = { terpel: '#0154BA', distracom: '#069E49' };

/** Carga Chart.js bajo demanda y registra solo lo que usa el gráfico de barras. */
async function cargarChartJs(): Promise<typeof Chart> {
  const { Chart, BarController, BarElement, CategoryScale, LinearScale, Tooltip } =
    await import('chart.js');
  Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip);
  return Chart;
}

/** Valor de un token de color del tema activo (`--color-border`, `--color-text-muted`, ...). */
function token(nombre: string, respaldo: string): string {
  const valor = getComputedStyle(document.documentElement).getPropertyValue(nombre).trim();
  return valor || respaldo;
}

/**
 * Gráfico de barras "Utilización por proveedor" (tanqueos por red de EDS).
 * Chart.js directo, sin wrapper. Se redibuja al cambiar los datos o el tema, porque
 * el canvas no resuelve variables CSS por sí mismo.
 */
@Component({
  selector: 'app-grafico-utilizacion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-64 w-full' },
  template: `
    <canvas #lienzo role="img" aria-label="Tanqueos por proveedor de combustible"></canvas>
  `,
})
export class GraficoUtilizacion {
  readonly datos = input.required<readonly UtilizacionEdsApi[]>();

  private readonly lienzo = viewChild.required<ElementRef<HTMLCanvasElement>>('lienzo');
  private readonly theme = inject(ThemeService);

  constructor() {
    const destruir = inject(DestroyRef);
    let grafico: Chart | null = null;

    effect((alLimpiar) => {
      const datos = this.datos();
      this.theme.tema(); // dependencia: redibujar al cambiar claro/oscuro
      const canvas = this.lienzo().nativeElement;
      let cancelado = false;

      void cargarChartJs().then((ChartJs) => {
        if (cancelado) return;
        const borde = token('--color-border', '#d7dee7');
        const muted = token('--color-text-muted', '#5b6b7f');
        const reducirMovimiento = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        grafico = new ChartJs(canvas, {
          type: 'bar',
          data: {
            labels: datos.map((u) => cap(u.proveedor)),
            datasets: [
              {
                label: 'Tanqueos',
                data: datos.map((u) => u.tanqueos),
                backgroundColor: datos.map(
                  (u, i) => COLOR_PROVEEDOR[u.proveedor] ?? PALETA[i % PALETA.length],
                ),
                borderRadius: { topLeft: 4, topRight: 4 },
                borderSkipped: false,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: reducirMovimiento ? false : undefined,
            plugins: {
              legend: { display: false },
              tooltip: {
                callbacks: {
                  title: (items) => items[0]?.label ?? '',
                  label: (item) => {
                    const u = datos[item.dataIndex];
                    return `${item.parsed.y} tanqueos · ${(u?.galones ?? 0).toLocaleString('es-CO')} gal`;
                  },
                },
              },
            },
            scales: {
              x: {
                grid: { color: borde },
                border: { color: muted },
                ticks: { color: muted, font: { size: 12 } },
              },
              y: {
                beginAtZero: true,
                grid: { color: borde },
                border: { color: muted },
                ticks: { color: muted, font: { size: 12 }, precision: 0 },
              },
            },
          },
        });
      });

      alLimpiar(() => {
        cancelado = true;
        grafico?.destroy();
        grafico = null;
      });
    });

    destruir.onDestroy(() => grafico?.destroy());
  }
}
