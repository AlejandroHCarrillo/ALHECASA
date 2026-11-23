import { UserRole } from '../models/user-role';

export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  agencyId: string | null;
  agencyName: string | null;
}

export interface LoginRequest {
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
