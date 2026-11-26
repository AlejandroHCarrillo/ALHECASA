import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { PrivacyNoticeModal } from './privacy-notice-modal';

/**
 * Leyenda «Al <acción>, tus datos se tratan conforme al aviso de privacidad» con un enlace que abre el aviso en un modal.
 * Va junto al botón de los formularios donde se capturan datos personales o de una propiedad.
 */
@Component({
  selector: 'app-privacy-notice',
  imports: [PrivacyNoticeModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="text-xs text-base-content/60" data-testid="privacy-legend">
      Al {{ action() }}, tus datos se tratarán conforme al
      <button type="button" class="link link-primary" data-testid="privacy-open" (click)="open.set(true)">aviso de privacidad</button>.
    </p>
    @if (open()) {
      <app-privacy-notice-modal (closed)="open.set(false)" />
    }
  `,
})
export class PrivacyNotice {
  /** Verbo del botón del formulario, en infinitivo: «enviar la solicitud», «guardar», «crear tu cuenta»… */
  readonly action = input('continuar');

  protected readonly open = signal(false);
}