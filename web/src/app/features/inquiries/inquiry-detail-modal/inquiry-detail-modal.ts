import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { InquiriesService } from '../inquiries.service';
import {
  FOLLOW_UP_KIND_LABELS,
  FollowUpKind,
  INQUIRY_LIMITS,
  INQUIRY_STATUS_BADGES,
  INQUIRY_STATUS_LABELS,
  Inquiry,
  InquiryStatus,
} from '../inquiry.models';

/**
 * Atención de una solicitud: datos del prospecto, su pregunta, el historial de seguimiento y el formulario
 * para registrar una respuesta o una nota. El padre conserva la solicitud y la actualiza con (updated).
 */
@Component({
  selector: 'app-inquiry-detail-modal',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, ErrorAlert, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inquiry-detail-modal.html',
})
export class InquiryDetailModal {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly inquiries = inject(InquiriesService);
  private readonly toast = inject(ToastService);

  readonly inquiry = input.required<Inquiry>();
  readonly updated = output<Inquiry>();
  readonly closed = output<void>();

  protected readonly limits = INQUIRY_LIMITS;
  protected readonly statusLabels = INQUIRY_STATUS_LABELS;
  protected readonly statusBadges = INQUIRY_STATUS_BADGES;
  protected readonly kindLabels = FOLLOW_UP_KIND_LABELS;
  protected readonly statuses: InquiryStatus[] = [InquiryStatus.New, InquiryStatus.InProgress, InquiryStatus.Closed];
  protected readonly kinds: FollowUpKind[] = [FollowUpKind.Reply, FollowUpKind.Note];

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    kind: [FollowUpKind.Reply as FollowUpKind],
    text: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(INQUIRY_LIMITS.text)]],
  });

  private readonly text = toSignal(this.form.controls.text.valueChanges, { initialValue: '' });

  /** Correo ya redactado para el prospecto, con lo que se haya escrito como cuerpo (solo para respuestas). */
  protected readonly mailtoReply = computed(() => {
    const inquiry = this.inquiry();
    const subject = encodeURIComponent(`Informes: ${inquiry.propertyTitle}`);
    const body = encodeURIComponent(this.text());
    return `mailto:${inquiry.email}?subject=${subject}&body=${body}`;
  });

  protected addFollowUp(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.run(
      this.inquiries.addFollowUp(this.inquiry().id, { kind: value.kind, text: value.text.trim() }),
      value.kind === FollowUpKind.Reply ? 'Respuesta registrada.' : 'Nota guardada.',
      () => this.form.controls.text.reset(''),
    );
  }

  protected changeStatus(event: Event): void {
    const status = (event.target as HTMLSelectElement).value as InquiryStatus;
    if (status === this.inquiry().status) {
      return;
    }

    this.run(this.inquiries.changeStatus(this.inquiry().id, status), 'Estado actualizado.');
  }

  protected hasTextError(): boolean {
    const c = this.form.controls.text;
    return c.touched && c.invalid;
  }

  private run(request$: Observable<Inquiry>, successMessage: string, afterSuccess?: () => void): void {
    this.saving.set(true);
    this.error.set(null);

    request$.subscribe({
      next: (inquiry) => {
        this.saving.set(false);
        this.updated.emit(inquiry);
        this.toast.success(successMessage);
        afterSuccess?.();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(resolveApiError(err));
      },
    });
  }
}