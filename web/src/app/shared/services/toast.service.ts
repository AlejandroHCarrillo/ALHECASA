import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
}

const TOAST_DURATION_MS = 3000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly _toasts = signal<Toast[]>([]);

  readonly toasts = this._toasts.asReadonly();

  /** Toast de éxito de 3 segundos. */
  success(message: string): void {
    const toast: Toast = { id: this.nextId++, message };
    this._toasts.update((list) => [...list, toast]);
    setTimeout(() => this.dismiss(toast.id), TOAST_DURATION_MS);
  }

  dismiss(id: number): void {
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }
}
