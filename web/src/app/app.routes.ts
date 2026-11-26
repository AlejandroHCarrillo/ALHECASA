import { Routes } from '@angular/router';
import { authGuard, companyGuard, guestGuard, guestMatch, roleGuard } from './core/auth/auth.guards';
import { UserRole } from './core/models/user-role';

export const routes: Routes = [
  // Portada pública: solo coincide sin sesión. Con sesión, `/` sigue a la ruta de abajo y abre el área de trabajo.
  {
    path: '',
    pathMatch: 'full',
    canMatch: [guestMatch],
    title: 'ALHECASA · Propiedades en venta y renta',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing),
  },
  // Detalle de una publicación: público, lo abre cualquiera (con o sin sesión).
  {
    path: 'publicaciones/:id',
    title: 'Publicación · ALHECASA',
    loadComponent: () =>
      import('./features/public-property-detail/public-property-detail').then((m) => m.PublicPropertyDetail),
  },
  // Todas las publicaciones activas de una inmobiliaria: público, como el detalle.
  {
    path: 'inmobiliarias/:id',
    title: 'Publicaciones de la inmobiliaria · ALHECASA',
    loadComponent: () => import('./features/agency-listings/agency-listings').then((m) => m.AgencyListings),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Ingresar · ALHECASA',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'registro',
    canActivate: [guestGuard],
    title: 'Crear cuenta gratis · ALHECASA',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'inicio' },
      {
        path: 'inicio',
        title: 'Inicio · ALHECASA',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        path: 'propiedades',
        canActivate: [roleGuard(UserRole.PlatformAdmin, UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller)],
        title: 'Propiedades · ALHECASA',
        loadChildren: () => import('./features/properties/properties.routes').then((m) => m.PROPERTIES_ROUTES),
      },
      {
        path: 'galeria',
        canActivate: [roleGuard(UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller)],
        title: 'Galería · ALHECASA',
        loadComponent: () => import('./features/gallery/gallery').then((m) => m.Gallery),
      },
      {
        path: 'solicitudes',
        canActivate: [roleGuard(UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller)],
        title: 'Solicitudes de informes · ALHECASA',
        loadChildren: () => import('./features/inquiries/inquiries.routes').then((m) => m.INQUIRIES_ROUTES),
      },
      {
        path: 'usuarios',
        canActivate: [roleGuard(UserRole.PlatformAdmin, UserRole.AgencyAdmin), companyGuard],
        title: 'Usuarios · ALHECASA',
        loadChildren: () => import('./features/users/users.routes').then((m) => m.USERS_ROUTES),
      },
      {
        path: 'mi-plan',
        canActivate: [roleGuard(UserRole.AgencyAdmin)],
        title: 'Mi plan · ALHECASA',
        loadChildren: () => import('./features/billing/billing.routes').then((m) => m.BILLING_ROUTES),
      },
      {
        path: 'planes',
        canActivate: [roleGuard(UserRole.PlatformAdmin)],
        title: 'Planes · ALHECASA',
        loadChildren: () => import('./features/plans/plans.routes').then((m) => m.PLANS_ROUTES),
      },
      {
        path: 'suscripciones',
        canActivate: [roleGuard(UserRole.PlatformAdmin)],
        title: 'Suscripciones · ALHECASA',
        loadChildren: () => import('./features/subscriptions/subscriptions.routes').then((m) => m.SUBSCRIPTIONS_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
