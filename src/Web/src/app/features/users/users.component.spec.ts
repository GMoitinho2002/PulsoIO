import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthSession } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { AdminUser } from '../../core/users/admin-user.models';
import { UsersComponent } from './users.component';

describe('UsersComponent', () => {
  const currentUser: AdminUser = {
    id: 'current-user',
    name: 'Gustavo',
    email: 'gustavo@example.com',
    isActive: true
  };
  const otherUser: AdminUser = {
    id: 'other-user',
    name: 'Ana',
    email: 'ana@example.com',
    isActive: true
  };

  let fixture: ComponentFixture<UsersComponent>;
  let component: UsersComponent;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [UsersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    const login = firstValueFrom(
      auth.login({ email: currentUser.email, password: 'not-a-real-password' })
    );
    controller.expectOne('/api/identity/auth/login').flush({
      accessToken: 'access-token',
      expiresAtUtc: '2026-08-03T03:00:00Z',
      user: { ...currentUser, roles: ['Admin'] }
    } satisfies AuthSession);
    await login;

    fixture = TestBed.createComponent(UsersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    controller.expectOne('/api/identity/users').flush([currentUser, otherUser]);
    fixture.detectChanges();
  });

  afterEach(() => controller.verify());

  it('bloqueia a tentativa de desativar a conta da sessão atual', () => {
    component.requestStatusChange(currentUser);

    expect(component.confirmation()).toBeNull();
    expect(component.feedback()?.tone).toBe('error');
    controller.expectNone('/api/identity/users/current-user/status');
  });

  it('confirma e aplica a desativação de outro usuário', () => {
    component.requestStatusChange(otherUser);
    expect(component.confirmation()).toEqual({ user: otherUser, isActive: false });

    component.confirmStatusChange();
    const request = controller.expectOne('/api/identity/users/other-user/status');
    expect(request.request.body).toEqual({ isActive: false });
    request.flush({ ...otherUser, isActive: false });

    expect(component.users().find(user => user.id === otherUser.id)?.isActive).toBe(false);
    expect(component.confirmation()).toBeNull();
    expect(component.feedback()?.message).toContain('desativado');
  });
});
