import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly retryingLogout = signal(false);
  readonly logoutRevoked = signal(false);
  readonly logoutNotice = signal<string | null>(
    this.route.snapshot.queryParamMap.get('logout') === 'unconfirmed'
      ? 'A sessão foi removida deste navegador, mas a API não confirmou a revogação. Tente novamente quando o backend estiver disponível.'
      : null
  );

  @ViewChild('emailInput') private emailInput?: ElementRef<HTMLInputElement>;
  @ViewChild('passwordInput') private passwordInput?: ElementRef<HTMLInputElement>;

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required]
    })
  });

  submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      queueMicrotask(() =>
        (this.form.controls.email.invalid ? this.emailInput : this.passwordInput)?.nativeElement.focus()
      );
      return;
    }

    this.submitting.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl(this.getSafeReturnUrl()),
        error: (error: unknown) => this.errorMessage.set(this.describeError(error))
      });
  }

  retryLogout(): void {
    if (this.retryingLogout()) return;

    this.retryingLogout.set(true);
    this.auth
      .logout()
      .pipe(finalize(() => this.retryingLogout.set(false)))
      .subscribe({
        next: () => {
          this.logoutRevoked.set(true);
          this.logoutNotice.set('Sessão revogada com sucesso no servidor.');
        },
        error: () =>
          this.logoutNotice.set(
            'A API ainda não confirmou a revogação. Verifique o backend e tente novamente.'
          )
      });
  }

  private getSafeReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/app';
  }

  private describeError(error: unknown): string {
    if (!(error instanceof HttpErrorResponse) || error.status === 0) {
      return 'Não foi possível conectar à API. Verifique se o backend está em execução.';
    }

    if (error.status === 401) {
      return 'E-mail ou senha inválidos.';
    }

    if (error.status === 423 || error.status === 429) {
      return 'Muitas tentativas de acesso. Aguarde alguns minutos e tente novamente.';
    }

    return 'Não foi possível entrar agora. Tente novamente em instantes.';
  }
}
