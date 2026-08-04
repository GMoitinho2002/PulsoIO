import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, timeout } from 'rxjs';

interface HealthResponse {
  status: string;
}

export type ApiHealthState =
  | { state: 'checking' }
  | { state: 'online' | 'degraded'; latencyMs: number; checkedAt: string }
  | { state: 'offline'; checkedAt: string };

@Injectable({ providedIn: 'root' })
export class ApiHealthService {
  private readonly http = inject(HttpClient);

  check(): Observable<ApiHealthState> {
    const startedAt = performance.now();

    return this.http.get<HealthResponse>('/health').pipe(
      timeout(5000),
      map((response): ApiHealthState => ({
        state: response.status === 'healthy' ? 'online' : 'degraded',
        latencyMs: Math.max(1, Math.round(performance.now() - startedAt)),
        checkedAt: this.formatTime()
      })),
      catchError(() => of<ApiHealthState>({ state: 'offline', checkedAt: this.formatTime() }))
    );
  }

  private formatTime(): string {
    return new Intl.DateTimeFormat('pt-BR', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    }).format(new Date());
  }
}
