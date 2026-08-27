import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    title: 'Pulso I/O — A saúde das suas integrações',
    loadComponent: () =>
      import('./features/landing/landing.component').then(module => module.LandingComponent)
  },
  {
    path: 'login',
    title: 'Entrar — Pulso I/O',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login.component').then(module => module.LoginComponent)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/admin/admin-shell.component').then(module => module.AdminShellComponent),
    children: [
      {
        path: '',
        title: 'Visão geral — Pulso I/O',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then(
            module => module.DashboardComponent
          )
      },
      {
        path: 'users',
        title: 'Usuários — Pulso I/O',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/users/users.component').then(module => module.UsersComponent)
      },
      {
        path: 'clients',
        title: 'Clientes — Pulso I/O',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/clients/clients.component').then(module => module.ClientsComponent)
      },
      {
        path: 'profile',
        title: 'Meu perfil — Pulso I/O',
        loadComponent: () =>
          import('./features/profile/profile.component').then(module => module.ProfileComponent)
      },
      {
        path: 'settings',
        title: 'Configurações — Pulso I/O',
        loadComponent: () =>
          import('./features/settings/settings.component').then(module => module.SettingsComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
