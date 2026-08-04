import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminUser, CreateAdminUserRequest } from './admin-user.models';

const usersUrl = '/api/identity/users';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);

  list(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(usersUrl);
  }

  create(request: CreateAdminUserRequest): Observable<AdminUser> {
    return this.http.post<AdminUser>(usersUrl, request);
  }

  updateStatus(id: string, isActive: boolean): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${usersUrl}/${encodeURIComponent(id)}/status`, { isActive });
  }
}
