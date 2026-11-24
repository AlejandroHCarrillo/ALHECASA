import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { UserRole } from '../models/user-role';
import { AuthService } from './auth.service';

/** Exige sesión; si no hay, envía a /login conservando la ruta pedida. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Solo visitantes: si ya hay sesión, va al inicio. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? router.createUrlTree(['/inicio']) : true;
};

/**
 * Solo para `canMatch`: deja que la ruta coincida únicamente si no hay sesión. Con sesión, la ruta no coincide
 * y el enrutador sigue con la siguiente (por ejemplo, `/` abre el área de trabajo en lugar de la portada).
 */
export const guestMatch: CanMatchFn = () => !inject(AuthService).isAuthenticated();

/** Exige alguno de los roles indicados. */
export const roleGuard =
  (...roles: UserRole[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    return auth.hasRole(...roles) ? true : router.createUrlTree(['/inicio']);
  };
