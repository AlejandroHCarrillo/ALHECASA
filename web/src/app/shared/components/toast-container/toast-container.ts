import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-toast-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toast toast-top toast-end z-[1000]" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="alert alert-success shadow-lg" role="status">
          <i class="fa-solid fa-circle-check" aria-hidden="true"></i>
          <span>{{ toast.message }}</span>
        </div>
      }
    </div>
  `,
})
export class ToastContainer {
  protected readonly toasts = inject(ToastService);
}
