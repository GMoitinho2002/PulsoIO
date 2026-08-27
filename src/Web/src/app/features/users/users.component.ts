import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AdminClientsService } from '../../core/clients/admin-clients.service';
import { ClientSummary } from '../../core/clients/client.models';
import {
  AdminUser,
  evaluatePasswordRequirements,
  passwordPolicyValidator
} from '../../core/users/admin-user.models';
import { AdminUsersService } from '../../core/users/admin-users.service';

interface StatusConfirmation {
  user: AdminUser;
  isActive: boolean;
}

interface Feedback {
  tone: 'success' | 'error';
  message: string;
}

@Component({
  selector: 'app-users',
  imports: [ReactiveFormsModule],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsersComponent implements OnInit {
  private readonly usersService = inject(AdminUsersService);
  private readonly clientsService = inject(AdminClientsService);
  private readonly auth = inject(AuthService);
  private statusTrigger: HTMLButtonElement | null = null;

  readonly users = signal<AdminUser[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly changingStatus = signal(false);
  readonly feedback = signal<Feedback | null>(null);
  readonly confirmation = signal<StatusConfirmation | null>(null);
  readonly clients = signal<ClientSummary[]>([]);
  readonly clientsLoading = signal(true);
  readonly clientsError = signal(false);

  readonly searchControl = new FormControl('', { nonNullable: true });

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(150)]
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator]
    }),
    isActive: new FormControl(true, { nonNullable: true }),
    clientId: new FormControl<string | null>(this.auth.user()?.clientId ?? null)
  });

  private readonly passwordValue = toSignal(this.form.controls.password.valueChanges, {
    initialValue: ''
  });
  private readonly searchValue = toSignal(this.searchControl.valueChanges, { initialValue: '' });

  readonly passwordRequirements = computed(() =>
    evaluatePasswordRequirements(this.passwordValue())
  );
  readonly activeCount = computed(() => this.users().filter(user => user.isActive).length);
  readonly sortedUsers = computed(() =>
    [...this.users()].sort((left, right) => left.name.localeCompare(right.name, 'pt-BR'))
  );
  readonly activeClients = computed(() =>
    this.clients()
      .filter(client => client.isActive)
      .sort((left, right) => left.name.localeCompare(right.name, 'pt-BR'))
  );
  readonly isRootAdministrator = computed(() => this.auth.user()?.isRoot === true);
  readonly assignedClientName = computed(() =>
    this.clients().find(client => client.id === this.auth.user()?.clientId)?.name ??
      this.auth.user()?.clientName ??
      'Cliente vinculado'
  );
  readonly filteredUsers = computed(() => {
    const query = this.normalize(this.searchValue()).trim();
    const users = this.sortedUsers();

    if (!query) return users;

    const tokens = query.split(/\s+/).filter(Boolean);
    return users.filter(user => {
      const scope = user.isRoot || !user.clientId ? 'pulso io acesso global raiz root' : user.clientName ?? '';
      const searchable = this.normalize(`${user.name} ${user.email} ${scope}`);

      return tokens.every(token => {
        if (token === 'ativo') return user.isActive;
        if (token === 'inativo') return !user.isActive;
        return searchable.includes(token);
      });
    });
  });

  @ViewChild('nameInput') private nameInput?: ElementRef<HTMLInputElement>;
  @ViewChild('emailInput') private emailInput?: ElementRef<HTMLInputElement>;
  @ViewChild('passwordInput') private passwordInput?: ElementRef<HTMLInputElement>;
  @ViewChild('confirmAction') private confirmAction?: ElementRef<HTMLButtonElement>;

  ngOnInit(): void {
    this.loadUsers();
    this.loadClients();
  }

  loadClients(): void {
    this.clientsLoading.set(true);
    this.clientsError.set(false);
    this.clientsService
      .list()
      .pipe(finalize(() => this.clientsLoading.set(false)))
      .subscribe({
        next: clients => {
          this.clients.set(clients);
          if (!this.isRootAdministrator()) {
            this.form.controls.clientId.setValue(this.auth.user()?.clientId ?? null);
          }
        },
        error: () => this.clientsError.set(true)
      });
  }

  loadUsers(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.usersService
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: users => this.users.set(users),
        error: error =>
          this.loadError.set(this.describeError(error, 'Não foi possível carregar os usuários.'))
      });
  }

  createUser(): void {
    this.feedback.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      queueMicrotask(() => this.focusFirstInvalidField());
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.usersService
      .create({
        name: value.name.trim(),
        email: value.email.trim(),
        password: value.password,
        isActive: value.isActive,
        clientId: value.clientId || null
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: user => {
          this.users.update(users => [...users.filter(item => item.id !== user.id), user]);
          this.form.reset({ name: '', email: '', password: '', isActive: true, clientId: null });
          this.feedback.set({ tone: 'success', message: `${user.name} foi criado com sucesso.` });
          queueMicrotask(() => this.nameInput?.nativeElement.focus());
        },
        error: error =>
          this.feedback.set({
            tone: 'error',
            message: this.describeError(
              error,
              'Não foi possível criar o usuário.',
              'Já existe um usuário cadastrado com este e-mail.'
            )
          })
      });
  }

  isCurrentUser(user: AdminUser): boolean {
    return this.auth.user()?.id === user.id;
  }

  requestStatusChange(user: AdminUser, event?: Event): void {
    const newStatus = !user.isActive;

    if (this.isCurrentUser(user) && !newStatus) {
      this.feedback.set({
        tone: 'error',
        message: 'Você não pode desativar a conta usada nesta sessão.'
      });
      return;
    }

    this.statusTrigger =
      event?.currentTarget instanceof HTMLButtonElement ? event.currentTarget : null;
    this.feedback.set(null);
    this.confirmation.set({ user, isActive: newStatus });
    setTimeout(() => this.confirmAction?.nativeElement.focus());
  }

  cancelStatusChange(): void {
    if (!this.changingStatus()) {
      this.confirmation.set(null);
      this.restoreStatusTriggerFocus();
    }
  }

  confirmStatusChange(): void {
    const change = this.confirmation();
    if (!change || this.changingStatus()) return;

    this.changingStatus.set(true);
    this.usersService
      .updateStatus(change.user.id, change.isActive)
      .pipe(finalize(() => this.changingStatus.set(false)))
      .subscribe({
        next: updated => {
          this.users.update(users => users.map(user => (user.id === updated.id ? updated : user)));
          this.confirmation.set(null);
          this.restoreStatusTriggerFocus();
          this.feedback.set({
            tone: 'success',
            message: `${updated.name} foi ${updated.isActive ? 'ativado' : 'desativado'} com sucesso.`
          });
        },
        error: error => {
          this.confirmation.set(null);
          this.restoreStatusTriggerFocus();
          this.feedback.set({
            tone: 'error',
            message: this.describeError(
              error,
              'Não foi possível alterar o status do usuário.',
              'Esta alteração não é permitida. A própria conta e o último administrador ativo devem permanecer ativos.'
            )
          });
        }
      });
  }

  @HostListener('document:keydown.escape')
  closeConfirmationWithEscape(): void {
    this.cancelStatusChange();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
  }

  private focusFirstInvalidField(): void {
    if (this.form.controls.name.invalid) {
      this.nameInput?.nativeElement.focus();
    } else if (this.form.controls.email.invalid) {
      this.emailInput?.nativeElement.focus();
    } else {
      this.passwordInput?.nativeElement.focus();
    }
  }

  private restoreStatusTriggerFocus(): void {
    const trigger = this.statusTrigger;
    this.statusTrigger = null;
    setTimeout(() => trigger?.focus());
  }

  private describeError(error: unknown, fallback: string, conflictMessage = fallback): string {
    if (!(error instanceof HttpErrorResponse) || error.status === 0) {
      return 'Não foi possível conectar à API. Verifique se o backend está em execução.';
    }

    if (error.status === 400) {
      return 'Revise os dados informados e tente novamente.';
    }

    if (error.status === 403) {
      return 'Sua conta não possui permissão para gerenciar usuários.';
    }

    if (error.status === 409) {
      return conflictMessage;
    }

    return fallback;
  }

  private normalize(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase('pt-BR');
  }
}
