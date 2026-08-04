import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminClientsService } from '../../core/clients/admin-clients.service';
import {
  ClientDetail,
  ClientEnvironment,
  ClientIntegration,
  ClientSummary,
  EnvironmentKind,
  IntegrationDirection,
  SaveEnvironmentRequest,
  SaveIntegrationRequest
} from '../../core/clients/client.models';

interface Feedback {
  tone: 'success' | 'error';
  message: string;
}

@Component({
  selector: 'app-clients',
  imports: [ReactiveFormsModule],
  templateUrl: './clients.component.html',
  styleUrl: './clients.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClientsComponent implements OnInit {
  private readonly clientsService = inject(AdminClientsService);

  readonly clients = signal<ClientSummary[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly selectedId = signal<string | null>(null);
  readonly detail = signal<ClientDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);
  readonly creatingClient = signal(false);
  readonly savingClient = signal(false);
  readonly savingEnvironment = signal(false);
  readonly savingIntegration = signal(false);
  readonly editingEnvironmentId = signal<string | null>(null);
  readonly editingIntegrationId = signal<string | null>(null);
  readonly feedback = signal<Feedback | null>(null);

  readonly sortedClients = computed(() =>
    [...this.clients()].sort((left, right) => left.name.localeCompare(right.name, 'pt-BR'))
  );
  readonly activeClientCount = computed(() => this.clients().filter(client => client.isActive).length);
  readonly sortedEnvironments = computed(() =>
    [...(this.detail()?.environments ?? [])].sort((left, right) =>
      left.name.localeCompare(right.name, 'pt-BR')
    )
  );
  readonly sortedIntegrations = computed(() =>
    [...(this.detail()?.integrations ?? [])].sort((left, right) =>
      left.name.localeCompare(right.name, 'pt-BR')
    )
  );

  readonly createClientForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  readonly clientForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  readonly environmentForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    kind: new FormControl<EnvironmentKind>('Production', { nonNullable: true }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  readonly integrationForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    environmentId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    direction: new FormControl<IntegrationDirection>('Bidirectional', { nonNullable: true }),
    sourceSystem: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    targetSystem: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)]
    }),
    httpMethod: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(16)] }),
    endpointPattern: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(500)]
    }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  ngOnInit(): void {
    this.loadClients();
  }

  loadClients(preferredId?: string): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.clientsService
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: clients => {
          this.clients.set(clients);
          const id = preferredId ?? this.selectedId() ?? clients[0]?.id;
          if (id && clients.some(client => client.id === id)) {
            this.selectClient(id);
          } else {
            this.selectedId.set(null);
            this.detail.set(null);
          }
        },
        error: error => this.loadError.set(this.describeError(error, 'Não foi possível carregar os clientes.'))
      });
  }

  selectClient(id: string): void {
    if (this.detailLoading() || id === this.selectedId() && this.detail()) return;
    this.selectedId.set(id);
    this.detail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(true);
    this.cancelEnvironmentEdit();
    this.cancelIntegrationEdit();
    this.clientsService
      .get(id)
      .pipe(finalize(() => this.detailLoading.set(false)))
      .subscribe({
        next: detail => {
          this.detail.set(detail);
          this.clientForm.reset({ name: detail.name, isActive: detail.isActive });
          this.ensureIntegrationEnvironment();
        },
        error: error => this.detailError.set(this.describeError(error, 'Não foi possível abrir este cliente.'))
      });
  }

  retrySelectedClient(): void {
    const id = this.selectedId();
    if (id) {
      this.detail.set(null);
      this.selectClient(id);
    }
  }

  createClient(): void {
    if (this.createClientForm.invalid || this.creatingClient()) {
      this.createClientForm.markAllAsTouched();
      return;
    }
    const value = this.createClientForm.getRawValue();
    this.feedback.set(null);
    this.creatingClient.set(true);
    this.clientsService
      .create({ name: value.name.trim(), isActive: value.isActive })
      .pipe(finalize(() => this.creatingClient.set(false)))
      .subscribe({
        next: client => {
          const summary: ClientSummary = {
            id: client.id,
            name: client.name,
            isActive: client.isActive,
            environmentCount: client.environments.length,
            integrationCount: client.integrations.length
          };
          this.clients.update(items => [...items.filter(item => item.id !== client.id), summary]);
          this.createClientForm.reset({ name: '', isActive: true });
          this.feedback.set({ tone: 'success', message: `${client.name} foi cadastrado.` });
          this.selectClient(client.id);
        },
        error: error => this.feedback.set({
          tone: 'error',
          message: this.describeError(error, 'Não foi possível cadastrar o cliente.', 'Já existe um cliente com este nome.')
        })
      });
  }

  saveClient(): void {
    const detail = this.detail();
    if (!detail || this.clientForm.invalid || this.savingClient()) {
      this.clientForm.markAllAsTouched();
      return;
    }
    const value = this.clientForm.getRawValue();
    if (detail.isActive && !value.isActive && !confirm(`Desativar o cliente ${detail.name}? Os cadastros serão preservados.`)) {
      this.clientForm.controls.isActive.setValue(true);
      return;
    }
    this.feedback.set(null);
    this.savingClient.set(true);
    this.clientsService
      .update(detail.id, { name: value.name.trim(), isActive: value.isActive })
      .pipe(finalize(() => this.savingClient.set(false)))
      .subscribe({
        next: updated => {
          this.detail.set(updated);
          this.clients.update(items => items.map(item => item.id === updated.id ? {
            ...item,
            name: updated.name,
            isActive: updated.isActive,
            environmentCount: updated.environments.length,
            integrationCount: updated.integrations.length
          } : item));
          this.feedback.set({ tone: 'success', message: 'Dados do cliente atualizados.' });
        },
        error: error => this.feedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível atualizar o cliente.') })
      });
  }

  editEnvironment(environment: ClientEnvironment): void {
    this.editingEnvironmentId.set(environment.id);
    this.environmentForm.reset({
      name: environment.name,
      kind: environment.kind,
      isActive: environment.isActive
    });
  }

  cancelEnvironmentEdit(): void {
    this.editingEnvironmentId.set(null);
    this.environmentForm.reset({ name: '', kind: 'Production', isActive: true });
  }

  saveEnvironment(): void {
    const clientId = this.selectedId();
    if (!clientId || this.environmentForm.invalid || this.savingEnvironment()) {
      this.environmentForm.markAllAsTouched();
      return;
    }
    const value = this.environmentForm.getRawValue();
    const request: SaveEnvironmentRequest = {
      name: value.name.trim(), kind: value.kind, isActive: value.isActive
    };
    const id = this.editingEnvironmentId();
    const operation = id
      ? this.clientsService.updateEnvironment(clientId, id, request)
      : this.clientsService.createEnvironment(clientId, request);

    this.feedback.set(null);
    this.savingEnvironment.set(true);
    operation.pipe(finalize(() => this.savingEnvironment.set(false))).subscribe({
      next: environment => {
        this.detail.update(detail => detail ? {
          ...detail,
          environments: id
            ? detail.environments.map(item => item.id === environment.id ? environment : item)
            : [...detail.environments, environment]
        } : detail);
        this.refreshSummaryCounts();
        this.cancelEnvironmentEdit();
        this.ensureIntegrationEnvironment();
        this.feedback.set({ tone: 'success', message: `Ambiente ${id ? 'atualizado' : 'cadastrado'}.` });
      },
      error: error => this.feedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível salvar o ambiente.') })
    });
  }

  editIntegration(integration: ClientIntegration): void {
    this.editingIntegrationId.set(integration.id);
    this.integrationForm.reset({
      name: integration.name,
      environmentId: integration.environmentId,
      direction: integration.direction,
      sourceSystem: integration.sourceSystem,
      targetSystem: integration.targetSystem,
      httpMethod: integration.httpMethod ?? '',
      endpointPattern: integration.endpointPattern ?? '',
      isActive: integration.isActive
    });
  }

  cancelIntegrationEdit(): void {
    this.editingIntegrationId.set(null);
    this.integrationForm.reset({
      name: '',
      environmentId: this.detail()?.environments[0]?.id ?? '',
      direction: 'Bidirectional',
      sourceSystem: '',
      targetSystem: '',
      httpMethod: '',
      endpointPattern: '',
      isActive: true
    });
  }

  saveIntegration(): void {
    const clientId = this.selectedId();
    if (!clientId || this.integrationForm.invalid || this.savingIntegration()) {
      this.integrationForm.markAllAsTouched();
      return;
    }
    const value = this.integrationForm.getRawValue();
    const request: SaveIntegrationRequest = {
      name: value.name.trim(),
      environmentId: value.environmentId,
      direction: value.direction,
      sourceSystem: value.sourceSystem.trim(),
      targetSystem: value.targetSystem.trim(),
      httpMethod: value.httpMethod.trim() || null,
      endpointPattern: value.endpointPattern.trim() || null,
      isActive: value.isActive
    };
    const id = this.editingIntegrationId();
    const operation = id
      ? this.clientsService.updateIntegration(clientId, id, request)
      : this.clientsService.createIntegration(clientId, request);

    this.feedback.set(null);
    this.savingIntegration.set(true);
    operation.pipe(finalize(() => this.savingIntegration.set(false))).subscribe({
      next: integration => {
        this.detail.update(detail => detail ? {
          ...detail,
          integrations: id
            ? detail.integrations.map(item => item.id === integration.id ? integration : item)
            : [...detail.integrations, integration]
        } : detail);
        this.refreshSummaryCounts();
        this.cancelIntegrationEdit();
        this.feedback.set({ tone: 'success', message: `Integração ${id ? 'atualizada' : 'cadastrada'}.` });
      },
      error: error => this.feedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível salvar a integração.') })
    });
  }

  environmentName(id: string): string {
    return this.detail()?.environments.find(environment => environment.id === id)?.name ?? 'Ambiente removido';
  }

  kindLabel(kind: EnvironmentKind): string {
    return ({ Production: 'Produção', Staging: 'Homologação', Development: 'Desenvolvimento' })[kind];
  }

  directionLabel(direction: IntegrationDirection): string {
    return ({ Inbound: 'Entrada', Outbound: 'Saída', Bidirectional: 'Bidirecional' })[direction];
  }

  private ensureIntegrationEnvironment(): void {
    if (!this.integrationForm.controls.environmentId.value) {
      this.integrationForm.controls.environmentId.setValue(this.detail()?.environments[0]?.id ?? '');
    }
  }

  private refreshSummaryCounts(): void {
    const detail = this.detail();
    if (!detail) return;
    this.clients.update(items => items.map(item => item.id === detail.id ? {
      ...item,
      name: detail.name,
      isActive: detail.isActive,
      environmentCount: detail.environments.length,
      integrationCount: detail.integrations.length
    } : item));
  }

  private describeError(error: unknown, fallback: string, conflict = fallback): string {
    if (!(error instanceof HttpErrorResponse) || error.status === 0) return 'Não foi possível conectar à API.';
    if (error.status === 400) return 'Revise os dados informados e tente novamente.';
    if (error.status === 403) return 'Sua conta não possui permissão para administrar clientes.';
    if (error.status === 409) return conflict;
    return fallback;
  }
}
