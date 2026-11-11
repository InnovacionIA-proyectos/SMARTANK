import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { mensajeDeError } from '../../core/api/api-error';
import { BitacoraService } from '../../core/servicios/bitacora.service';
import { EventosService } from '../../core/servicios/eventos.service';
import { ImagenService } from '../../core/servicios/imagen.service';
import { Button } from '../../shared/ui/button';
import { Card } from '../../shared/ui/card';
import { Icon } from '../../shared/ui/icon';
import { Input } from '../../shared/ui/input';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/states';
import { cargarApi } from '../../shared/utils/cargar-api';
import { formatFecha } from '../../shared/utils/fecha';

/**
 * Bitácora móvil del conductor. Es pública: el enlace de WhatsApp trae un token firmado
 * (`/bitacora/:token`) que identifica la orden de tanqueo, sin login.
 */
@Component({
  selector: 'app-bitacora-page',
  imports: [Card, Input, Button, Icon, LoadingState, ErrorState, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (ok()) {
      <div
        class="mx-auto flex min-h-screen max-w-md flex-col items-center justify-center gap-4 p-6 text-center"
      >
        <app-icon name="check-circle-2" [size]="56" class="text-success" />
        <h1 class="text-h2">Tanqueo registrado</h1>
        <p class="text-body text-text-muted">Gracias. El evento quedó en la bitácora.</p>
      </div>
    } @else if (carga.loading() && !carga.data()) {
      <div class="mx-auto max-w-md p-4"><app-loading-state mensaje="Cargando orden…" /></div>
    } @else if (carga.error()) {
      <div class="mx-auto max-w-md p-4">
        <app-error-state
          titulo="No se pudo abrir la orden"
          [mensaje]="carga.error() ?? ''"
          [conReintento]="true"
          (reintentar)="carga.refetch()"
        />
      </div>
    } @else if (carga.data(); as orden) {
      @if (orden.ya_registrada) {
        <div
          class="mx-auto flex min-h-screen max-w-md flex-col items-center justify-center gap-4 p-6 text-center"
        >
          <app-icon name="check-circle-2" [size]="56" class="text-success" />
          <h1 class="text-h2">Tanqueo ya registrado</h1>
          <p class="text-body text-text-muted">Esta orden ya tiene su cierre en la bitácora.</p>
        </div>
      } @else if (orden.estado !== 'autorizada') {
        <div class="mx-auto max-w-md p-4">
          <app-empty-state
            titulo="Orden no vigente"
            mensaje="Esta orden de tanqueo no está autorizada actualmente. Comunícate con el despacho."
          />
        </div>
      } @else {
        <div class="mx-auto max-w-md space-y-4 p-4">
          <div>
            <h1 class="text-h2">Cierre de Tanqueo</h1>
            <p class="text-body text-text-muted">
              Manifiesto {{ orden.manifiesto ?? '—' }} · registra la carga: galones, odómetro y foto
              del surtidor.
            </p>
          </div>

          <app-card>
            <dl class="grid grid-cols-2 gap-x-4 gap-y-3">
              <div>
                <dt class="text-caption text-text-muted">Placa</dt>
                <dd class="text-body text-text-primary">{{ orden.placa }}</dd>
              </div>
              <div>
                <dt class="text-caption text-text-muted">Galones autorizados</dt>
                <dd class="text-body tabular text-text-primary">{{ orden.galones_max }}</dd>
              </div>
              <div class="col-span-2">
                <dt class="text-caption text-text-muted">Estación</dt>
                <dd class="text-body text-text-primary">{{ orden.estacion_nombre }}</dd>
              </div>
              @if (orden.ruta) {
                <div class="col-span-2">
                  <dt class="text-caption text-text-muted">Ruta</dt>
                  <dd class="text-body text-text-primary">{{ orden.ruta }}</dd>
                </div>
              }
              <div class="col-span-2">
                <dt class="text-caption text-text-muted">Vigente hasta</dt>
                <dd class="text-body text-text-primary">{{ fechaVence() }}</dd>
              </div>
            </dl>
          </app-card>

          <app-card>
            <form (submit)="enviar($event)" class="space-y-4">
              <app-input
                label="Galones cargados"
                type="number"
                inputmode="decimal"
                step="0.1"
                min="0"
                [required]="true"
                [(value)]="galones"
                inputClass="h-12"
              />
              <app-input
                label="Odómetro (km)"
                type="number"
                inputmode="numeric"
                min="0"
                [(value)]="odometro"
                inputClass="h-12"
              />
              <div class="flex flex-col gap-1">
                <span class="text-caption font-medium text-text-muted">Foto del surtidor</span>
                <input
                  #archivo
                  type="file"
                  accept="image/*"
                  class="hidden"
                  (change)="seleccionarFoto($event)"
                />
                <div class="flex items-center gap-3 rounded-md border border-border bg-surface p-3">
                  @if (vistaPrevia()) {
                    <img
                      [src]="vistaPrevia()"
                      alt="Foto del surtidor"
                      class="h-16 w-16 shrink-0 rounded object-cover"
                    />
                  } @else {
                    <div
                      class="flex h-16 w-16 shrink-0 items-center justify-center rounded bg-bg text-text-muted"
                    >
                      <app-icon name="image-plus" [size]="22" />
                    </div>
                  }
                  <div class="flex flex-col gap-1">
                    <button
                      appButton
                      type="button"
                      variant="secondary"
                      [disabled]="procesando() || enviando()"
                      (click)="archivo.click()"
                    >
                      {{
                        procesando() ? 'Procesando…' : foto() ? 'Cambiar imagen' : 'Subir imagen'
                      }}
                    </button>
                    <span class="text-caption text-text-muted">
                      {{
                        foto()
                          ? 'Imagen lista para enviar.'
                          : 'Selecciona una foto desde el dispositivo.'
                      }}
                    </span>
                  </div>
                </div>
                @if (fotoError()) {
                  <span class="text-caption text-danger">{{ fotoError() }}</span>
                }
              </div>
              @if (error()) {
                <p class="rounded-md bg-danger/10 px-3 py-2 text-caption text-danger" role="alert">
                  {{ error() }}
                </p>
              }
              <button
                appButton
                type="submit"
                size="lg"
                class="h-12 w-full"
                [disabled]="enviando() || procesando() || !galones()"
              >
                {{ enviando() ? 'Registrando…' : 'Registrar tanqueo' }}
              </button>
            </form>
          </app-card>
        </div>
      }
    }
  `,
})
export class BitacoraPage {
  /** Token firmado de la ruta `/bitacora/:token`. */
  readonly token = input.required<string>();

  private readonly bitacora = inject(BitacoraService);
  private readonly eventos = inject(EventosService);
  private readonly imagen = inject(ImagenService);

  protected readonly carga = cargarApi(() => this.bitacora.obtener(this.token()));
  protected readonly fechaVence = computed(() => formatFecha(this.carga.data()?.vence_at));

  protected readonly galones = signal('');
  protected readonly odometro = signal('');
  /** URL devuelta por el API para la foto subida (se envía al registrar el evento). */
  protected readonly foto = signal('');
  /**
   * Vista previa local de la foto: `GET /api/uploads/{id}` exige sesión, así que el
   * conductor (anónimo) no podría ver la imagen que acaba de subir desde esa URL.
   */
  protected readonly vistaPrevia = signal('');
  protected readonly fotoError = signal<string | null>(null);
  protected readonly procesando = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly ok = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.liberarVistaPrevia());
  }

  protected async seleccionarFoto(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const file = entrada.files?.[0];
    if (!file) return;
    this.fotoError.set(null);
    this.procesando.set(true);
    try {
      const blob = await this.imagen.comprimir(file);
      const { url } = await firstValueFrom(this.bitacora.subirFoto(this.token(), blob));
      this.liberarVistaPrevia();
      this.vistaPrevia.set(URL.createObjectURL(blob));
      this.foto.set(url);
    } catch (e) {
      this.fotoError.set(mensajeDeError(e, 'No se pudo subir la imagen. Intenta de nuevo.'));
      this.liberarVistaPrevia();
      this.foto.set('');
    } finally {
      this.procesando.set(false);
      // Permite volver a elegir el mismo archivo si falló.
      entrada.value = '';
    }
  }

  protected enviar(evento: Event): void {
    evento.preventDefault();
    this.error.set(null);
    this.enviando.set(true);
    this.eventos
      .registrar({
        token: this.token(),
        galones_cargados: Number(this.galones()),
        odometro_evento: this.odometro() ? Number(this.odometro()) : null,
        foto_surtidor_url: this.foto() || null,
      })
      .subscribe({
        next: () => this.ok.set(true),
        error: (e: unknown) => {
          this.error.set(mensajeDeError(e, 'No se pudo registrar el tanqueo'));
          this.enviando.set(false);
        },
      });
  }

  private liberarVistaPrevia(): void {
    const actual = this.vistaPrevia();
    if (actual) URL.revokeObjectURL(actual);
    this.vistaPrevia.set('');
  }
}
