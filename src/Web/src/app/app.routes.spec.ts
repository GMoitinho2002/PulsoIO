import { adminGuard, authGuard } from './core/auth/auth.guard';
import { routes } from './app.routes';

describe('rotas administrativas', () => {
  it('mantém o workspace autenticado e restringe usuários e clientes ao Admin', () => {
    const appRoute = routes.find(route => route.path === 'app');
    const usersRoute = appRoute?.children?.find(route => route.path === 'users');
    const clientsRoute = appRoute?.children?.find(route => route.path === 'clients');
    const profileRoute = appRoute?.children?.find(route => route.path === 'profile');

    expect(appRoute?.canActivate).toEqual([authGuard]);
    expect(usersRoute?.canActivate).toEqual([adminGuard]);
    expect(clientsRoute?.canActivate).toEqual([adminGuard]);
    expect(profileRoute?.canActivate).toBeUndefined();
    expect(clientsRoute?.loadComponent).toBeDefined();
    expect(profileRoute?.loadComponent).toBeDefined();
  });
});
