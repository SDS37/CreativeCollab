import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';

const editPage = () =>
  import('./creatives/creative-edit-page/creative-edit-page').then((m) => m.CreativeEditPage);

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'creatives' },
  { path: 'sign-in', loadComponent: () => import('./auth/sign-in-page/sign-in-page').then((m) => m.SignInPage) },
  { path: 'register', loadComponent: () => import('./auth/register-page/register-page').then((m) => m.RegisterPage) },
  {
    path: 'creatives',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./creatives/creative-list-page/creative-list-page').then((m) => m.CreativeListPage),
  },
  // Declared before ':id' so "new" is not read as an id. Creating needs Editor; opening one to read does not.
  { path: 'creatives/new', canActivate: [roleGuard('Editor')], loadComponent: editPage },
  { path: 'creatives/:id', canActivate: [authGuard], loadComponent: editPage },
  { path: '**', redirectTo: 'creatives' },
];
