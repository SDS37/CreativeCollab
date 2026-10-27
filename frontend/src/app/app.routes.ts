import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'creatives' },
  {
    path: 'creatives',
    loadComponent: () =>
      import('./creatives/creative-list-page/creative-list-page').then((m) => m.CreativeListPage),
  },
  {
    path: 'creatives/new',
    loadComponent: () =>
      import('./creatives/creative-edit-page/creative-edit-page').then((m) => m.CreativeEditPage),
  },
  {
    path: 'creatives/:id',
    loadComponent: () =>
      import('./creatives/creative-edit-page/creative-edit-page').then((m) => m.CreativeEditPage),
  },
  { path: '**', redirectTo: 'creatives' },
];
