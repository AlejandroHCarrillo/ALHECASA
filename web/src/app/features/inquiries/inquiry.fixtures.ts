import { Inquiry } from './inquiry.models';

/** Solicitud de ejemplo para las pruebas. */
export const sampleInquiry = (overrides: Partial<Inquiry> = {}): Inquiry => ({
  id: 'q-1',
  propertyId: 'p-1',
  propertyTitle: 'Casa con alberca',
  name: 'Ana Lopez',
  phone: '55 1234 5678',
  email: 'ana@demo.com',
  message: 'Sigue disponible?\nPuedo visitarla el sabado?',
  status: 'New',
  followUps: [],
  createdAt: '2026-11-24T10:00:00Z',
  updatedAt: null,
  ...overrides,
});