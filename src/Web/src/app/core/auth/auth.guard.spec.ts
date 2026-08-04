import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter
} from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthSession } from './auth.models';
import { adminGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('adminGuard', () => {
  let auth: AuthService;
  let controller: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });

    auth = TestBed.inject(AuthService);
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => controller.verify());

  it('permite a rota para usuário com papel Admin', async () => {
    await restoreSession(['Admin']);

    expect(await evaluateGuard()).toBe(true);
  });

  it('redireciona usuário autenticado sem papel Admin', async () => {
    await restoreSession(['Viewer']);

    const result = await evaluateGuard();
    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/app');
  });

  async function restoreSession(roles: string[]): Promise<void> {
    const restoration = firstValueFrom(auth.restoreSession());
    const request = controller.expectOne('/api/identity/auth/refresh');
    const restoredSession: AuthSession = {
      accessToken: 'restored-access-token',
      expiresAtUtc: '2026-08-03T02:00:00Z',
      user: {
        id: '624b9ce9-4446-477c-a80d-ad1958d846bc',
        name: 'Gustavo',
        email: 'gustavo@example.com',
        roles
      }
    };

    request.flush(restoredSession);
    await restoration;
  }

  function evaluateGuard(): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    );

    return firstValueFrom(result as Observable<boolean | UrlTree>);
  }
});
