import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export interface AdminUser {
  id: string;
  name: string;
  email: string;
  roles: string[];
  clientId: string | null;
  clientName: string | null;
  isRoot: boolean;
  hasProfilePhoto: boolean;
  isActive: boolean;
}

export interface CreateAdminUserRequest {
  name: string;
  email: string;
  password: string;
  isActive: boolean;
  clientId: string | null;
}

export interface PasswordRequirements {
  minLength: boolean;
  uppercase: boolean;
  lowercase: boolean;
  special: boolean;
}

export function evaluatePasswordRequirements(password: string): PasswordRequirements {
  return {
    minLength: Array.from(password).length >= 6,
    uppercase: /\p{Lu}/u.test(password),
    lowercase: /\p{Ll}/u.test(password),
    special: /[\p{P}\p{S}]/u.test(password)
  };
}

export const passwordPolicyValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const password = typeof control.value === 'string' ? control.value : '';

  if (!password) {
    return null;
  }

  const requirements = evaluatePasswordRequirements(password);
  return Object.values(requirements).every(Boolean)
    ? null
    : { passwordPolicy: requirements };
};
