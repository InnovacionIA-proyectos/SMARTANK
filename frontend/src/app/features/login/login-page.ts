import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { mensajeDeError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { RUTA_INICIAL } from '../../core/auth/guards';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { Input } from '../../shared/ui/input';

/** Inicio de sesión con correo y contraseña. */
@Component({
  selector: 'app-login-page',
  imports: [Card, Input, Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class:
      "flex min-h-screen items-center justify-center bg-bg bg-[url('/fondo.jpg')] bg-cover bg-center p-4",
  },
  template: `
    <app-card class="w-full max-w-sm space-y-5">
      <div>
        <img src="logo-combinado.png" alt="SMARTANK · Botero Soto" class="w-full rounded-md" />
      </div>
      <form (submit)="enviar($event)" class="space-y-4">
        <app-input
          label="Correo"
          type="email"
          autocomplete="email"
          [required]="true"
          [(value)]="email"
        />
        <app-input
          label="Contraseña"
          type="password"
          autocomplete="current-password"
          [required]="true"
          [(value)]="password"
        />
        @if (error()) {
          <p class="rounded-md bg-danger/10 px-3 py-2 text-caption text-danger" role="alert">
            {{ error() }}
          </p>
        }
        <button appButton type="submit" size="lg" class="w-full" [disabled]="enviando()">
          {{ enviando() ? 'Ingresando…' : 'Ingresar' }}
        </button>
      </form>
    </app-card>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly enviando = signal(false);

  protected enviar(evento: Event): void {
    evento.preventDefault();
    this.error.set(null);
    this.enviando.set(true);
    this.auth.login({ email: this.email(), password: this.password() }).subscribe({
      // replace: tras autenticarse, "atrás" no debe volver al formulario de login.
      next: () => void this.router.navigateByUrl(RUTA_INICIAL, { replaceUrl: true }),
      error: (err: unknown) => {
        this.error.set(mensajeDeError(err, 'No se pudo iniciar sesión'));
        this.enviando.set(false);
      },
    });
  }
}
