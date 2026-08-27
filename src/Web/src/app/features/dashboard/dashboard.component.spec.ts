import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    controller = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => controller.verify());

  it('exibe os totais administrativos retornados pela API', () => {
    controller.expectOne('/health').flush({ status: 'healthy' });
    controller.expectOne('/api/administration/overview').flush({
      totalClients: 3,
      activeClients: 2,
      totalEnvironments: 5,
      activeEnvironments: 4,
      totalIntegrations: 8,
      activeIntegrations: 6
    });
    fixture.detectChanges();

    expect(component.overview()?.totalClients).toBe(3);
    expect(component.overview()?.totalIntegrations).toBe(8);
    expect(component.loadError()).toBe(false);
  });

  it('permite tentar novamente quando o resumo falha', () => {
    controller.expectOne('/health').flush({ status: 'healthy' });
    controller.expectOne('/api/administration/overview').flush(null, {
      status: 500,
      statusText: 'Server Error'
    });

    expect(component.loadError()).toBe(true);
    component.load();
    controller.expectOne('/api/administration/overview').flush({
      totalClients: 0,
      activeClients: 0,
      totalEnvironments: 0,
      activeEnvironments: 0,
      totalIntegrations: 0,
      activeIntegrations: 0
    });

    expect(component.loadError()).toBe(false);
  });
});
