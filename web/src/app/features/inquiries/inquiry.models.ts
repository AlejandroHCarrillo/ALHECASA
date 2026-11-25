export const InquiryStatus = {
  New: 'New',
  InProgress: 'InProgress',
  Closed: 'Closed',
} as const;
export type InquiryStatus = (typeof InquiryStatus)[keyof typeof InquiryStatus];

export const FollowUpKind = {
  Reply: 'Reply',
  Note: 'Note',
} as const;
export type FollowUpKind = (typeof FollowUpKind)[keyof typeof FollowUpKind];

export const INQUIRY_STATUS_LABELS: Record<InquiryStatus, string> = {
  New: 'Nueva',
  InProgress: 'En seguimiento',
  Closed: 'Cerrada',
};

export const INQUIRY_STATUS_BADGES: Record<InquiryStatus, string> = {
  New: 'badge-accent',
  InProgress: 'badge-warning',
  Closed: 'badge-ghost',
};

export const FOLLOW_UP_KIND_LABELS: Record<FollowUpKind, string> = {
  Reply: 'Respuesta al prospecto',
  Note: 'Nota interna',
};

/** Límites alineados con InquiryLimits y CreateInquiryRequestValidator del backend. */
export const INQUIRY_LIMITS = {
  name: 100,
  phone: 30,
  email: 256,
  text: 2000,
  minPhoneDigits: 7,
  maxPhoneDigits: 15,
} as const;

/** Lo que escribe el prospecto al pedir informes. */
export interface CreateInquiryRequest {
  name: string;
  phone: string;
  email: string;
  message: string;
}

export interface FollowUp {
  id: string;
  kind: FollowUpKind;
  text: string;
  authorName: string;
  createdAt: string;
}

/** Solicitud de informes con los datos del prospecto y su seguimiento (del más antiguo al más reciente). */
export interface Inquiry {
  id: string;
  propertyId: string;
  propertyTitle: string;
  name: string;
  phone: string;
  email: string;
  message: string;
  status: InquiryStatus;
  followUps: FollowUp[];
  createdAt: string;
  updatedAt: string | null;
}

export interface AddFollowUpRequest {
  kind: FollowUpKind;
  text: string;
}