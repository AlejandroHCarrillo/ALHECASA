import { Routes } from '@angular/router';

export const PROPERTIES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./property-list/property-list').then((m) => m.PropertyList),
  },
];
