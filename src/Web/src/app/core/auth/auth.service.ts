import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  Observable,
  ReplaySubject,
  catchError,
  defer,
  finalize,
  firstValueFrom,
  from,
  map,
  of,
  shareReplay,
  switchMap,
  take,
  tap,
  timeout
} from 'rxjs';
import { AuthSession, AuthUser, LoginRequest } from './auth.models';

const authUrl = '/api/identity/auth';
const csrfHeaders = new HttpHeaders({ 'X-Pulso-CSRF': '1' });

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly sessionState = signal<AuthSession | null>(null);
  private readonly initializedState = signal(false);
  private readonly initializedSubject = new ReplaySubject<void>(1);
  private refreshInFlight: Observable<AuthSession> | null = null;

  readonly session = this.sessionState.asReadonly();
  readonly user = computed(() => this.sessionState()?.user ?? null);
  readonly accessToken = computed(() => this.sessionState()?.accessToken ?? null);
  readonly isAuthenticated = computed(() => this.sessionState() !== null);
  readonly isInitialized = this.initializedState.asReadonly();

  restoreSession(): Observable<void> {
    if (this.initializedState()) {
      return of(undefined);
    }

    return this.refresh().pipe(
      map(() => undefined),
      timeout(8000),
      catchError(() => {
        this.sessionState.set(null);
        return of(undefined);
      }),
      finalize(() => {
        this.initializedState.set(true);
        this.initializedSubject.next();
        this.initializedSubject.complete();
      })
    );
  }

  whenInitialized(): Observable<void> {
    return this.initializedState() ? of(undefined) : this.initializedSubject.pipe(take(1));
  }

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${authUrl}/login`, request, {
        headers: csrfHeaders,
        withCredentials: true
      })
      .pipe(tap(session => this.sessionState.set(session)));
  }

  refresh(): Observable<AuthSession> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const request = defer(() => this.requestRefresh())
      .pipe(
        tap(session => this.sessionState.set(session)),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: true })
      );

    this.refreshInFlight = request;
    return request;
  }

  private requestRefresh(): Observable<AuthSession> {
    const sendRequest = () =>
      this.http.post<AuthSession>(`${authUrl}/refresh`, null, {
        headers: csrfHeaders,
        withCredentials: true
      });

    if (typeof navigator === 'undefined' || !navigator.locks) {
      return sendRequest();
    }

    return from(
      navigator.locks.request('pulso-io-auth-refresh', () => firstValueFrom(sendRequest()))
    );
  }

  logout(): Observable<void> {
    const pendingRefresh = this.refreshInFlight
      ? this.refreshInFlight.pipe(
          catchError(() => of(null)),
          map(() => undefined)
        )
      : of(undefined);

    return pendingRefresh.pipe(
      switchMap(() =>
        this.http.post<void>(`${authUrl}/logout`, null, {
          headers: csrfHeaders,
          withCredentials: true
        })
      ),
      finalize(() => this.sessionState.set(null))
    );
  }

  loadCurrentUser(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${authUrl}/me`, { withCredentials: true }).pipe(
      tap(user => {
        const current = this.sessionState();

        if (current) {
          this.sessionState.set({ ...current, user });
        }
      })
    );
  }

  clearSession(): void {
    this.sessionState.set(null);
  }
}
