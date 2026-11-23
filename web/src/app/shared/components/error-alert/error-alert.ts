import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Alert de error a todo el ancho con botón X para cerrarlo. */
@Component({
  selector: 'app-error-alert',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="alert" class="alert alert-error w-full">
      <i class="fa-solid fa-circle-exclamation" aria-hidden="true"></i>
      <span class="flex-1">{{ message() }}</span>
      <button type="button" class="btn btn-ghost btn-xs btn-circle" aria-label="Cerrar mensaje" (click)="dismissed.emit()">
        <i class="fa-solid fa-xmark" aria-hidden="true"></i>
      </button>
    </div>
  `,
})
export class ErrorAlert {
  readonly message = input.required<string>();
  readonly dismissed = output<void>();
}
