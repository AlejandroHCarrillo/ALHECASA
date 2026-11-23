import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, input, output, viewChild } from '@angular/core';

/**
 * Modal basado en <dialog> nativo: el navegador aporta el bloqueo de foco, Esc y el fondo inerte.
 * El padre lo crea con @if y lo destruye al recibir (closed).
 */
@Component({
  selector: 'app-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog class="modal" [attr.aria-labelledby]="'modal-title'" (close)="closed.emit()">
      <div class="modal-box" [class]="boxClass()">
        <h3 id="modal-title" class="mb-4 text-lg font-bold">{{ title() }}</h3>
        <ng-content />
      </div>
      <form method="dialog" class="modal-backdrop">
        <button type="submit" aria-label="Cerrar">cerrar</button>
      </form>
    </dialog>
  `,
})
export class Modal {
  readonly title = input.required<string>();
  readonly boxClass = input('max-w-2xl');
  readonly closed = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }
}
