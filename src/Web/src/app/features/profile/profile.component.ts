import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import {
  evaluatePasswordRequirements,
  passwordPolicyValidator
} from '../../core/users/admin-user.models';
import { ProfileService } from '../../core/profile/profile.service';

interface Feedback {
  tone: 'success' | 'error';
  message: string;
}

const acceptedPhotoTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);
const maxPhotoBytes = 2 * 1024 * 1024;

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);

  readonly user = this.auth.user;
  readonly photoUrl = this.profileService.photoUrl;
  readonly previewUrl = signal<string | null>(null);
  readonly photoLoading = signal(false);
  readonly uploadingPhoto = signal(false);
  readonly deletingPhoto = signal(false);
  readonly savingEmail = signal(false);
  readonly savingPassword = signal(false);
  readonly photoFeedback = signal<Feedback | null>(null);
  readonly accountFeedback = signal<Feedback | null>(null);
  readonly initials = computed(() => {
    const name = this.user()?.name.trim() || this.user()?.email || 'PI';
    return name.split(/\s+/).slice(0, 2).map(part => part[0]?.toUpperCase()).join('');
  });

  readonly emailForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)]
    }),
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  readonly passwordForm = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    newPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator]
    }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  private readonly newPasswordValue = toSignal(this.passwordForm.controls.newPassword.valueChanges, {
    initialValue: ''
  });
  readonly passwordRequirements = computed(() => evaluatePasswordRequirements(this.newPasswordValue()));
  readonly passwordsMatch = computed(() =>
    this.passwordForm.controls.newPassword.value === this.passwordForm.controls.confirmPassword.value
  );

  @ViewChild('photoInput') private photoInput?: ElementRef<HTMLInputElement>;

  ngOnInit(): void {
    this.emailForm.controls.email.setValue(this.user()?.email ?? '');
    if (this.user()?.hasProfilePhoto) {
      this.photoLoading.set(true);
      this.profileService
        .loadPhoto(true)
        .pipe(finalize(() => this.photoLoading.set(false)))
        .subscribe({ error: () => this.photoFeedback.set({ tone: 'error', message: 'Não foi possível carregar sua foto.' }) });
    }
  }

  choosePhoto(): void {
    this.photoInput?.nativeElement.click();
  }

  photoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.photoFeedback.set(null);
    if (!acceptedPhotoTypes.has(file.type)) {
      this.photoFeedback.set({ tone: 'error', message: 'Use uma imagem JPEG, PNG ou WebP.' });
      input.value = '';
      return;
    }
    if (file.size > maxPhotoBytes) {
      this.photoFeedback.set({ tone: 'error', message: 'A foto deve ter no máximo 2 MB.' });
      input.value = '';
      return;
    }

    this.setPreview(file);
    this.uploadingPhoto.set(true);
    this.profileService
      .uploadPhoto(file)
      .pipe(finalize(() => {
        this.uploadingPhoto.set(false);
        this.revokePreview();
        input.value = '';
      }))
      .subscribe({
        next: () => {
          this.photoFeedback.set({ tone: 'success', message: 'Foto de perfil atualizada.' });
          this.refreshCurrentUser();
        },
        error: error => this.photoFeedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível enviar a foto.') })
      });
  }

  deletePhoto(): void {
    if (this.deletingPhoto() || !confirm('Remover sua foto de perfil?')) return;
    this.photoFeedback.set(null);
    this.deletingPhoto.set(true);
    this.profileService
      .deletePhoto()
      .pipe(finalize(() => this.deletingPhoto.set(false)))
      .subscribe({
        next: () => {
          this.photoFeedback.set({ tone: 'success', message: 'Foto de perfil removida.' });
          this.refreshCurrentUser();
        },
        error: error => this.photoFeedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível remover a foto.') })
      });
  }

  updateEmail(): void {
    if (this.emailForm.invalid || this.savingEmail()) {
      this.emailForm.markAllAsTouched();
      return;
    }
    const value = this.emailForm.getRawValue();
    this.accountFeedback.set(null);
    this.savingEmail.set(true);
    this.profileService
      .updateEmail(value.email.trim(), value.currentPassword)
      .pipe(finalize(() => this.savingEmail.set(false)))
      .subscribe({
        next: () => this.finishSensitiveUpdate('E-mail alterado. Entre novamente com o novo endereço.'),
        error: error => this.accountFeedback.set({
          tone: 'error',
          message: this.describeError(error, 'Não foi possível alterar o e-mail.', 'Este e-mail já está em uso.')
        })
      });
  }

  updatePassword(): void {
    if (this.passwordForm.invalid || !this.passwordsMatch() || this.savingPassword()) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const value = this.passwordForm.getRawValue();
    this.accountFeedback.set(null);
    this.savingPassword.set(true);
    this.profileService
      .updatePassword(value.currentPassword, value.newPassword)
      .pipe(finalize(() => this.savingPassword.set(false)))
      .subscribe({
        next: () => this.finishSensitiveUpdate('Senha alterada. Entre novamente com a nova senha.'),
        error: error => this.accountFeedback.set({ tone: 'error', message: this.describeError(error, 'Não foi possível alterar a senha.') })
      });
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  private setPreview(file: File): void {
    this.revokePreview();
    if (typeof URL !== 'undefined' && typeof URL.createObjectURL === 'function') {
      this.previewUrl.set(URL.createObjectURL(file));
    }
  }

  private revokePreview(): void {
    const url = this.previewUrl();
    if (url && typeof URL !== 'undefined' && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(url);
    }
    this.previewUrl.set(null);
  }

  private refreshCurrentUser(): void {
    this.auth.loadCurrentUser().subscribe({ error: () => undefined });
  }

  private finishSensitiveUpdate(message: string): void {
    this.auth.clearSession();
    this.profileService.reset();
    void this.router.navigate(['/login'], {
      queryParams: { profileUpdated: message },
      replaceUrl: true
    });
  }

  private describeError(error: unknown, fallback: string, conflict = fallback): string {
    if (!(error instanceof HttpErrorResponse) || error.status === 0) return 'Não foi possível conectar à API.';
    if (error.status === 400 || error.status === 401) return 'A senha atual está incorreta ou os dados são inválidos.';
    if (error.status === 409) return conflict;
    if (error.status === 413) return 'A foto ultrapassa o limite de 2 MB.';
    return fallback;
  }
}

