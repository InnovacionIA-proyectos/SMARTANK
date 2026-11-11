import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../shared/ui/icon';

/** Página 404 para rutas no definidas. */
@Component({
  selector: 'app-no-encontrado-page',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex min-h-screen flex-col items-center justify-center gap-4 bg-bg p-6 text-center',
  },
  template: `
    <app-icon name="compass" [size]="48" class="text-text-muted" />
    <div>
      <p class="text-metric tabular text-primary">404</p>
      <h1 class="text-h2 text-text-primary">Página no encontrada</h1>
      <p class="mt-1 text-body text-text-muted">La ruta que buscas no existe o fue movida.</p>
    </div>
    <a
      routerLink="/despacho"
      class="rounded-md bg-primary px-4 py-2 text-body font-medium text-white hover:bg-primary-hover"
    >
      Volver al Panel de Despacho
    </a>
  `,
})
export class NoEncontradoPage {}
