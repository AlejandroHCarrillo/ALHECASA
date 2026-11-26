import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../shared/services/toast.service';
import { AgencyProfile, agencyLogoUrl, formatAddress } from '../agency-profile.models';
import { AgencyProfileService } from '../agency-profile.service';
import { AgencyProfileModal } from '../agency-profile-modal/agency-profile-modal';

/**
 * «Mi inmobiliaria» en la página de inicio del administrador: la información que se muestra en su página pública,
 * con el botón para editarla. Si no se pudo cargar, la tarjeta no se muestra (el resto de la página no depende de ella).
 */
@Component({
  selector: 'app-agency-profile-card',
  imports: [RouterLink, AgencyProfileModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agency-profile-card.html',
})
export class AgencyProfileCard implements OnInit {
  private readonly service = inject(AgencyProfileService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  protected readonly profile = signal<AgencyProfile | null>(null);
  protected readonly editing = signal(false);

  /** Cambia al guardar para que el navegador vuelva a pedir el logotipo, que conserva la misma dirección. */
  private readonly logoVersion = signal(Date.now());

  ngOnInit(): void {
    this.service.getMine().subscribe({
      next: (profile) => this.profile.set(profile),
      error: () => this.profile.set(null),
    });
  }

  protected logoUrl(profile: AgencyProfile): string {
    return agencyLogoUrl(profile.id, this.logoVersion());
  }

  protected address(profile: AgencyProfile): string {
    return formatAddress(profile);
  }

  /** Sin descripción, dirección ni logotipo, la página pública casi no tiene qué mostrar. */
  protected isEmpty(profile: AgencyProfile): boolean {
    return !profile.description && !profile.hasLogo && !formatAddress(profile);
  }

  protected onSaved(profile: AgencyProfile): void {
    this.profile.set(profile);
    this.logoVersion.set(Date.now());
    this.editing.set(false);
    this.auth.updateAgencyName(profile.name);
    this.toast.success('La información de la inmobiliaria se guardó correctamente.');
  }
}