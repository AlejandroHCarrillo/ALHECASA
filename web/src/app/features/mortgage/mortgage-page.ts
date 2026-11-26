import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Logo } from '../../shared/components/logo/logo';
import { MORTGAGE_CURRENCIES, MortgageCalculator, MortgageCurrency } from '../../shared/components/mortgage-calculator/mortgage-calculator';

/** Precio más alto que se acepta de la dirección: más allá es un enlace escrito a mano, no una propiedad. */
const MAX_PRICE = 1_000_000_000_000;

interface Preload {
  price: number | null;
  currency: MortgageCurrency | null;
}

/** Lee `?precio=&moneda=`. Una dirección escrita a mano puede traer cualquier cosa: lo que no se reconoce se ignora. */
export function readPreload(params: ParamMap): Preload {
  const raw = params.get('precio');
  const price = raw !== null && /^\d+(\.\d+)?$/.test(raw.trim()) ? Number(raw) : Number.NaN;
  const currency = MORTGAGE_CURRENCIES.find((code) => code === params.get('moneda')?.toUpperCase()) ?? null;

  return { price: Number.isFinite(price) && price > 0 && price <= MAX_PRICE ? price : null, currency };
}

/**
 * Calculadora hipotecaria como página pública (no pide sesión), para quien aún no tiene una propiedad en mente.
 * El detalle de una publicación en venta enlaza aquí con el precio y la moneda en la dirección.
 */
@Component({
  selector: 'app-mortgage-page',
  imports: [RouterLink, Logo, MortgageCalculator],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mortgage-page.html',
})
export class MortgagePage {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  protected readonly preload = toSignal(this.route.queryParamMap.pipe(map(readPreload)), { initialValue: { price: null, currency: null } as Preload });
}