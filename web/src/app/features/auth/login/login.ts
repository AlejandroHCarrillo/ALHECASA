import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Logo } from '../../../shared/components/logo/logo';
import { resolveApiError } from '../../../shared/utils/api-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, ErrorAlert, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
})
export class Login {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigateByUrl(this.resolveReturnUrl()),
      error: (err: unknown) => {
        this.submitting.set(false);
        // El 401 no distingue entre correo inexistente, cuenta inactiva o contraseña incorrecta.
        this.error.set(
          err instanceof HttpErrorResponse && err.status === 401
            ? 'Correo o contraseña incorrectos.'
            : resolveApiError(err),
        );
      },
    });
  }

  /** Solo se aceptan regresos internos ("/ruta"); se rechazan "//host" para evitar redirecciones abiertas. */
  private resolveReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/inicio';
  }
}
