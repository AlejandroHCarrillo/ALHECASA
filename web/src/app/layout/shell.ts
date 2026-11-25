import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { USER_ROLE_LABELS, UserRole } from '../core/models/user-role';
import { Logo } from '../shared/components/logo/logo';

interface MenuItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
  /** Si se omite, lo ve cualquier usuario autenticado. */
  roles?: UserRole[];
  /** No se muestra en la cuenta gratuita de un particular (tiene un solo usuario). */
  companyOnly?: boolean;
}

const MENU_GROUPS: MenuItem[][] = [
  // Operación diaria
  [
    { label: 'Inicio', route: '/inicio', icon: 'fa-house', exact: true },
    {
      label: 'Propiedades',
      route: '/propiedades',
      icon: 'fa-building',
      roles: [UserRole.PlatformAdmin, UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller],
    },
    {
      // Solo personal de una inmobiliaria: el administrador de plataforma no tiene propiedades propias que mostrar.
      label: 'Galería',
      route: '/galeria',
      icon: 'fa-images',
      roles: [UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller],
    },
    {
      // Prospectos que piden informes de las propiedades de la inmobiliaria.
      label: 'Solicitudes de informes',
      route: '/solicitudes',
      icon: 'fa-envelope',
      roles: [UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller],
    },
  ],
  // Administración
  [
    {
      label: 'Usuarios',
      route: '/usuarios',
      icon: 'fa-users',
      roles: [UserRole.PlatformAdmin, UserRole.AgencyAdmin],
      companyOnly: true,
    },
  ],
];

const COLLAPSED_KEY = 'alhecasa.menu.collapsed';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
})
export class Shell {
  protected readonly auth = inject(AuthService);

  protected readonly collapsed = signal(localStorage.getItem(COLLAPSED_KEY) === 'true');

  // Cada ítem solo aparece si el usuario puede abrir esa ruta (misma regla que el roleGuard).
  protected readonly groups = computed(() =>
    MENU_GROUPS.map((group) =>
      group.filter(
        (item) => (!item.roles || this.auth.hasRole(...item.roles)) && !(item.companyOnly && this.auth.user()?.isIndividual),
      ),
    ).filter((group) => group.length > 0),
  );

  protected readonly roleLabel = computed(() => {
    const user = this.auth.user();
    if (user?.isIndividual) {
      return 'Cuenta gratuita';
    }

    return user ? USER_ROLE_LABELS[user.role] : '';
  });

  protected toggleCollapsed(): void {
    this.collapsed.update((value) => !value);
    localStorage.setItem(COLLAPSED_KEY, String(this.collapsed()));
  }

  protected logout(): void {
    this.auth.logout();
  }
}
