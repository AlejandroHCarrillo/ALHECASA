import { BillingPeriod, PlanKind, describeLimit } from '../plans/plan.models';
import { cardDigits, formatCardNumber, formatExpiry, isExpired, isValidCardNumber, parseExpiry, describeUsage, usagePercent } from './billing.models';

describe('describeLimit', () => {
  it('describes the listing plans', () => {
    expect(describeLimit(PlanKind.Listings, 100)).toBe('100 publicaciones');
    expect(describeLimit(PlanKind.Listings, 1)).toBe('1 publicación');
    expect(describeLimit(PlanKind.Listings, null)).toBe('Publicaciones ilimitadas');
  });

  it('describes the featured packages as simultaneous slots', () => {
    expect(describeLimit(PlanKind.Featured, 5)).toBe('5 destacados a la vez');
    expect(describeLimit(PlanKind.Featured, 1)).toBe('1 destacado a la vez');
    expect(describeLimit(PlanKind.Featured, null)).toBe('Destacados ilimitados');
  });
});

describe('usage helpers', () => {
  it('reads «used of limit», with unlimited spelled out', () => {
    expect(describeUsage(2, 3)).toBe('2 de 3');
    expect(describeUsage(12, null)).toBe('12 de ilimitado');
  });

  it('turns usage into a 0–100 percentage, capped, and 0 when unlimited', () => {
    expect(usagePercent(1, 4)).toBe(25);
    expect(usagePercent(3, 3)).toBe(100);
    expect(usagePercent(9, 3)).toBe(100);
    expect(usagePercent(5, null)).toBe(0);
    expect(usagePercent(0, 0)).toBe(0);
  });

  it('knows the four billing periods', () => {
    expect(Object.values(BillingPeriod)).toEqual(['Day', 'Week', 'Month', 'Year']);
  });
});

describe('card helpers', () => {
  it('keeps only the digits', () => {
    expect(cardDigits('4242 4242-4242 4242')).toBe('4242424242424242');
  });

  it('groups the number by four while typing, up to 19 digits', () => {
    expect(formatCardNumber('42424')).toBe('4242 4');
    expect(formatCardNumber('4242-4242 4242 4242')).toBe('4242 4242 4242 4242');
    expect(formatCardNumber('12345678901234567890123')).toBe('1234 5678 9012 3456 789');
    expect(formatCardNumber('')).toBe('');
  });

  it('checks the card number with the Luhn algorithm and the length', () => {
    expect(isValidCardNumber('4242 4242 4242 4242')).toBe(true);
    expect(isValidCardNumber('5555555555554444')).toBe(true);
    expect(isValidCardNumber('4000 0000 0000 0002')).toBe(true);
    expect(isValidCardNumber('4242 4242 4242 4241')).toBe(false);
    expect(isValidCardNumber('4242')).toBe(false);
    expect(isValidCardNumber('')).toBe(false);
  });

  it('formats and parses the expiry as MM/AA', () => {
    expect(formatExpiry('1')).toBe('1');
    expect(formatExpiry('122')).toBe('12/2');
    expect(formatExpiry('1228')).toBe('12/28');
    expect(formatExpiry('12/28')).toBe('12/28');
    expect(parseExpiry('12/28')).toEqual({ month: 12, year: 2028 });
    expect(parseExpiry('13/28')).toBeNull();
    expect(parseExpiry('00/28')).toBeNull();
    expect(parseExpiry('1/28')).toBeNull();
    expect(parseExpiry('')).toBeNull();
  });

  it('expires a card at the end of its month', () => {
    const now = new Date(2026, 5, 15); // 15 de junio de 2026
    expect(isExpired(5, 2026, now)).toBe(true);
    expect(isExpired(6, 2026, now)).toBe(false);
    expect(isExpired(1, 2027, now)).toBe(false);
    expect(isExpired(12, 2025, now)).toBe(true);
  });
});
