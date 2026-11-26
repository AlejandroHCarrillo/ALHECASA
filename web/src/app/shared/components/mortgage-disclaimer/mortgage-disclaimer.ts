import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Aviso de que la calculadora hipotecaria es una referencia y no una promesa de crédito. */
@Component({
  selector: 'app-mortgage-disclaimer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="rounded-box border border-base-300 bg-base-200/60 p-3 text-xs leading-relaxed text-base-content/70" role="note" data-testid="disclaimer">
      <p class="mb-1 font-semibold text-base-content/80">
        <i class="fa-solid fa-circle-info mr-1" aria-hidden="true"></i>Importante
      </p>
      <p>
        Los cálculos son solo un parámetro para darte una idea y no una promesa ni una oferta de crédito. No incluyen el CAT y la
        tasa, el plazo y los gastos reales dependen de cada institución. Para conocer las condiciones que te corresponden, consulta a
        tu banco o institución de financiamiento.
      </p>
    </aside>
  `,
})
export class MortgageDisclaimer {}