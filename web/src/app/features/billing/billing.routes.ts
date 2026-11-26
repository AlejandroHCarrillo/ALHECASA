import { Routes } from '@angular/router';

export const BILLING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./my-plan/my-plan').then((m) => m.MyPlan),
  },
];
