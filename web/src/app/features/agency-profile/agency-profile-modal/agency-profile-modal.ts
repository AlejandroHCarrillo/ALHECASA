import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Observable, of, switchMap } from 'rxjs';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { PrivacyNotice } from '../../../shared/components/privacy-notice/privacy-notice';
import { resolveApiError } from '../../../shared/utils/api-error';
import { phoneValidator } from '../../public-property-detail/inquiry-form-modal/inquiry-form-modal';
import {
  AGENCY_PROFILE_LIMITS,
  AgencyProfile,
  LOGO_LIMITS,
  UpdateAgencyProfileRequest,
  agencyLogoUrl,
} from '../agency-profile.models';
import { AgencyProfileService } from '../agency-profile.service';

/** Vacío es válido (el campo es opcional); si hay texto, debe ser una dirección http o https, como exige la API. */
export function webAddressValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) {
    return null;
  }

  try {
    const url = new URL(value);
    return (url.protocol === 'http:' || url.protocol === 'https:') && !!url.hostname ? null : { webAddress: true };
  } catch {
    return { webAddress: true };
  }
}

/** Validators.required da por bueno un texto de puros espacios, que la API rechaza al recortarlo. */
export function notBlankValidator(control: AbstractControl<string>): ValidationErrors | null {
  return (control.value ?? '').trim() ? null : { required: true };
}

type TextField = keyof UpdateAgencyProfileRequest;

/** Logotipo elegido en el equipo que todavía no se sube. */
interface StagedLogo {
  file: File;
  preview: string;
}

/**
 * Modal con el que el administrador de la inmobiliaria configura su información pública: datos de contacto, descripción,
 * enlaces, horario, dirección y logotipo. Guarda los datos y después, si cambió, el logotipo.
 */
@Component({
  selector: 'app-agency-profile-modal',
  imports: [ReactiveFormsModule, Modal, ErrorAlert, PrivacyNotice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agency-profile-modal.html',
})
export class AgencyProfileModal implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly service = inject(AgencyProfileService);

  readonly profile = input.required<AgencyProfile>();
  readonly saved = output<AgencyProfile>();
  readonly cancelled = output<void>();

  protected readonly limits = AGENCY_PROFILE_LIMITS;
  protected readonly acceptTypes = LOGO_LIMITS.acceptedTypes.join(',');
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  /** El logotipo guardado se muestra mientras no se quite ni se reemplace. */
  protected readonly savedLogoUrl = signal<string | null>(null);
  protected readonly staged = signal<StagedLogo | null>(null);
  protected readonly removeLogo = signal(false);
  protected readonly logoProblem = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [notBlankValidator, Validators.maxLength(AGENCY_PROFILE_LIMITS.name)]],
    email: ['', [notBlankValidator, Validators.email, Validators.maxLength(AGENCY_PROFILE_LIMITS.email)]],
    phone: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.phone), phoneValidator]],
    description: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.description)]],
    website: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.url), webAddressValidator]],
    facebookUrl: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.url), webAddressValidator]],
    instagramUrl: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.url), webAddressValidator]],
    openingHours: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.openingHours)]],
    street: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.street)]],
    city: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.city)]],
    state: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.state)]],
    postalCode: ['', [Validators.maxLength(AGENCY_PROFILE_LIMITS.postalCode)]],
  });

  ngOnInit(): void {
    const p = this.profile();
    this.form.setValue({
      name: p.name,
      email: p.email,
      phone: p.phone ?? '',
      description: p.description ?? '',
      website: p.website ?? '',
      facebookUrl: p.facebookUrl ?? '',
      instagramUrl: p.instagramUrl ?? '',
      openingHours: p.openingHours ?? '',
      street: p.street ?? '',
      city: p.city ?? '',
      state: p.state ?? '',
      postalCode: p.postalCode ?? '',
    });
    this.savedLogoUrl.set(p.hasLogo ? agencyLogoUrl(p.id, Date.now()) : null);
  }

  ngOnDestroy(): void {
    this.clearStaged();
  }

  protected hasError(control: TextField): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  /** Lo que se muestra como logotipo ahora: el recién elegido, el guardado, o ninguno. */
  protected currentLogo(): string | null {
    return this.staged()?.preview || (this.removeLogo() ? null : this.savedLogoUrl());
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permite elegir de nuevo el mismo archivo
    if (!file) {
      return;
    }

    if (!(LOGO_LIMITS.acceptedTypes as readonly string[]).includes(file.type)) {
      this.logoProblem.set(`"${file.name}" no es una imagen JPG, PNG o WebP.`);
      return;
    }

    if (file.size > LOGO_LIMITS.maxBytes) {
      this.logoProblem.set(`"${file.name}" pesa más de ${LOGO_LIMITS.maxBytes / (1024 * 1024)} MB.`);
      return;
    }

    this.logoProblem.set(null);
    this.clearStaged();
    this.staged.set({ file, preview: typeof URL.createObjectURL === 'function' ? URL.createObjectURL(file) : '' });
    this.removeLogo.set(false);
  }

  protected discardLogo(): void {
    this.logoProblem.set(null);
    if (this.staged()) {
      this.clearStaged();
      return;
    }

    this.removeLogo.set(true);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.service
      .update(this.toRequest())
      .pipe(switchMap((updated) => this.applyLogo(updated)))
      .subscribe({
        next: (profile) => {
          this.submitting.set(false);
          this.saved.emit(profile);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(resolveApiError(err));
        },
      });
  }

  private applyLogo(updated: AgencyProfile): Observable<AgencyProfile> {
    const staged = this.staged();
    if (staged) {
      return this.service.setLogo(staged.file);
    }

    return this.removeLogo() && updated.hasLogo ? this.service.removeLogo() : of(updated);
  }

  private toRequest(): UpdateAgencyProfileRequest {
    const v = this.form.getRawValue();
    const optional = (text: string): string | null => text.trim() || null;
    return {
      name: v.name.trim(),
      email: v.email.trim(),
      phone: optional(v.phone),
      description: optional(v.description),
      website: optional(v.website),
      facebookUrl: optional(v.facebookUrl),
      instagramUrl: optional(v.instagramUrl),
      openingHours: optional(v.openingHours),
      street: optional(v.street),
      city: optional(v.city),
      state: optional(v.state),
      postalCode: optional(v.postalCode),
    };
  }

  private clearStaged(): void {
    const staged = this.staged();
    if (staged?.preview && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(staged.preview);
    }

    this.staged.set(null);
  }
}