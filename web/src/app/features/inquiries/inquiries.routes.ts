import { Routes } from '@angular/router';

export const INQUIRIES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./inquiry-list/inquiry-list').then((m) => m.InquiryList),
  },
];