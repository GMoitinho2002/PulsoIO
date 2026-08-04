import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const hasRetried = new HttpContextToken<boolean>(() => false);
const authSessionEndpoints = [
  '/api/identity/auth/login',
  '/api/identity/auth/refresh',
  '/api/identity/auth/logout'
];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const accessToken = auth.accessToken();
  const isOwnApiRequest = request.url.startsWith('/api/');
  const managesSession = authSessionEndpoints.some(endpoint => request.url.startsWith(endpoint));

  if (!accessToken || !isOwnApiRequest || managesSession) {
    return next(request);
  }

  const authenticatedRequest = request.clone({
    setHeaders: { Authorization: `Bearer ${accessToken}` }
  });

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        request.context.get(hasRetried)
      ) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(refreshError => {
          auth.clearSession();

          if (refreshError instanceof HttpErrorResponse && refreshError.status === 401) {
            redirectToLogin(router);
          }

          return throwError(() => refreshError);
        }),
        switchMap(session =>
          next(
            request.clone({
              context: request.context.set(hasRetried, true),
              setHeaders: { Authorization: `Bearer ${session.accessToken}` }
            })
          ).pipe(
            catchError(retryError => {
              if (retryError instanceof HttpErrorResponse && retryError.status === 401) {
                auth.clearSession();
                redirectToLogin(router);
              }

              return throwError(() => retryError);
            })
          )
        )
      );
    })
  );
};

function redirectToLogin(router: Router): void {
  if (!router.url.startsWith('/login')) {
    void router.navigate(['/login'], { replaceUrl: true });
  }
}
