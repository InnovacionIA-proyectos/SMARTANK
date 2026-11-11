import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  input,
} from '@angular/core';
import {
  AlertTriangle,
  ArrowLeft,
  BarChart3,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  ChevronsUpDown,
  Compass,
  Fuel,
  Gauge,
  History,
  ImagePlus,
  Inbox,
  LayoutDashboard,
  Loader2,
  LogOut,
  MapPin,
  Moon,
  PanelLeftClose,
  PanelLeftOpen,
  Sun,
  X,
  createElement,
} from 'lucide';

/** Catálogo de íconos usados por la app (outline, Lucide). Solo se empaquetan estos. */
const ICONOS = {
  'alert-triangle': AlertTriangle,
  'arrow-left': ArrowLeft,
  'bar-chart-3': BarChart3,
  'check-circle-2': CheckCircle2,
  'chevron-down': ChevronDown,
  'chevron-up': ChevronUp,
  'chevrons-up-down': ChevronsUpDown,
  compass: Compass,
  fuel: Fuel,
  gauge: Gauge,
  history: History,
  'image-plus': ImagePlus,
  inbox: Inbox,
  'layout-dashboard': LayoutDashboard,
  'loader-2': Loader2,
  'log-out': LogOut,
  'map-pin': MapPin,
  moon: Moon,
  'panel-left-close': PanelLeftClose,
  'panel-left-open': PanelLeftOpen,
  sun: Sun,
  x: X,
} as const;

export type NombreIcono = keyof typeof ICONOS;

/**
 * Ícono Lucide como SVG en línea. Se usa la librería `lucide` (sin dependencia de
 * Angular) y se inserta el SVG generado directamente en el DOM: así no hay wrapper
 * de terceros que pueda quedar desfasado respecto a la versión de Angular.
 */
@Component({
  selector: 'app-icon',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex shrink-0', 'aria-hidden': 'true' },
})
export class Icon {
  readonly name = input.required<NombreIcono>();
  readonly size = input(20);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    effect(() => {
      const svg = createElement(ICONOS[this.name()], {
        width: this.size(),
        height: this.size(),
        'aria-hidden': 'true',
      });
      this.host.nativeElement.replaceChildren(svg);
    });
  }
}
