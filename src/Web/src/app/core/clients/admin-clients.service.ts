import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ClientDetail,
  ClientEnvironment,
  ClientIntegration,
  ClientSummary,
  SaveClientRequest,
  SaveEnvironmentRequest,
  SaveIntegrationRequest
} from './client.models';

const clientsUrl = '/api/administration/clients';

@Injectable({ providedIn: 'root' })
export class AdminClientsService {
  private readonly http = inject(HttpClient);

  list(): Observable<ClientSummary[]> {
    return this.http.get<ClientSummary[]>(clientsUrl);
  }

  get(id: string): Observable<ClientDetail> {
    return this.http.get<ClientDetail>(`${clientsUrl}/${encodeURIComponent(id)}`);
  }

  create(request: SaveClientRequest): Observable<ClientDetail> {
    return this.http.post<ClientDetail>(clientsUrl, request);
  }

  update(id: string, request: SaveClientRequest): Observable<ClientDetail> {
    return this.http.put<ClientDetail>(`${clientsUrl}/${encodeURIComponent(id)}`, request);
  }

  createEnvironment(
    clientId: string,
    request: SaveEnvironmentRequest
  ): Observable<ClientEnvironment> {
    return this.http.post<ClientEnvironment>(
      `${clientsUrl}/${encodeURIComponent(clientId)}/environments`,
      request
    );
  }

  updateEnvironment(
    clientId: string,
    environmentId: string,
    request: SaveEnvironmentRequest
  ): Observable<ClientEnvironment> {
    return this.http.put<ClientEnvironment>(
      `${clientsUrl}/${encodeURIComponent(clientId)}/environments/${encodeURIComponent(environmentId)}`,
      request
    );
  }

  createIntegration(
    clientId: string,
    request: SaveIntegrationRequest
  ): Observable<ClientIntegration> {
    return this.http.post<ClientIntegration>(
      `${clientsUrl}/${encodeURIComponent(clientId)}/integrations`,
      request
    );
  }

  updateIntegration(
    clientId: string,
    integrationId: string,
    request: SaveIntegrationRequest
  ): Observable<ClientIntegration> {
    return this.http.put<ClientIntegration>(
      `${clientsUrl}/${encodeURIComponent(clientId)}/integrations/${encodeURIComponent(integrationId)}`,
      request
    );
  }
}
