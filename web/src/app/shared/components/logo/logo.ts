import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

type LogoSize = 'sm' | 'md' | 'lg';

/**
 * Logo de ALHECASA: isotipo (casa con cinta verde petróleo) y nombre en Manrope.
 * `negative` es la variante para fondo azul; sin ella, la variante principal para fondo claro.
 */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex flex-col" [class.items-center]="markOnly()">
      <span class="inline-flex items-center gap-2" [class]="textColor()">
        <svg [attr.width]="markPx()" [attr.height]="markPx()" viewBox="0 0 100 100" aria-hidden="true" focusable="false">
          <path d="M6 52 L30 33 V94 H6 Z" [attr.fill]="band()" />
          <path
            [attr.fill]="house()"
            fill-rule="evenodd"
            d="M30 33 L62 8 L94 33 V94 H30 Z M50 52 H74 V76 H50 Z"
          />
        </svg>
        @if (!markOnly()) {
          <span class="font-display font-extrabold uppercase leading-none tracking-tight" [class]="textSize()">ALHECASA</span>
        }
      </span>
      @if (tagline() && !markOnly()) {
        <span class="mt-1 text-sm" [class]="negative() ? 'text-white/70' : 'text-base-content/60'">
          El lugar donde comienza tu historia.
        </span>
      }
    </span>
  `,
})
export class Logo {
  readonly negative = input(false);
  readonly markOnly = input(false);
  readonly tagline = input(false);
  readonly size = input<LogoSize>('md');

  protected readonly house = computed(() => (this.negative() ? '#ffffff' : '#142D4E'));
  protected readonly band = computed(() => (this.negative() ? '#2BB5A6' : '#0F766E'));
  protected readonly textColor = computed(() => (this.negative() ? 'text-white' : 'text-secondary'));
  protected readonly markPx = computed(() => ({ sm: 28, md: 36, lg: 52 })[this.size()]);
  protected readonly textSize = computed(() => ({ sm: 'text-xl', md: 'text-2xl', lg: 'text-4xl' })[this.size()]);
}
