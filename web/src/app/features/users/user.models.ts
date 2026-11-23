import { UserRole } from '../../core/models/user-role';

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  role: UserRole;
  agencyId: string | null;
  agencyName: string | null;
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface CreateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: UserRole;
  agencyId: string | null;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  agencyId: string | null;
  isActive: boolean;
}

export interface Agency {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  isActive: boolean;
}
