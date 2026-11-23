export const UserRole = {
  PlatformAdmin: 'PlatformAdmin',
  AgencyAdmin: 'AgencyAdmin',
  Client: 'Client',
} as const;

export type UserRole = (typeof UserRole)[keyof typeof UserRole];

export const USER_ROLE_LABELS: Record<UserRole, string> = {
  PlatformAdmin: 'Administrador de la plataforma',
  AgencyAdmin: 'Administrador de inmobiliaria',
  Client: 'Cliente',
};

export const USER_ROLE_BADGES: Record<UserRole, string> = {
  PlatformAdmin: 'badge-primary',
  AgencyAdmin: 'badge-secondary',
  Client: 'badge-neutral',
};
