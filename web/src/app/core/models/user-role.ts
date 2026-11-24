export const UserRole = {
  PlatformAdmin: 'PlatformAdmin',
  AgencyAdmin: 'AgencyAdmin',
  Client: 'Client',
  Supervisor: 'Supervisor',
  Seller: 'Seller',
} as const;

export type UserRole = (typeof UserRole)[keyof typeof UserRole];

export const USER_ROLE_LABELS: Record<UserRole, string> = {
  PlatformAdmin: 'Administrador de la plataforma',
  AgencyAdmin: 'Administrador de inmobiliaria',
  Supervisor: 'Supervisor',
  Seller: 'Vendedor',
  Client: 'Cliente',
};

export const USER_ROLE_BADGES: Record<UserRole, string> = {
  PlatformAdmin: 'badge-primary',
  AgencyAdmin: 'badge-secondary',
  Supervisor: 'badge-accent',
  Seller: 'badge-info',
  Client: 'badge-neutral',
};

/** Roles del personal de una inmobiliaria: siempre pertenecen a una (el backend lo exige). */
const ROLES_REQUIRING_AGENCY: readonly UserRole[] = [
  UserRole.AgencyAdmin,
  UserRole.Supervisor,
  UserRole.Seller,
];

export function requiresAgency(role: UserRole): boolean {
  return ROLES_REQUIRING_AGENCY.includes(role);
}
