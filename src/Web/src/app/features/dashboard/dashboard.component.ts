import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiHealthService, ApiHealthState } from '../../core/api-health.service';
import { AuthService } from '../../core/auth/auth.service';
import { AdministrationOverview, DashboardService } from '../../core/dashboard/dashboard.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly dashboard = inject(DashboardService);
  private readonly apiHealth = inject(ApiHealthService);

  readonly user = this.auth.user;
  readonly overview = signal<AdministrationOverview | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly health = signal<ApiHealthState>({ state: 'checking' });
  readonly isAdmin = computed(() =>
    this.user()?.roles.some(role => role.toLowerCase() === 'admin') ?? false
  );

  ngOnInit(): void {
    this.load();
    this.apiHealth.check().subscribe(health => this.health.set(health));
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.dashboard
      .loadOverview()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: overview => this.overview.set(overview),
        error: () => this.loadError.set(true)
      });
  }
}
