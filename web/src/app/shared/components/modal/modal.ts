import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, input, output, viewChild } from '@angular/core';

let nextId = 0;

/**
 * Modal basado en <dialog> nativo: el navegador aporta el bloqueo de foco, Esc y el fondo inerte.
 * El padre lo crea con @if y lo destruye al recibir (closed).
 */
@Component({
  selector: 'app-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog class="modal" [attr.aria-labelledby]="titleId" (close)="closed.emit()">
      <div class="modal-box" [class]="boxClass()">
        <h3 [id]="titleId" class="mb-4 text-lg font-bold">{{ title() }}</h3>
        <ng-content />
      </div>
      <form method="dialog" class="modal-backdrop" (submit)="$event.stopPropagation()">
        <button type="submit" aria-label="Cerrar">cerrar</button>
      </form>
    </dialog>
  `,
})
export class Modal {
  /** Con ventanas encimadas (p. ej. el aviso de privacidad sobre un formulario) cada título necesita su propio id. */
  protected readonly titleId = `modal-title-${nextId++}`;
  readonly title = input.required<string>();
  readonly boxClass = input('max-w-2xl');
  readonly closed = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }
}
