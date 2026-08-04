import { adminGuard, authGuard } from './core/auth/auth.guard';
import { routes } from './app.routes';

describe('rotas administrativas', () => {
  it('mantém a visão geral autenticada e restringe somente usuários ao Admin', () => {
    const appRoute = routes.find(route => route.path === 'app');
    const usersRoute = appRoute?.children?.find(route => route.path === 'users');

    expect(appRoute?.canActivate).toEqual([authGuard]);
    expect(usersRoute?.canActivate).toEqual([adminGuard]);
  });
});
