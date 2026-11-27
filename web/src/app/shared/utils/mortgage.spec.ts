import {
  LoanTerms,
  affordability,
  amortizationSchedule,
  groupByYear,
  isValidLoan,
  loanSummary,
  monthlyPayment,
  upfrontCosts,
} from './mortgage';

const loan = (overrides: Partial<LoanTerms> = {}): LoanTerms => ({ principal: 1_000_000, annualRate: 10, years: 20, ...overrides });

describe('monthlyPayment', () => {
  it('matches known values of the fixed-payment formula', () => {
    expect(monthlyPayment(loan())).toBeCloseTo(9650.22, 2);
    expect(monthlyPayment(loan({ principal: 2_000_000, annualRate: 9.5, years: 15 }))).toBeCloseTo(20884.49, 2);
    expect(monthlyPayment(loan({ principal: 500_000, annualRate: 12, years: 10 }))).toBeCloseTo(7173.55, 2);
  });

  it('with a 0 % rate is the principal split evenly, not a division by zero', () => {
    const payment = monthlyPayment(loan({ principal: 1_200_000, annualRate: 0, years: 10 }));

    expect(payment).toBe(10_000);
    expect(Number.isFinite(payment)).toBe(true);
  });

  it('handles the shortest term', () => {
    expect(monthlyPayment(loan({ principal: 12_000, annualRate: 0, years: 1 }))).toBe(1000);
    expect(monthlyPayment(loan({ principal: 12_000, annualRate: 12, years: 1 }))).toBeCloseTo(1066.19, 1);
  });

  it('a higher rate or a shorter term means a higher payment', () => {
    expect(monthlyPayment(loan({ annualRate: 12 }))).toBeGreaterThan(monthlyPayment(loan({ annualRate: 10 })));
    expect(monthlyPayment(loan({ years: 10 }))).toBeGreaterThan(monthlyPayment(loan({ years: 20 })));
  });

  it.each([
    ['no amount', { principal: 0 }],
    ['a negative amount', { principal: -1 }],
    ['a rate below 0', { annualRate: -1 }],
    ['a rate above 100', { annualRate: 101 }],
    ['a term below one year', { years: 0.5 }],
    ['a term above forty years', { years: 41 }],
    ['an amount that is not a number', { principal: Number.NaN }],
    ['an infinite amount', { principal: Number.POSITIVE_INFINITY }],
    ['a rate that is not a number', { annualRate: Number.NaN }],
    ['a term that is not a number', { years: Number.NaN }],
  ])('is not calculable with %s, and says so instead of returning Infinity', (_, overrides) => {
    expect(isValidLoan(loan(overrides))).toBe(false);
    expect(monthlyPayment(loan(overrides))).toBeNaN();
    expect(amortizationSchedule(loan(overrides))).toEqual([]);
    expect(loanSummary(loan(overrides))).toBeNull();
  });

  it('accepts the limits', () => {
    expect(isValidLoan(loan({ years: 1 }))).toBe(true);
    expect(isValidLoan(loan({ years: 40 }))).toBe(true);
    expect(isValidLoan(loan({ annualRate: 0 }))).toBe(true);
    expect(isValidLoan(loan({ annualRate: 100 }))).toBe(true);
  });
});

describe('amortizationSchedule', () => {
  it('has one row per month', () => {
    expect(amortizationSchedule(loan())).toHaveLength(240);
    expect(amortizationSchedule(loan({ years: 1 }))).toHaveLength(12);
  });

  it('the principal repaid adds up to the loan, and the debt ends exactly at zero', () => {
    for (const terms of [loan(), loan({ annualRate: 0 }), loan({ principal: 777_777.77, annualRate: 8.35, years: 17 }), loan({ years: 1 })]) {
      const rows = amortizationSchedule(terms);

      expect(rows.reduce((sum, row) => sum + row.principal, 0)).toBeCloseTo(terms.principal, 4);
      expect(rows.at(-1)?.balance).toBe(0);
    }
  });

  it('starts with mostly interest and ends with mostly principal', () => {
    const rows = amortizationSchedule(loan());

    expect(rows[0].interest).toBeCloseTo(1_000_000 * (0.1 / 12), 6);
    expect(rows[0].principal).toBeCloseTo(9650.22 - 8333.33, 1);
    expect(rows[0].interest).toBeGreaterThan(rows[0].principal);
    expect(rows.at(-1)!.principal).toBeGreaterThan(rows.at(-1)!.interest);
  });

  it('every payment is the fixed payment (the last one only absorbs rounding) and the debt only goes down', () => {
    const rows = amortizationSchedule(loan());
    const payment = monthlyPayment(loan());

    for (const row of rows) {
      expect(row.payment).toBeCloseTo(payment, 4);
      expect(row.principal + row.interest).toBeCloseTo(row.payment, 6);
    }
    for (let i = 1; i < rows.length; i++) {
      expect(rows[i].balance).toBeLessThan(rows[i - 1].balance);
    }
  });

  it('with a 0 % rate there are no interests', () => {
    const rows = amortizationSchedule(loan({ annualRate: 0, years: 2 }));

    expect(rows.every((row) => row.interest === 0)).toBe(true);
    expect(rows[0].principal).toBeCloseTo(1_000_000 / 24, 6);
  });
});

describe('groupByYear', () => {
  it('adds up each year of twelve payments and keeps the balance at the end of the year', () => {
    const rows = amortizationSchedule(loan({ years: 5 }));
    const years = groupByYear(rows);

    expect(years.map((y) => y.year)).toEqual([1, 2, 3, 4, 5]);
    expect(years[0].payment).toBeCloseTo(monthlyPayment(loan({ years: 5 })) * 12, 4);
    expect(years[0].balance).toBe(rows[11].balance);
    expect(years.at(-1)?.balance).toBe(0);
    expect(years.reduce((sum, y) => sum + y.principal, 0)).toBeCloseTo(1_000_000, 4);
    expect(years.reduce((sum, y) => sum + y.interest, 0)).toBeCloseTo(rows.reduce((sum, r) => sum + r.interest, 0), 4);
  });

  it('is empty for an empty schedule', () => {
    expect(groupByYear([])).toEqual([]);
  });
});

