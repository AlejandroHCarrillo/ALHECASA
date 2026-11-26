import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Logo } from '../../../shared/components/logo/logo';
import { PrivacyNotice } from '../../../shared/components/privacy-notice/privacy-notice';
import { resolveApiError } from '../../../shared/utils/api-error';

/** Mismas reglas que la API (StrongPassword): de 10 a 128 caracteres, con mayúscula, minúscula y número. */
const PASSWORD_RULES = [Validators.required, Validators.minLength(10), Validators.maxLength(128)];

function strongPassword(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  return /[A-Z]/.test(value) && /[a-z]/.test(value) && /[0-9]/.test(value) ? null : { weak: true };
}

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value as string | undefined;
  const confirm = group.get('confirmPassword')?.value as string | undefined;
  return password === confirm ? null : { mismatch: true };
}

/** Alta pública de una cuenta gratuita de particular para publicar su anuncio. */
@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, ErrorAlert, Logo, PrivacyNotice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.html',
})
export class Register {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = this.fb.group(
    {
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [...PASSWORD_RULES, strongPassword]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected hasError(control: 'firstName' | 'lastName' | 'email' | 'password' | 'confirmPassword'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected mismatch(): boolean {
    return this.form.controls.confirmPassword.touched && this.form.hasError('mismatch');
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { firstName, lastName, email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.auth.register({ firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim(), password }).subscribe({
      // La cuenta ya trae sesión: se va directo a las propiedades para publicar la primera.
      next: () => void this.router.navigateByUrl('/propiedades'),
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(
          err instanceof HttpErrorResponse && err.status === 429
            ? 'Se crearon demasiadas cuentas desde esta conexión. Inténtalo más tarde.'
            : resolveApiError(err),
        );
      },
    });
  }
}