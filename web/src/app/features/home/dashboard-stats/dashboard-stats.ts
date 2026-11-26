import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InquiryStatus } from '../../inquiries/inquiry.models';
import { PropertyStatus } from '../../properties/property.models';
import { AgencyDashboard } from '../dashboard.models';

interface StatCard {
  key: string;
  label: string;
  hint: string;
  value: number;
  /** Clase de color del número. */
  tone: string;
  /** Estado con el que se abre la lista al pulsar la tarjeta. Sin estado, se abre la lista completa. */
  status?: string;
}

/** Cuántas propiedades y solicitudes de informes tiene la inmobiliaria, con acceso directo a cada lista. */
@Component({
  selector: 'app-dashboard-stats',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-stats.html',
})
export class DashboardStats {
  readonly dashboard = input.required<AgencyDashboard>();

  protected readonly propertyCards = computed<StatCard[]>(() => {
    const p = this.dashboard().properties;
    return [
      { key: 'published', label: 'Activas', hint: 'Visibles para los interesados', value: p.published, tone: 'text-success', status: PropertyStatus.Published },
      { key: 'featured', label: 'Destacadas', hint: 'Con paquete de destacados', value: p.featured, tone: 'text-secondary' },
      { key: 'draft', label: 'Borradores', hint: 'Aún no son visibles', value: p.draft, tone: 'text-base-content', status: PropertyStatus.Draft },
      { key: 'paused', label: 'Pausadas', hint: 'Retiradas temporalmente', value: p.paused, tone: 'text-warning', status: PropertyStatus.Paused },
      { key: 'closed', label: 'Cerradas', hint: 'Vendidas o rentadas', value: p.closed, tone: 'text-base-content/70', status: PropertyStatus.Closed },
    ];
  });

  protected readonly inquiryCards = computed<StatCard[]>(() => {
    const { inquiries, recentDays } = this.dashboard();
    return [
      { key: 'inquiry-new', label: 'Preguntas nuevas', hint: 'Sin atender', value: inquiries.new, tone: 'text-accent', status: InquiryStatus.New },
      { key: 'inquiry-progress', label: 'En seguimiento', hint: 'Ya se respondieron', value: inquiries.inProgress, tone: 'text-warning', status: InquiryStatus.InProgress },
      { key: 'inquiry-closed', label: 'Cerradas', hint: 'Atendidas por completo', value: inquiries.closed, tone: 'text-base-content/70', status: InquiryStatus.Closed },
      { key: 'inquiry-recent', label: 'Recibidas', hint: `En los últimos ${recentDays} días`, value: inquiries.receivedRecently, tone: 'text-base-content' },
    ];
  });

  protected query(card: StatCard): Record<string, string> | null {
    return card.status ? { status: card.status } : null;
  }
}