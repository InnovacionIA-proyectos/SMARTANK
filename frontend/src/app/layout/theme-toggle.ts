import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService } from '../core/theme/theme.service';
import { Button } from '../shared/ui/button';
import { Icon } from '../shared/ui/icon';

/** Conmutador de modo claro/oscuro (disponible en todas las pantallas autenticadas). */
@Component({
  selector: 'app-theme-toggle',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      appButton
      variant="ghost"
      size="sm"
      type="button"
      (click)="theme.alternar()"
      [attr.aria-label]="theme.tema() === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'"
    >
      <app-icon [name]="theme.tema() === 'dark' ? 'sun' : 'moon'" [size]="20" />
    </button>
  `,
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
}
