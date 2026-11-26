import { Routes } from '@angular/router';

export const SUBSCRIPTIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./subscription-list/subscription-list').then((m) => m.SubscriptionList),
  },
];
