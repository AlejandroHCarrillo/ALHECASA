import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { USER_ROLE_LABELS } from '../../core/models/user-role';

@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card bg-base-100 shadow">
      <div class="card-body">
        <h1 class="card-title text-2xl">Bienvenido, {{ auth.user()?.fullName }}</h1>
        <p class="text-base-content/70">
          Plataforma de gestión de compra, venta y renta de bienes raíces.
        </p>
        @if (auth.user(); as user) {
          <dl class="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
            <div>
              <dt class="text-xs uppercase text-base-content/60">Correo</dt>
              <dd>{{ user.email }}</dd>
            </div>
            <div>
              <dt class="text-xs uppercase text-base-content/60">Rol</dt>
              <dd>{{ user.isIndividual ? 'Particular' : roleLabels[user.role] }}</dd>
            </div>
            @if (user.isIndividual) {
              <div>
                <dt class="text-xs uppercase text-base-content/60">Cuenta</dt>
                <dd>Cuenta gratuita de particular</dd>
              </div>
            } @else {
              <div>
                <dt class="text-xs uppercase text-base-content/60">Inmobiliaria</dt>
                <dd>{{ user.agencyName ?? 'Sin inmobiliaria' }}</dd>
              </div>
            }
          </dl>
        }
      </div>
    </div>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly roleLabels = USER_ROLE_LABELS;
}
