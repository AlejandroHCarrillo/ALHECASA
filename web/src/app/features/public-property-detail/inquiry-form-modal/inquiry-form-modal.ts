import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { PrivacyNotice } from '../../../shared/components/privacy-notice/privacy-notice';
import { resolveApiError } from '../../../shared/utils/api-error';
import { INQUIRY_LIMITS } from '../../inquiries/inquiry.models';
import { PublicPropertiesService } from '../../landing/public-properties.service';

const PHONE_CHARACTERS = /^\+?[0-9()\-\s.]+$/;

/** Teléfono con solo los caracteres habituales y entre 7 y 15 dígitos (mismas reglas que la API). */
export function phoneValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) {
    return null; // Lo exige `required`.
  }

  const digits = value.replace(/\D/g, '').length;
  return PHONE_CHARACTERS.test(value) && digits >= INQUIRY_LIMITS.minPhoneDigits && digits <= INQUIRY_LIMITS.maxPhoneDigits
    ? null
    : { phone: true };
}

/** Modal para que un visitante pida informes de una publicación. No requiere sesión. */
@Component({
  selector: 'app-inquiry-form-modal',
  imports: [ReactiveFormsModule, Modal, ErrorAlert, PrivacyNotice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inquiry-form-modal.html',
})
export class InquiryFormModal implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly auth = inject(AuthService);

  readonly propertyId = input.required<string>();
  readonly propertyTitle = input.required<string>();
  readonly closed = output<void>();

  protected readonly limits = INQUIRY_LIMITS;
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(INQUIRY_LIMITS.name)]],
    phone: ['', [Validators.required, Validators.maxLength(INQUIRY_LIMITS.phone), phoneValidator]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(INQUIRY_LIMITS.email)]],
    message: ['', [Validators.required, Validators.maxLength(INQUIRY_LIMITS.text)]],
  });

  ngOnInit(): void {
    // Si ya hay sesión, se sugieren nombre y correo; el visitante puede cambiarlos.
    const user = this.auth.user();
    if (user) {
      this.form.patchValue({ name: user.fullName, email: user.email });
    }
  }

  protected hasError(control: 'name' | 'phone' | 'email' | 'message'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.publicProperties
      .requestInfo(this.propertyId(), {
        name: value.name.trim(),
        phone: value.phone.trim(),
        email: value.email.trim(),
        message: value.message.trim(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.sent.set(true);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(this.describe(err));
        },
      });
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) {
        return 'Esta propiedad ya no está disponible.';
      }
      if (err.status === 429) {
        return 'Enviaste muchas solicitudes seguidas. Espera unos minutos e inténtalo de nuevo.';
      }
    }

    return resolveApiError(err);
  }
}