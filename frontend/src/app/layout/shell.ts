import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from './sidebar';
import { ThemeToggle } from './theme-toggle';
import { UserMenu } from './user-menu';

/** Layout de las pantallas autenticadas: sidebar, cabecera con tema/usuario y pie. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, ThemeToggle, UserMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative flex min-h-screen' },
  template: `
    <!-- Fondo: color del tema como base + la imagen como textura MUY tenue detrás.
         Así el texto (oscuro en tema claro / claro en tema oscuro) siempre contrasta. -->
    <div aria-hidden="true" class="fixed inset-0 -z-20 bg-bg"></div>
    <div
      aria-hidden="true"
      class="pointer-events-none fixed inset-0 -z-10 bg-[url('/fondo.jpg')] bg-cover bg-center bg-fixed opacity-[0.12]"
    ></div>

    <!-- Navegación lateral (colapsable) -->
    <app-sidebar />

    <div class="flex min-w-0 flex-1 flex-col">
      <!-- Header -->
      <header
        class="flex h-14 shrink-0 items-center justify-end gap-2 border-b border-border bg-surface px-6"
      >
        <app-theme-toggle />
        <app-user-menu />
      </header>

      <!-- Contenedor de contenido -->
      <main class="flex-1 p-6">
        <div class="mx-auto w-full max-w-screen-2xl">
          <router-outlet />
        </div>
      </main>

      <!-- Footer -->
      <footer
        class="shrink-0 border-t border-border bg-surface px-6 py-3 text-caption text-text-muted"
      >
        SMARTANK · {{ anio }} Botero Soto — Soluciones Logísticas
      </footer>
    </div>
  `,
})
export class Shell {
  protected readonly anio = new Date().getFullYear();
}
