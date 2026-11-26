import { mainOperation, operationCount, zoneCommands, zonePath } from './zone.models';

describe('zone.models', () => {
  it('builds the router commands of a city and of a neighborhood', () => {
    expect(zoneCommands('venta', 'guadalajara')).toEqual(['/venta', 'guadalajara']);
    expect(zoneCommands('renta', 'guadalajara', 'providencia')).toEqual(['/renta', 'guadalajara', 'providencia']);
    expect(zoneCommands('renta', 'guadalajara', null)).toEqual(['/renta', 'guadalajara']);
  });

  it('builds the canonical path, encoding each part', () => {
    expect(zonePath('venta', 'guadalajara', 'providencia')).toBe('/venta/guadalajara/providencia');
    expect(zonePath('venta', 'a b')).toBe('/venta/a%20b');
  });

  it('links to the operation with more properties, and to sale on a tie', () => {
    expect(mainOperation({ saleCount: 3, rentCount: 1 })).toBe('venta');
    expect(mainOperation({ saleCount: 1, rentCount: 3 })).toBe('renta');
    expect(mainOperation({ saleCount: 2, rentCount: 2 })).toBe('venta');
    expect(mainOperation({ saleCount: 0, rentCount: 1 })).toBe('renta');
  });

  it('counts by operation', () => {
    const zone = { saleCount: 5, rentCount: 2 };
    expect(operationCount(zone, 'venta')).toBe(5);
    expect(operationCount(zone, 'renta')).toBe(2);
  });
});
