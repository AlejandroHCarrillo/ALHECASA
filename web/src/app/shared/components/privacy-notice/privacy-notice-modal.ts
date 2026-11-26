import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { Modal } from '../modal/modal';

/**
 * Aviso de privacidad en una ventana modal. Solo informa: no pide aceptar nada, se cierra con «Entendido», Esc o el fondo.
 * El padre lo crea con @if y lo destruye al recibir (closed).
 */
@Component({
  selector: 'app-privacy-notice-modal',
  imports: [Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './privacy-notice-modal.html',
})
export class PrivacyNoticeModal {
  readonly closed = output<void>();
}