import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { USER_ROLE_LABELS } from '../../../core/models/user-role';
import { Modal } from '../../../shared/components/modal/modal';
import { User } from '../user.models';

/** Modal de solo lectura con los datos del usuario. */
@Component({
  selector: 'app-user-detail-modal',
  imports: [Modal, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal title="Detalle del usuario" (closed)="closed.emit()">
      <dl class="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
        <div>
          <dt class="text-xs uppercase text-base-content/60">Nombre</dt>
          <dd>{{ user().firstName }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Apellidos</dt>
          <dd>{{ user().lastName }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Correo</dt>
          <dd class="break-all">{{ user().email }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Rol</dt>
          <dd>{{ roleLabels[user().role] }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Inmobiliaria</dt>
          <dd>{{ user().agencyName ?? 'Sin inmobiliaria (particular)' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Estado</dt>
          <dd>{{ user().isActive ? 'Activo' : 'Inactivo' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Alta</dt>
          <dd>{{ user().createdAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Último acceso</dt>
          <dd>{{ user().lastLoginAt ? (user().lastLoginAt | date: 'dd/MM/yyyy HH:mm') : 'Nunca' }}</dd>
        </div>
      </dl>

      <div class="modal-action">
        <button type="button" class="btn" (click)="closed.emit()">Cerrar</button>
        <button type="button" class="btn btn-warning" (click)="edit.emit()">
          <i class="fa-solid fa-pen" aria-hidden="true"></i>
          Editar
        </button>
      </div>
    </app-modal>
  `,
})
export class UserDetailModal {
  readonly user = input.required<User>();
  readonly closed = output<void>();
  readonly edit = output<void>();

  protected readonly roleLabels = USER_ROLE_LABELS;
}
