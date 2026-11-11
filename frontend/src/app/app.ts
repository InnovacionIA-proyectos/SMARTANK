import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme/theme.service';

/** Raíz de la aplicación: solo hospeda el enrutador y aplica el tema guardado. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<router-outlet />',
})
export class App {
  constructor() {
    // Instanciar el servicio aplica de inmediato la clase `.dark` guardada en <html>.
    inject(ThemeService);
  }
}
