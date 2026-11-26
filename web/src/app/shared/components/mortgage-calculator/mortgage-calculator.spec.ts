import { ComponentFixture, TestBed } from '@angular/core/testing';
import { affordability, monthlyPayment } from '../../utils/mortgage';
import { MortgageCalculator, MortgageCurrency } from './mortgage-calculator';

const money = (value: number, currency: MortgageCurrency = 'MXN'): string =>
  new Intl.NumberFormat('es-MX', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value).replace(/\s+/g, ' ');

describe('MortgageCalculator', () => {
  const render = (inputs: { price?: number | null; currency?: MortgageCurrency | null; compact?: boolean } = {}): ComponentFixture<MortgageCalculator> => {
    TestBed.configureTestingModule({});
    const fixture = TestBed.createComponent(MortgageCalculator);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    fixture.detectChanges();
    return fixture;
  };

  const root = (fixture: ComponentFixture<MortgageCalculator>) => fixture.nativeElement as HTMLElement;
  const byId = (fixture: ComponentFixture<MortgageCalculator>, id: string) => root(fixture).querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const text = (fixture: ComponentFixture<MortgageCalculator>, id: string) => byId(fixture, id)?.textContent?.replace(/\s+/g, ' ').trim();
  const field = (fixture: ComponentFixture<MortgageCalculator>, id: string) => root(fixture).querySelector(`#${id}`) as HTMLInputElement;
  const type = (fixture: ComponentFixture<MortgageCalculator>, id: string, value: string): void => {
    const input = field(fixture, id);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const click = (fixture: ComponentFixture<MortgageCalculator>, id: string): void => {
    byId(fixture, id)!.click();
    fixture.detectChanges();
  };

  beforeEach(() => TestBed.resetTestingModule());

  describe('con los datos de una publicación', () => {
    it('opens with the price, a 20 % down payment and the monthly payment of the rest', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      expect(field(fixture, 'mortgage-price').value).toBe('2000000');
      expect(field(fixture, 'mortgage-down-percent').value).toBe('20');
      expect(field(fixture, 'mortgage-down-amount').value).toBe('400000');
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 1_600_000, annualRate: 10, years: 20 })));
      expect(text(fixture, 'result-loan')).toBe(money(1_600_000));
    });

    it('formats the amounts in the currency of the publication', () => {
      const fixture = render({ price: 150_000, currency: 'USD' });

      expect(text(fixture, 'result-loan')).toBe(money(120_000, 'USD'));
      expect(text(fixture, 'result-loan')).toContain('USD');
    });

    it('loads the new price when the publication changes', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      fixture.componentRef.setInput('price', 3_000_000);
      fixture.detectChanges();
      fixture.detectChanges();

      expect(field(fixture, 'mortgage-price').value).toBe('3000000');
      expect(field(fixture, 'mortgage-down-amount').value).toBe('600000');
      expect(text(fixture, 'result-loan')).toBe(money(2_400_000));
    });

    it('does not offer to change the currency in the compact version', () => {
      expect(byId(render({ price: 1_000_000, currency: 'MXN', compact: true }), 'currency')).toBeNull();
      TestBed.resetTestingModule();
      expect(byId(render({ price: 1_000_000, currency: 'MXN' }), 'currency')).not.toBeNull();
    });
  });

  describe('sin precio', () => {
    it('asks for the data instead of showing numbers or errors', () => {
      const fixture = render();

      expect(text(fixture, 'no-result')).toContain('Escribe el precio');
      expect(byId(fixture, 'result-payment')).toBeNull();
      expect(byId(fixture, 'error-price')).toBeNull();
      expect(byId(fixture, 'error-annualRate')).toBeNull();
    });

    it('calculates as soon as the price is written, and lets the currency be chosen', () => {
      const fixture = render();

      type(fixture, 'mortgage-price', '1000000');

      expect(field(fixture, 'mortgage-down-amount').value).toBe('200000');
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 800_000, annualRate: 10, years: 20 })));

      const select = byId(fixture, 'currency') as unknown as HTMLSelectElement;
      select.value = 'USD';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(text(fixture, 'result-loan')).toBe(money(800_000, 'USD'));
    });
  });

  describe('enganche', () => {
    it('keeps the amount and the percentage in sync, whichever one is edited', () => {
      const fixture = render({ price: 1_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-down-percent', '30');
      expect(field(fixture, 'mortgage-down-amount').value).toBe('300000');
      expect(text(fixture, 'result-loan')).toBe(money(700_000));

      type(fixture, 'mortgage-down-amount', '250000');
      expect(field(fixture, 'mortgage-down-percent').value).toBe('25');
      expect(text(fixture, 'result-loan')).toBe(money(750_000));
    });

    it('keeps the percentage when the price changes', () => {
      const fixture = render({ price: 1_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-price', '2500000');

      expect(field(fixture, 'mortgage-down-percent').value).toBe('20');
      expect(field(fixture, 'mortgage-down-amount').value).toBe('500000');
    });

    it('lets the down payment be zero', () => {
      const fixture = render({ price: 1_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-down-percent', '0');

      expect(text(fixture, 'result-loan')).toBe(money(1_000_000));
      expect(byId(fixture, 'error-downPaymentPercent')).toBeNull();
    });
  });

  describe('recálculo', () => {
    it('recalculates when the rate or the term change', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-rate', '12');
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 1_600_000, annualRate: 12, years: 20 })));

      type(fixture, 'mortgage-years', '15');
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 1_600_000, annualRate: 12, years: 15 })));
    });

    it('works with a 0 % rate', () => {
      const fixture = render({ price: 1_200_000, currency: 'MXN' });

      type(fixture, 'mortgage-down-percent', '0');
      type(fixture, 'mortgage-rate', '0');
      type(fixture, 'mortgage-years', '10');

      expect(text(fixture, 'result-payment')).toBe(money(10_000));
      expect(text(fixture, 'result-interest')).toBe(money(0));
    });

    it('shows the totals: what is paid, the interests and the interests per peso', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      const payment = monthlyPayment({ principal: 1_600_000, annualRate: 10, years: 20 });

      expect(text(fixture, 'result-total-paid')).toBe(money(payment * 240));
      expect(text(fixture, 'result-interest')).toBe(money(payment * 240 - 1_600_000));
      expect(text(fixture, 'result-interest-ratio')).toBe(money((payment * 240 - 1_600_000) / 1_600_000));
    });

    it('draws capital and interests as shares of the total, adding up to 100 %', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      const capital = parseFloat((byId(fixture, 'bar-capital') as HTMLElement).style.width);
      const interest = parseFloat((byId(fixture, 'bar-interest') as HTMLElement).style.width);

      expect(capital + interest).toBeCloseTo(100, 6);
      expect(capital).toBeGreaterThan(0);
      expect(interest).toBeGreaterThan(0);
    });
  });

  describe('gastos opcionales', () => {
    it('adds the down payment, the opening fee and the closing costs by default', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      // 400,000 de enganche + 1 % de 1,600,000 + 5 % de 2,000,000
      expect(text(fixture, 'result-upfront')).toBe(money(400_000 + 16_000 + 100_000));
      expect(text(fixture, 'result-upfront-detail')).toContain('apertura');
      expect(text(fixture, 'result-upfront-detail')).toContain('escrituración');
    });

    it('adds the appraisal and drops a cost that is set to zero', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-appraisal', '5000');
      type(fixture, 'mortgage-opening', '0');
      type(fixture, 'mortgage-closing', '0');

      expect(text(fixture, 'result-upfront')).toBe(money(405_000));
      expect(text(fixture, 'result-upfront-detail')).not.toContain('apertura');
      expect(text(fixture, 'result-upfront-detail')).toContain('avalúo');
    });

    it('shows the payment with insurance only when there is insurance', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      expect(byId(fixture, 'result-payment-insurance')).toBeNull();

      type(fixture, 'mortgage-insurance', '800');

      const payment = monthlyPayment({ principal: 1_600_000, annualRate: 10, years: 20 });
      expect(text(fixture, 'result-payment')).toBe(money(payment));
      expect(text(fixture, 'result-payment-insurance')).toBe(money(payment + 800));
    });

    it('always shows the disclaimer, with or without a result', () => {
      expect(text(render({ price: 1_000_000, currency: 'MXN' }), 'disclaimer')).toContain('banco o institución de financiamiento');
    });

    it('shows the disclaimer even before there is a result', () => {
      expect(text(render({ price: null, currency: null }), 'disclaimer')).toContain('no una promesa');
    });
  });

  describe('validaciones', () => {
    it.each([
      ['mortgage-rate', '150', 'error-annualRate', 'entre 0 y 100'],
      ['mortgage-rate', '-1', 'error-annualRate', 'entre 0 y 100'],
      ['mortgage-years', '0', 'error-years', 'entre 1 y 40'],
      ['mortgage-years', '41', 'error-years', 'entre 1 y 40'],
      ['mortgage-price', '-5', 'error-price', 'mayor que cero'],
      ['mortgage-price', '0', 'error-price', 'mayor que cero'],
      ['mortgage-down-percent', '100', 'error-downPaymentPercent', 'entre 0 y 99.99'],
      ['mortgage-down-percent', '-1', 'error-downPaymentPercent', 'entre 0 y 99.99'],
      ['mortgage-down-amount', '-1', 'error-downPaymentAmount', 'negativo'],
    ])('rejects %s = %s with a message and no numbers', (id, value, errorId, message) => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      type(fixture, id, value);

      expect(text(fixture, errorId)).toContain(message);
      expect(byId(fixture, 'result-payment')).toBeNull();
      expect(field(fixture, id).getAttribute('aria-invalid')).toBe('true');
    });

    it('rejects a down payment that is not less than the price', () => {
      const fixture = render({ price: 1_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-down-amount', '1000000');

      expect(text(fixture, 'error-downPaymentAmount')).toContain('menor que el precio');
      expect(byId(fixture, 'result-payment')).toBeNull();
    });

    it('asks for the rate and the term when they are emptied', () => {
      const fixture = render({ price: 1_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-rate', '');
      type(fixture, 'mortgage-years', '');

      expect(text(fixture, 'error-annualRate')).toContain('Escribe la tasa');
      expect(text(fixture, 'error-years')).toContain('Escribe el plazo');
      expect(byId(fixture, 'no-result')).not.toBeNull();
    });

    it('recovers the numbers once the value is fixed', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      type(fixture, 'mortgage-rate', '150');
      expect(byId(fixture, 'result-payment')).toBeNull();

      type(fixture, 'mortgage-rate', '9');
      expect(byId(fixture, 'error-annualRate')).toBeNull();
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 1_600_000, annualRate: 9, years: 20 })));
    });

    it.each([
      ['mortgage-appraisal', '-1', 'error-appraisal'],
      ['mortgage-insurance', '-1', 'error-monthlyInsurance'],
      ['mortgage-opening', '101', 'error-openingFeePercent'],
      ['mortgage-closing', '-2', 'error-closingCostsPercent'],
    ])('flags the optional cost %s = %s without blocking the payment', (id, value, errorId) => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      type(fixture, id, value);

      expect(byId(fixture, errorId)).not.toBeNull();
      expect(byId(fixture, 'result-payment')).not.toBeNull();
    });
  });

  describe('tabla de amortización', () => {
    it('is not rendered until it is opened', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      expect(byId(fixture, 'schedule')).toBeNull();
      expect(root(fixture).querySelectorAll('[data-testid="schedule-row"]')).toHaveLength(0);
    });

    it('shows one row per year, ending with no debt', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      click(fixture, 'schedule-toggle');

      const rows = Array.from(root(fixture).querySelectorAll('[data-testid="schedule-row"]')) as HTMLElement[];
      expect(rows).toHaveLength(20);
      expect(rows[0].textContent).toContain('1');
      expect(rows.at(-1)?.textContent).toContain(money(0));
    });

    it('can show every month', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      click(fixture, 'schedule-toggle');
      click(fixture, 'schedule-view-month');

      expect(root(fixture).querySelectorAll('[data-testid="schedule-row"]')).toHaveLength(240);
    });

    it('follows the term and can be closed again', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      type(fixture, 'mortgage-years', '5');

      click(fixture, 'schedule-toggle');
      expect(root(fixture).querySelectorAll('[data-testid="schedule-row"]')).toHaveLength(5);

      click(fixture, 'schedule-toggle');
      expect(byId(fixture, 'schedule')).toBeNull();
    });
  });

  describe('cuánto me alcanza', () => {
    const afford = (fixture: ComponentFixture<MortgageCalculator>) => {
      click(fixture, 'mode-afford');
      type(fixture, 'mortgage-income', '50000');
    };

    it('asks for the income first', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      click(fixture, 'mode-afford');

      expect(text(fixture, 'no-afford')).toContain('Escribe tu ingreso');
      expect(byId(fixture, 'afford-max-loan')).toBeNull();
    });

    it('gives the biggest loan, payment and price for an income, with 30 % of it by default', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });

      afford(fixture);
      type(fixture, 'mortgage-afford-down', '300000');

      const expected = affordability({ monthlyIncome: 50_000, debtRatioPercent: 30, annualRate: 10, years: 20, downPayment: 300_000 })!;
      expect(field(fixture, 'mortgage-ratio').value).toBe('30');
      expect(text(fixture, 'afford-max-payment')).toBe(money(15_000));
      expect(text(fixture, 'afford-max-loan')).toBe(money(expected.maxLoan));
      expect(text(fixture, 'afford-max-price')).toBe(money(expected.maxPrice));
    });

    it('uses the rate, the term and the insurance of the loan data', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      type(fixture, 'mortgage-rate', '8');
      type(fixture, 'mortgage-years', '30');
      type(fixture, 'mortgage-insurance', '1000');

      afford(fixture);

      const expected = affordability({ monthlyIncome: 50_000, debtRatioPercent: 30, annualRate: 8, years: 30, monthlyInsurance: 1_000 })!;
      expect(text(fixture, 'afford-max-payment')).toBe(money(14_000));
      expect(text(fixture, 'afford-max-loan')).toBe(money(expected.maxLoan));
    });

    it('says whether the publication fits the budget', () => {
      const cheap = render({ price: 1_000_000, currency: 'MXN' });
      afford(cheap);
      type(cheap, 'mortgage-afford-down', '200000');
      expect(text(cheap, 'afford-fit')).toContain('entra en tu presupuesto');

      TestBed.resetTestingModule();
      const dear = render({ price: 9_000_000, currency: 'MXN' });
      afford(dear);
      expect(text(dear, 'afford-fit')).toContain('por encima de tu presupuesto');
    });

    it('says nothing about a publication when there is no price', () => {
      const fixture = render();

      afford(fixture);

      expect(byId(fixture, 'afford-max-loan')).not.toBeNull();
      expect(byId(fixture, 'afford-fit')).toBeNull();
    });

    it.each([
      ['mortgage-income', '0', 'error-monthlyIncome'],
      ['mortgage-income', '-10', 'error-monthlyIncome'],
      ['mortgage-ratio', '0', 'error-debtRatioPercent'],
      ['mortgage-ratio', '101', 'error-debtRatioPercent'],
      ['mortgage-afford-down', '-5', 'error-affordDownPayment'],
    ])('rejects %s = %s', (id, value, errorId) => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      afford(fixture);

      type(fixture, id, value);

      expect(byId(fixture, errorId)).not.toBeNull();
      if (id !== 'mortgage-afford-down') {
        expect(byId(fixture, 'afford-max-loan')).toBeNull();
      }
    });

    it('goes back to the payment calculator without losing what was typed', () => {
      const fixture = render({ price: 2_000_000, currency: 'MXN' });
      type(fixture, 'mortgage-rate', '11');

      click(fixture, 'mode-afford');
      click(fixture, 'mode-payment');

      expect(field(fixture, 'mortgage-rate').value).toBe('11');
      expect(text(fixture, 'result-payment')).toBe(money(monthlyPayment({ principal: 1_600_000, annualRate: 11, years: 20 })));
    });
  });
});
