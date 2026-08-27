import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AdministrationOverview {
  totalClients: number;
  activeClients: number;
  totalEnvironments: number;
  activeEnvironments: number;
  totalIntegrations: number;
  activeIntegrations: number;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  loadOverview(): Observable<AdministrationOverview> {
    return this.http.get<AdministrationOverview>('/api/administration/overview');
  }
}
