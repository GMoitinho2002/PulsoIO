import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClientDetail, ClientSummary } from '../../core/clients/client.models';
import { ClientsComponent } from './clients.component';

describe('ClientsComponent', () => {
  const summary: ClientSummary = {
    id: 'client-1',
    name: 'Cliente piloto',
    isActive: true,
    environmentCount: 0,
    integrationCount: 0
  };
  const detail: ClientDetail = {
    id: summary.id,
    name: summary.name,
    isActive: true,
    environments: [],
    integrations: []
  };

  let fixture: ComponentFixture<ClientsComponent>;
  let component: ClientsComponent;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ClientsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    controller = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ClientsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    controller.expectOne('/api/administration/clients').flush([summary]);
    controller.expectOne('/api/administration/clients/client-1').flush(detail);
    fixture.detectChanges();
  });

  afterEach(() => controller.verify());

  it('carrega o primeiro cliente e prepara seu detalhe', () => {
    expect(component.selectedId()).toBe(summary.id);
    expect(component.detail()?.name).toBe(summary.name);
    expect(component.integrationForm.controls.environmentId.value).toBe('');
  });

  it('impede nomes que o backend rejeitaria', () => {
    component.createClientForm.controls.name.setValue('A');
    component.environmentForm.controls.name.setValue('A'.repeat(101));
    component.integrationForm.controls.name.setValue('A'.repeat(151));

    expect(component.createClientForm.controls.name.invalid).toBe(true);
    expect(component.environmentForm.controls.name.invalid).toBe(true);
    expect(component.integrationForm.controls.name.invalid).toBe(true);
  });

  it('cadastra um cliente e seleciona o novo registro', () => {
    component.createClientForm.setValue({ name: 'Nova empresa', isActive: true });
    component.createClient();

    const createRequest = controller.expectOne('/api/administration/clients');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({ name: 'Nova empresa', isActive: true });
    createRequest.flush({ ...detail, id: 'client-2', name: 'Nova empresa' });

    controller.expectOne('/api/administration/clients/client-2').flush({
      ...detail,
      id: 'client-2',
      name: 'Nova empresa'
    });

    expect(component.selectedId()).toBe('client-2');
    expect(component.clients().some(client => client.id === 'client-2')).toBe(true);
  });
});
