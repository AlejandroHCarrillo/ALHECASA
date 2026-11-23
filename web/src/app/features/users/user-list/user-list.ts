import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { USER_ROLE_BADGES, USER_ROLE_LABELS, UserRole } from '../../../core/models/user-role';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { Paginator } from '../../../shared/components/paginator/paginator';
import { PagedResult } from '../../../shared/models/paged-result';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { UserDetailModal } from '../user-detail-modal/user-detail-modal';
import { UserFormModal } from '../user-form-modal/user-form-modal';
import { Agency, User } from '../user.models';
import { UsersService } from '../users.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-user-list',
  imports: [ReactiveFormsModule, ErrorAlert, Modal, Paginator, UserFormModal, UserDetailModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-list.html',
})
export class UserList implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly roleLabels = USER_ROLE_LABELS;
  protected readonly roleBadges = USER_ROLE_BADGES;

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly result = signal<PagedResult<User> | null>(null);
  protected readonly loading = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly agencies = signal<Agency[]>([]);

  // Estado de los modales
  protected readonly showForm = signal(false);
  protected readonly formUser = signal<User | null>(null);
  protected readonly showDetail = signal(false);
  protected readonly detailUser = signal<User | null>(null);
  protected readonly deleteTarget = signal<User | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta respuestas viejas si el usuario sigue escribiendo o cambiando de página.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.listError.set(null);
        }),
        switchMap(() =>
          this.usersService.getPaged(this.searchControl.value, this.page, PAGE_SIZE).pipe(
            catchError((err: unknown) => {
              this.listError.set(resolveApiError(err));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
        }
      });

    // Búsqueda con debounce de 300 ms.
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.page = 1;
        this.reload$.next();
      });
  }

  ngOnInit(): void {
    this.reload$.next();

    // Solo el administrador de plataforma elige inmobiliaria; la lista alimenta el formulario.
    if (this.auth.hasRole(UserRole.PlatformAdmin)) {
      this.usersService.getAgencies().subscribe({
        next: (agencies) => this.agencies.set(agencies),
        error: (err: unknown) => this.listError.set(resolveApiError(err)),
      });
    }
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
  }

  // --- Detalle (solo lectura) ---
  protected openDetail(user: User): void {
    this.detailUser.set(user);
    this.showDetail.set(true);
  }

  protected closeDetail(): void {
    this.showDetail.set(false);
    this.detailUser.set(null);
  }

  protected editFromDetail(): void {
    const user = this.detailUser();
    this.closeDetail();
    if (user) {
      this.openForm(user);
    }
  }

  // --- Alta / edición ---
  protected openForm(user: User | null): void {
    this.formUser.set(user);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
    this.formUser.set(null);
  }

  protected onSaved(message: string): void {
    const wasCreate = this.formUser() === null;
    this.closeForm();
    this.toast.success(message);
    if (wasCreate) {
      this.page = 1;
    }
    this.reload$.next();
  }

  // --- Eliminación (lógica) ---
  protected askDelete(user: User): void {
    this.deleteError.set(null);
    this.deleteTarget.set(user);
  }

  protected cancelDelete(): void {
    this.deleteTarget.set(null);
    this.deleteError.set(null);
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.usersService.delete(target.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteTarget.set(null);
        this.toast.success('Usuario eliminado correctamente.');

        // Si se eliminó el último registro de la página, regresa a la anterior.
        if (this.result()?.items.length === 1 && this.page > 1) {
          this.page--;
        }
        this.reload$.next();
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.deleteError.set(resolveApiError(err));
      },
    });
  }

  protected isSelf(user: User): boolean {
    return user.id === this.auth.user()?.id;
  }
}
