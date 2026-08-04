import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthSession } from './auth.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const loginUrl = '/api/identity/auth/login';
const refreshUrl = '/api/identity/auth/refresh';
const logoutUrl = '/api/identity/auth/logout';

const session: AuthSession = {
  accessToken: 'access-token-1',
  expiresAtUtc: '2026-08-03T02:00:00Z',
  user: {
    id: '624b9ce9-4446-477c-a80d-ad1958d846bc',
    name: 'Gustavo',
    email: 'gustavo@example.com',
    roles: ['Admin']
  }
};

describe('AuthService e authInterceptor', () => {
  let auth: AuthService;
  let http: HttpClient;
  let controller: HttpTestingController;
  let router: Router;
  let locksDescriptor: PropertyDescriptor | undefined;

  beforeEach(() => {
    locksDescriptor = Object.getOwnPropertyDescriptor(navigator, 'locks');
    Object.defineProperty(navigator, 'locks', { configurable: true, value: undefined });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    controller.verify();

    if (locksDescriptor) {
      Object.defineProperty(navigator, 'locks', locksDescriptor);
    } else {
      Reflect.deleteProperty(navigator, 'locks');
    }
  });

  it('nunca envia o bearer para uma URL externa', async () => {
    await authenticate();

    const response = firstValueFrom(http.get('https://external.example/data'));
    const request = controller.expectOne('https://external.example/data');

    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ ok: true });
    await response;
  });

  it('não envia bearer aos endpoints de sessão', async () => {
    await authenticate();

    const refresh = firstValueFrom(auth.refresh());
    const refreshRequest = controller.expectOne(refreshUrl);

    expect(refreshRequest.request.headers.has('Authorization')).toBe(false);
    expect(refreshRequest.request.headers.get('X-Pulso-CSRF')).toBe('1');
    expect(refreshRequest.request.withCredentials).toBe(true);
    refreshRequest.flush({ ...session, accessToken: 'access-token-2' });
    await refresh;

    const logout = firstValueFrom(auth.logout());
    const logoutRequest = controller.expectOne(logoutUrl);

    expect(logoutRequest.request.headers.has('Authorization')).toBe(false);
    expect(logoutRequest.request.headers.get('X-Pulso-CSRF')).toBe('1');
    expect(logoutRequest.request.withCredentials).toBe(true);
    logoutRequest.flush(null);
    await logout;
  });

  it('não tenta renovar novamente quando o próprio refresh recebe 401', async () => {
    await authenticate();

    const refresh = firstValueFrom(auth.refresh());
    const request = controller.expectOne(refreshUrl);
    request.flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(refresh).rejects.toBeInstanceOf(HttpErrorResponse);
    controller.expectNone(refreshUrl);
  });

  it('compartilha uma única renovação entre chamadas concorrentes na mesma aba', async () => {
    const first = firstValueFrom(auth.refresh());
    const second = firstValueFrom(auth.refresh());
    const requests = controller.match(refreshUrl);

    expect(requests).toHaveLength(1);
    requests[0].flush(session);

    await expect(first).resolves.toEqual(session);
    await expect(second).resolves.toEqual(session);
  });

  it('limpa a sessão e redireciona ao login quando o refresh definitivo recebe 401', async () => {
    await authenticate();
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const apiCall = firstValueFrom(http.get('/api/protected-resource'));
    const apiRequest = controller.expectOne('/api/protected-resource');
    expect(apiRequest.request.headers.get('Authorization')).toBe('Bearer access-token-1');
    apiRequest.flush(null, { status: 401, statusText: 'Unauthorized' });

    const refreshRequest = controller.expectOne(refreshUrl);
    refreshRequest.flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(apiCall).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });

  async function authenticate(): Promise<void> {
    const login = firstValueFrom(
      auth.login({ email: 'gustavo@example.com', password: 'not-a-real-password' })
    );
    const request = controller.expectOne(loginUrl);

    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.headers.get('X-Pulso-CSRF')).toBe('1');
    expect(request.request.withCredentials).toBe(true);
    request.flush(session);
    await login;
  }
});
