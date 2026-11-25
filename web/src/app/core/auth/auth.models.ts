import { UserRole } from '../models/user-role';

export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  agencyId: string | null;
  agencyName: string | null;
  /** Cuenta gratuita de un particular. Ausente en sesiones guardadas antes de existir el registro público. */
  isIndividual?: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** Alta pública de una cuenta gratuita de particular. */
export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export interface Session {
  token: string;
  expiresAt: string;
  user: CurrentUser;
}
