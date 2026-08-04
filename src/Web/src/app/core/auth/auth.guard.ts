import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.whenInitialized().pipe(
    map(() =>
      auth.isAuthenticated()
        ? true
        : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })
    )
  );
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth
    .whenInitialized()
    .pipe(map(() => (auth.isAuthenticated() ? router.createUrlTree(['/app']) : true)));
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.whenInitialized().pipe(
    map(() => {
      const isAdmin = auth.user()?.roles.some(role => role.toLowerCase() === 'admin');
      return isAdmin ? true : router.createUrlTree(['/app']);
    })
  );
};
