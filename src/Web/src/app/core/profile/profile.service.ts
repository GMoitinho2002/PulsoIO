import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';

const profileUrl = '/api/identity/profile';

@Injectable({ providedIn: 'root' })
export class ProfileService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly photoUrlState = signal<string | null>(null);
  private photoLoaded = false;

  readonly photoUrl = this.photoUrlState.asReadonly();

  loadPhoto(hasProfilePhoto = true, force = false): Observable<string | null> {
    if (!hasProfilePhoto) {
      this.photoLoaded = true;
      this.clearPhoto();
      return of(null);
    }

    if (this.photoLoaded && !force) {
      return of(this.photoUrlState());
    }

    return this.http.get(`${profileUrl}/photo`, { responseType: 'blob' }).pipe(
      map(blob => this.createPhotoUrl(blob)),
      tap(url => {
        this.photoLoaded = true;
        this.setPhotoUrl(url);
      }),
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.photoLoaded = true;
          this.clearPhoto();
          return of(null);
        }

        return throwError(() => error);
      })
    );
  }

  uploadPhoto(file: File): Observable<string | null> {
    const headers = new HttpHeaders({ 'Content-Type': file.type });
    return this.http
      .put<void>(`${profileUrl}/photo`, file, { headers })
      .pipe(switchMap(() => this.loadPhoto(true, true)));
  }

  deletePhoto(): Observable<void> {
    return this.http.delete<void>(`${profileUrl}/photo`).pipe(
      tap(() => {
        this.photoLoaded = true;
        this.clearPhoto();
      })
    );
  }

  updateEmail(email: string, currentPassword: string): Observable<void> {
    return this.http.put<void>(`${profileUrl}/email`, { email, currentPassword });
  }

  updatePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.put<void>(`${profileUrl}/password`, { currentPassword, newPassword });
  }

  clearPhoto(): void {
    const current = this.photoUrlState();
    if (current && typeof URL !== 'undefined' && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(current);
    }
    this.photoUrlState.set(null);
  }

  reset(): void {
    this.photoLoaded = false;
    this.clearPhoto();
  }

  ngOnDestroy(): void {
    this.clearPhoto();
  }

  private createPhotoUrl(blob: Blob): string | null {
    if (typeof URL === 'undefined' || typeof URL.createObjectURL !== 'function') {
      return null;
    }
    return URL.createObjectURL(blob);
  }

  private setPhotoUrl(url: string | null): void {
    if (this.photoUrlState() !== url) {
      this.clearPhoto();
      this.photoUrlState.set(url);
    }
  }
}

