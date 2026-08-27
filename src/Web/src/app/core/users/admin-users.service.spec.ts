import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AdminUser, CreateAdminUserRequest } from './admin-user.models';
import { AdminUsersService } from './admin-users.service';

describe('AdminUsersService', () => {
  let service: AdminUsersService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AdminUsersService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('lista usuários pelo endpoint administrativo', async () => {
    const users: AdminUser[] = [
      {
        id: 'user-1',
        name: 'Gustavo',
        email: 'gustavo@example.com',
        isActive: true,
        roles: ['Admin'],
        clientId: null,
        clientName: null,
        isRoot: true,
        hasProfilePhoto: false
      }
    ];
    const result = firstValueFrom(service.list());
    const request = controller.expectOne('/api/identity/users');

    expect(request.request.method).toBe('GET');
    request.flush(users);
    await expect(result).resolves.toEqual(users);
  });

  it('envia todos os campos necessários ao criar um usuário', async () => {
    const body: CreateAdminUserRequest = {
      name: 'Ana Silva',
      email: 'ana@example.com',
      password: 'Segura!',
      isActive: true,
      clientId: null
    };
    const created: AdminUser = {
      id: 'user-2',
      name: body.name,
      email: body.email,
      isActive: body.isActive,
      roles: [],
      clientId: body.clientId,
      clientName: null,
      isRoot: true,
      hasProfilePhoto: false
    };
    const result = firstValueFrom(service.create(body));
    const request = controller.expectOne('/api/identity/users');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(created);
    expect(await result).toEqual(created);
  });

  it('altera somente o status no endpoint do usuário', async () => {
    const updated: AdminUser = {
      id: 'user-2',
      name: 'Ana Silva',
      email: 'ana@example.com',
      isActive: false,
      roles: [],
      clientId: null,
      clientName: null,
      isRoot: true,
      hasProfilePhoto: false
    };
    const result = firstValueFrom(service.updateStatus('user-2', false));
    const request = controller.expectOne('/api/identity/users/user-2/status');

    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isActive: false });
    request.flush(updated);
    expect(await result).toEqual(updated);
  });
});
