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
  ],
  // Administración
  [
    {
      label: 'Usuarios',
      route: '/usuarios',
      icon: 'fa-users',
      roles: [UserRole.PlatformAdmin, UserRole.AgencyAdmin],
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
      group.filter((item) => !item.roles || this.auth.hasRole(...item.roles)),
    ).filter((group) => group.length > 0),
  );

  protected readonly roleLabel = computed(() => {
    const role = this.auth.user()?.role;
    return role ? USER_ROLE_LABELS[role] : '';
  });

  protected toggleCollapsed(): void {
    this.collapsed.update((value) => !value);
    localStorage.setItem(COLLAPSED_KEY, String(this.collapsed()));
  }

  protected logout(): void {
    this.auth.logout();
  }
}
