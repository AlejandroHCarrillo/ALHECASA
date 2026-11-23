import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { USER_ROLE_LABELS, UserRole } from '../../../core/models/user-role';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { resolveApiError } from '../../../shared/utils/api-error';
import { Agency, User } from '../user.models';
import { UsersService } from '../users.service';

/** Modal compartido para crear (user = null) y editar un usuario. */
@Component({
  selector: 'app-user-form-modal',
  imports: [ReactiveFormsModule, Modal, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-form-modal.html',
})
export class UserFormModal implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly usersService = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly user = input<User | null>(null);
  readonly agencies = input<Agency[]>([]);
  readonly saved = output<string>();
  readonly cancelled = output<void>();

  protected readonly roleLabels = USER_ROLE_LABELS;
  protected readonly isEdit = computed(() => this.user() !== null);
  protected readonly isPlatformAdmin = computed(() => this.auth.hasRole(UserRole.PlatformAdmin));
  protected readonly isSelf = computed(() => this.user()?.id === this.auth.user()?.id);

  /** Un AgencyAdmin no puede asignar el rol PlatformAdmin. */
  protected readonly roleOptions = computed<UserRole[]>(() =>
    this.isPlatformAdmin()
      ? [UserRole.PlatformAdmin, UserRole.AgencyAdmin, UserRole.Client]
      : [UserRole.AgencyAdmin, UserRole.Client],
  );

  protected readonly selectedRole = signal<UserRole>(UserRole.Client);

  /** La inmobiliaria solo se elige si quien captura es PlatformAdmin y el rol destino pertenece a una inmobiliaria. */
  protected readonly showAgency = computed(
    () => this.isPlatformAdmin() && this.selectedRole() !== UserRole.PlatformAdmin,
  );

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(10),
        Validators.maxLength(128),
        Validators.pattern(/[A-Z]/),
        Validators.pattern(/[a-z]/),
        Validators.pattern(/[0-9]/),
      ],
    ],
    role: [UserRole.Client as UserRole, [Validators.required]],
    agencyId: [''],
    isActive: [true],
  });

  constructor() {
    this.form.controls.role.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((role) => {
      this.selectedRole.set(role);
      this.applyAgencyRules(role);
    });
  }

  ngOnInit(): void {
    const user = this.user();

    if (user) {
      this.form.patchValue({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        role: user.role,
        agencyId: user.agencyId ?? '',
        isActive: user.isActive,
      });
      // La contraseña no se edita aquí.
      this.form.controls.password.disable();

      // Backend: nadie puede cambiar su propio rol ni desactivarse.
      if (this.isSelf()) {
        this.form.controls.role.disable();
        this.form.controls.isActive.disable();
      }
    } else {
      this.form.patchValue({ role: this.roleOptions().at(-1) ?? UserRole.Client });
    }

    this.selectedRole.set(this.form.controls.role.value);
    this.applyAgencyRules(this.form.controls.role.value);
  }

  protected hasError(control: 'firstName' | 'lastName' | 'email' | 'password' | 'agencyId'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const agencyId = this.showAgency() && value.agencyId ? value.agencyId : null;
    const user = this.user();

    let request$: Observable<User>;
    if (user) {
      request$ = this.usersService.update(user.id, {
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        role: value.role,
        agencyId,
        isActive: value.isActive,
      });
    } else {
      request$ = this.usersService.create({
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        password: value.password,
        role: value.role,
        agencyId,
      });
    }

    this.submitting.set(true);
    this.error.set(null);

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        this.saved.emit(user ? 'Usuario actualizado correctamente.' : 'Usuario creado correctamente.');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(resolveApiError(err));
      },
    });
  }

  /** AgencyAdmin exige inmobiliaria; Client puede ser particular; PlatformAdmin no pertenece a ninguna. */
  private applyAgencyRules(role: UserRole): void {
    const agency = this.form.controls.agencyId;
    agency.clearValidators();

    if (!this.isPlatformAdmin() || role === UserRole.PlatformAdmin) {
      agency.setValue('');
    } else if (role === UserRole.AgencyAdmin) {
      agency.addValidators(Validators.required);
    }

    agency.updateValueAndValidity();
  }
}
