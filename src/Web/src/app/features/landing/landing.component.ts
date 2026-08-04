import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiHealthService, ApiHealthState } from '../../core/api-health.service';

@Component({
  selector: 'app-landing',
  imports: [RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LandingComponent implements OnInit {
  private readonly apiHealthService = inject(ApiHealthService);

  readonly apiHealth = signal<ApiHealthState>({ state: 'checking' });

  readonly statusLabel = computed(() => {
    switch (this.apiHealth().state) {
      case 'online':
        return 'API operacional';
      case 'degraded':
        return 'Resposta inesperada';
      case 'offline':
        return 'API indisponível';
      default:
        return 'Verificando API';
    }
  });

  readonly statusDetail = computed(() => {
    const health = this.apiHealth();

    if (health.state === 'checking') {
      return 'Conectando ao ambiente local…';
    }

    if (health.state === 'offline') {
      return 'Inicie o backend .NET para restabelecer a conexão.';
    }

    return `${health.latencyMs} ms · verificado às ${health.checkedAt}`;
  });

  ngOnInit(): void {
    this.refreshHealth();
  }

  refreshHealth(): void {
    this.apiHealth.set({ state: 'checking' });
    this.apiHealthService.check().subscribe(health => this.apiHealth.set(health));
  }
}
