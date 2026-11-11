import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AuthService } from '../core/auth/auth.service';
import { Button } from '../shared/ui/button';
import { Icon } from '../shared/ui/icon';

/** Usuario en sesión y cierre de sesión (limpia la cookie en el backend y vuelve al login). */
@Component({
  selector: 'app-user-menu',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex items-center gap-2' },
  template: `
    @if (auth.usuario(); as usuario) {
      <span class="hidden text-caption text-text-muted md:inline" [attr.title]="usuario.email">
        {{ usuario.nombre || usuario.email }}
      </span>
    }
    <button
      appButton
      variant="ghost"
      size="sm"
      type="button"
      (click)="salir()"
      [disabled]="saliendo()"
    >
      <app-icon name="log-out" [size]="18" />
      <span class="hidden sm:inline">Cerrar sesión</span>
    </button>
  `,
})
export class UserMenu {
  protected readonly auth = inject(AuthService);
  protected readonly saliendo = signal(false);

  protected salir(): void {
    this.saliendo.set(true);
    this.auth.logout().subscribe();
  }
}
