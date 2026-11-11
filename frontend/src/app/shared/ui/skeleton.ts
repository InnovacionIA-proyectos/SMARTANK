import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Bloque de carga (placeholder). Las dimensiones se dan con clases del host:
 * `<app-skeleton class="h-[32rem] w-full" />`. El pulso se neutraliza con reduced-motion.
 */
@Component({
  selector: 'app-skeleton',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block animate-pulse rounded-md bg-border/60', 'aria-hidden': 'true' },
})
export class Skeleton {}