describe('loanSummary', () => {
  it('totals what is paid and what is paid in interest', () => {
    const summary = loanSummary(loan())!;

    expect(summary.monthlyPayment).toBeCloseTo(9650.22, 2);
    expect(summary.totalPaid).toBeCloseTo(summary.monthlyPayment * 240, 6);
    expect(summary.totalPaid).toBeCloseTo(2_316_051.95, 0);
    expect(summary.totalInterest).toBeCloseTo(summary.totalPaid - 1_000_000, 6);
    expect(summary.interestRatio).toBeCloseTo(summary.totalInterest / 1_000_000, 10);
  });

  it('agrees with the sum of the schedule', () => {
    const terms = loan({ principal: 3_250_000, annualRate: 11.2, years: 25 });
    const summary = loanSummary(terms)!;
    const rows = amortizationSchedule(terms);

    expect(summary.totalPaid).toBeCloseTo(rows.reduce((sum, r) => sum + r.payment, 0), 2);
    expect(summary.totalInterest).toBeCloseTo(rows.reduce((sum, r) => sum + r.interest, 0), 2);
  });

  it('has no interests at 0 %', () => {
    expect(loanSummary(loan({ annualRate: 0 }))?.totalInterest).toBeCloseTo(0, 6);
  });
});

describe('upfrontCosts', () => {
  it('adds the down payment and every optional cost', () => {
    const costs = upfrontCosts({ price: 2_000_000, downPayment: 400_000, openingFeePercent: 1, appraisal: 5_000, closingCostsPercent: 5 });

    expect(costs.downPayment).toBe(400_000);
    expect(costs.openingFee).toBe(16_000); // 1 % de un crédito de 1,600,000
    expect(costs.appraisal).toBe(5_000);
    expect(costs.closingCosts).toBe(100_000); // 5 % del precio
    expect(costs.total).toBe(521_000);
  });

  it('charges only what was indicated', () => {
    const costs = upfrontCosts({ price: 2_000_000, downPayment: 400_000 });

    expect(costs).toEqual({ downPayment: 400_000, openingFee: 0, appraisal: 0, closingCosts: 0, total: 400_000 });
  });

  it('ignores negative and non-numeric amounts instead of lowering the total', () => {
    const costs = upfrontCosts({ price: 1_000_000, downPayment: 200_000, openingFeePercent: -3, appraisal: Number.NaN, closingCostsPercent: -1 });

    expect(costs.total).toBe(200_000);
  });

  it('never has a negative loan when the down payment exceeds the price', () => {
    expect(upfrontCosts({ price: 100, downPayment: 500, openingFeePercent: 1 }).openingFee).toBe(0);
  });
});

describe('affordability', () => {
  const inputs = { monthlyIncome: 50_000, debtRatioPercent: 30, annualRate: 10, years: 20 };

  it('is the exact inverse of the monthly payment', () => {
    const result = affordability(inputs)!;

    expect(result.maxMonthlyPayment).toBe(15_000);
    expect(monthlyPayment({ principal: result.maxLoan, annualRate: 10, years: 20 })).toBeCloseTo(15_000, 6);
  });

  it('gives a known loan for a known income', () => {
    expect(affordability(inputs)!.maxLoan).toBeCloseTo(1_554_369, 0);
  });

  it('adds the down payment to get the price', () => {
    const result = affordability({ ...inputs, downPayment: 300_000 })!;

    expect(result.maxPrice).toBeCloseTo(result.maxLoan + 300_000, 6);
  });

  it('takes the insurance out of what can be paid', () => {
    const without = affordability(inputs)!;
    const withInsurance = affordability({ ...inputs, monthlyInsurance: 1_500 })!;

    expect(withInsurance.maxMonthlyPayment).toBe(13_500);
    expect(withInsurance.maxLoan).toBeCloseTo(without.maxLoan * 0.9, 4);
  });

  it('cannot lend anything when the insurance alone eats the whole payment', () => {
    const result = affordability({ ...inputs, monthlyInsurance: 20_000 })!;

    expect(result.maxMonthlyPayment).toBe(0);
    expect(result.maxLoan).toBe(0);
  });

  it('works with a 0 % rate', () => {
    expect(affordability({ ...inputs, annualRate: 0, years: 10 })!.maxLoan).toBe(15_000 * 120);
  });

  it('a higher income, a longer term or a lower rate each allow a bigger loan', () => {
    const base = affordability(inputs)!.maxLoan;

    expect(affordability({ ...inputs, monthlyIncome: 60_000 })!.maxLoan).toBeGreaterThan(base);
    expect(affordability({ ...inputs, years: 30 })!.maxLoan).toBeGreaterThan(base);
    expect(affordability({ ...inputs, annualRate: 8 })!.maxLoan).toBeGreaterThan(base);
  });

  it.each([
    ['no income', { monthlyIncome: 0 }],
    ['a negative income', { monthlyIncome: -5 }],
    ['no share of the income', { debtRatioPercent: 0 }],
    ['more than all of the income', { debtRatioPercent: 101 }],
    ['an invalid rate', { annualRate: -2 }],
    ['an invalid term', { years: 0 }],
    ['an income that is not a number', { monthlyIncome: Number.NaN }],
  ])('is not calculable with %s', (_, overrides) => {
    expect(affordability({ ...inputs, ...overrides })).toBeNull();
  });
});
