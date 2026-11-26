import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AgencyProfile, agencyLogoUrl, formatAddress } from '../../agency-profile/agency-profile.models';

/**
 * Lo que la inmobiliaria configuró para mostrarse al público: logotipo, descripción, contacto, horario, dirección y enlaces.
 * Solo muestra lo que tiene; los enlaces externos se abren en otra pestaña sin pasar datos de esta (`noopener`).
 */
@Component({
  selector: 'app-agency-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agency-header.html',
})
export class AgencyHeader {
  readonly agency = input.required<AgencyProfile>();

  protected readonly logoUrl = computed(() => agencyLogoUrl(this.agency().id));
  protected readonly address = computed(() => formatAddress(this.agency()));

  /** `tel:` solo con dígitos y el `+` inicial: el texto que escribió la inmobiliaria puede traer espacios y paréntesis. */
  protected readonly phoneHref = computed(() => {
    const phone = this.agency().phone;
    return phone ? `tel:${phone.replace(/[^\d+]/g, '')}` : null;
  });
}