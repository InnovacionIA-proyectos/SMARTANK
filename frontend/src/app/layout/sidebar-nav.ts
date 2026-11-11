import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';
import type { Rol } from '../core/api/api-types';
import { AuthService } from '../core/auth/auth.service';
import { ROLES_ALERTAS, ROLES_TABLERO } from '../core/auth/roles';
import { Icon, type NombreIcono } from '../shared/ui/icon';

interface ItemNav {
  href: string;
  label: string;
  icon: NombreIcono;
  /** Roles que ven el ítem; sin definir lo ven todos los usuarios autenticados. */
  roles?: readonly Rol[];
}

/** Ítems de la navegación lateral, en el mismo orden y con los mismos textos del original. */
export const ITEMS_NAV: readonly ItemNav[] = [
  { href: '/despacho', label: 'Panel de Despacho', icon: 'layout-dashboard' },
  { href: '/recomendacion', label: 'Recomendación de Tanqueo', icon: 'fuel' },
  { href: '/seguimiento', label: 'Seguimiento Control Combustible', icon: 'gauge' },
  { href: '/alertas', label: 'Bandeja de Alertas', icon: 'alert-triangle', roles: ROLES_ALERTAS },
  { href: '/tablero', label: 'Recuperación de Margen', icon: 'bar-chart-3', roles: ROLES_TABLERO },
  { href: '/historial', label: 'Historial de Tanqueos', icon: 'history' },
];

/** Navegación principal: filtra por rol y marca como activo el ítem de la ruta actual. */
@Component({
  selector: 'app-sidebar-nav',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1', role: 'navigation', 'aria-label': 'Navegación principal' },
  template: `
    @for (item of visibles(); track item.href) {
      <a
        [routerLink]="item.href"
        [class]="clases(item.href)"
        [attr.aria-current]="activo(item.href) ? 'page' : null"
        [attr.title]="colapsado() ? item.label : null"
      >
        <app-icon [name]="item.icon" [size]="20" />
        @if (!colapsado()) {
          {{ item.label }}
        }
      </a>
    }
  `,
})
export class SidebarNav {
  readonly colapsado = input(false);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly visibles = computed(() =>
    ITEMS_NAV.filter((i) => !i.roles || this.auth.tieneRol(i.roles)),
  );

  protected activo(href: string): boolean {
    return this.url().startsWith(href);
  }

  protected clases(href: string): string {
    const base = `flex items-center gap-3 rounded-md py-2 text-body transition-colors ${
      this.colapsado() ? 'justify-center px-2' : 'px-3'
    }`;
    return `${base} ${this.activo(href) ? 'bg-primary text-white' : 'text-text-muted hover:bg-border/40'}`;
  }
}
