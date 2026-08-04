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
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profile/profile.service';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);

  readonly user = this.auth.user;
  readonly signingOut = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly photoUrl = this.profileService.photoUrl;
  readonly isAdmin = computed(() =>
    this.user()?.roles.some(role => role.toLowerCase() === 'admin') ?? false
  );
  readonly initials = computed(() => {
    const name = this.user()?.name.trim() || this.user()?.email || 'PI';
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map(part => part[0]?.toUpperCase())
      .join('');
  });

  @ViewChild('profileArea') private profileArea?: ElementRef<HTMLElement>;
  @ViewChild('profileButton') private profileButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('profileLink') private profileLink?: ElementRef<HTMLAnchorElement>;

  ngOnInit(): void {
    if (this.user()?.hasProfilePhoto) {
      this.profileService.loadPhoto(true).subscribe({ error: () => undefined });
    }
  }

  toggleProfileMenu(event: Event): void {
    event.stopPropagation();
    const opening = !this.profileMenuOpen();
    this.profileMenuOpen.set(opening);
    if (opening) setTimeout(() => this.profileLink?.nativeElement.focus());
  }

  closeProfileMenu(restoreFocus = false): void {
    if (!this.profileMenuOpen()) return;
    this.profileMenuOpen.set(false);
    if (restoreFocus) setTimeout(() => this.profileButton?.nativeElement.focus());
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnOutsideClick(event: Event): void {
    if (!this.profileArea?.nativeElement.contains(event.target as Node)) {
      this.closeProfileMenu();
    }
  }

  @HostListener('document:keydown.escape')
  closeProfileMenuOnEscape(): void {
    this.closeProfileMenu(true);
  }

  logout(): void {
    if (this.signingOut()) return;

    this.signingOut.set(true);
    this.closeProfileMenu();
    this.auth.logout().subscribe({
      next: () => {
        this.profileService.reset();
        void this.router.navigate(['/login'], { replaceUrl: true });
      },
      error: () => {
        this.profileService.reset();
        void this.router.navigate(['/login'], {
          queryParams: { logout: 'unconfirmed' },
          replaceUrl: true
        });
      }
    });
  }
}
