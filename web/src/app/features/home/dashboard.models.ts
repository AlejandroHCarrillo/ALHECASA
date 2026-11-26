/** Propiedades de la cuenta por estado (sin las eliminadas). */
export interface PropertyStats {
  total: number;
  /** Activas: visibles para los interesados. */
  published: number;
  draft: number;
  paused: number;
  /** Operación concluida (vendidas o rentadas). */
  closed: number;
  /** Destacadas en este momento. */
  featured: number;
}

/** Solicitudes de informes por etapa. */
export interface InquiryStats {
  new: number;
  inProgress: number;
  closed: number;
  /** Las que llegaron en los últimos `recentDays` días. */
  receivedRecently: number;
  total: number;
}

export interface RecentInquiry {
  id: string;
  propertyId: string;
  propertyTitle: string;
  name: string;
  createdAt: string;
}

/** Resumen de la inmobiliaria (o del espacio del particular) para su página de inicio. */
export interface AgencyDashboard {
  properties: PropertyStats;
  inquiries: InquiryStats;
  recentDays: number;
  latestNewInquiries: RecentInquiry[];
}