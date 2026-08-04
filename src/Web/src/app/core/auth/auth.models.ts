export interface AuthUser {
  id: string;
  name: string;
  email: string;
  roles: string[];
  clientId: string | null;
  clientName: string | null;
  isRoot: boolean;
  hasProfilePhoto: boolean;
}

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}
