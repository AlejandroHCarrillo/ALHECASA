import { TestBed } from '@angular/core/testing';
import { convertToParamMap, Params, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { MortgagePage, readPreload } from './mortgage-page';

const params = (values: Params) => convertToParamMap(values);

describe('readPreload', () => {
  it('reads a valid price and currency', () => {
    expect(readPreload(params({ precio: '2500000', moneda: 'MXN' }))).toEqual({ price: 2_500_000, currency: 'MXN' });
  });

  it('accepts decimals and a lowercase currency', () => {
    expect(readPreload(params({ precio: '150000.50', moneda: 'usd' }))).toEqual({ price: 150_000.5, currency: 'USD' });
  });

  it.each(['abc', '-5', '0', '', '1e9', 'Infinity', '12,5', '9999999999999999'])('ignores the price "%s"', (precio) => {
    expect(readPreload(params({ precio })).price).toBeNull();
  });

  it.each(['EUR', 'x', ''])('ignores the currency "%s"', (moneda) => {
    expect(readPreload(params({ moneda })).currency).toBeNull();
  });

  it('returns nothing when the address carries no data', () => {
    expect(readPreload(params({}))).toEqual({ price: null, currency: null });
  });
});

describe('MortgagePage', () => {
  const open = async (url: string) => {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'calculadora-hipotecaria', component: MortgagePage }])] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, MortgagePage);
    harness.detectChanges();
    harness.detectChanges();
    return harness.routeNativeElement as HTMLElement;
  };

  const value = (root: HTMLElement, id: string) => (root.querySelector(`#${id}`) as HTMLInputElement).value;

  it('opens empty, with the public header, when no price is given', async () => {
    const root = await open('/calculadora-hipotecaria');

    expect(root.querySelector('h1')?.textContent).toContain('Calculadora hipotecaria');
    expect(root.querySelector('a[href="/login"]')).not.toBeNull();
    expect(root.querySelector('a[href="/registro"]')).not.toBeNull();
    expect(value(root, 'mortgage-price')).toBe('');
    expect(root.querySelector('[data-testid="no-result"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="result-payment"]')).toBeNull();
  });

  it('preloads the price and currency from the address', async () => {
    const root = await open('/calculadora-hipotecaria?precio=2000000&moneda=USD');

    expect(value(root, 'mortgage-price')).toBe('2000000');
    expect((root.querySelector('[data-testid="currency"]') as HTMLSelectElement).value).toBe('USD');
    expect(root.querySelector('[data-testid="result-payment"]')?.textContent).toContain('USD');
  });

  it('ignores invalid values in the address instead of failing', async () => {
    const root = await open('/calculadora-hipotecaria?precio=basura&moneda=EUR');

    expect(value(root, 'mortgage-price')).toBe('');
    expect((root.querySelector('[data-testid="currency"]') as HTMLSelectElement).value).toBe('MXN');
    expect(root.querySelector('[data-testid="no-result"]')).not.toBeNull();
  });
});