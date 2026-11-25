import { HttpParams } from '@angular/common/http';
import {
  AdvancedCriteria,
  NO_ADVANCED_CRITERIA,
  advancedFromUrl,
  advancedToUrl,
  appendAdvancedParams,
  cleanNumber,
  countAdvanced,
  createAdvancedCriteriaForm,
  normalizeAdvanced,
} from './property-criteria';

const full: AdvancedCriteria = {
  minPrice: 100,
  maxPrice: 900,
  currency: 'USD',
  minBedrooms: 2,
  minBathrooms: 1,
  minParking: 1,
  minArea: 50,
  maxArea: 200,
  petsAllowed: true,
  furnished: true,
  amenities: ['Pool', 'Gym'],
  sort: 'PriceDesc',
};

const fromRecord = (record: Record<string, string>) => (key: string) => record[key] ?? null;

describe('property criteria', () => {
  describe('cleanNumber', () => {
    it.each([
      [null, null],
      [undefined, null],
      ['', null],
      ['abc', null],
      [-1, null],
      [Number.NaN, null],
      [Infinity, null],
      [0, 0],
      ['12.5', 12.5],
      [3, 3],
    ])('turns %j into %j', (input, expected) => {
      expect(cleanNumber(input)).toBe(expected);
    });
  });

  describe('normalizeAdvanced', () => {
    it('fills in the defaults, drops invalid numbers and repeated amenities', () => {
      expect(normalizeAdvanced({ minPrice: -5, minBedrooms: 2, amenities: ['Pool', 'Pool', 'Gym'] })).toEqual({
        ...NO_ADVANCED_CRITERIA,
        minBedrooms: 2,
        amenities: ['Pool', 'Gym'],
      });
    });

    it('treats only true as true', () => {
      expect(normalizeAdvanced({ petsAllowed: false, furnished: undefined })).toMatchObject({ petsAllowed: false, furnished: false });
    });
  });

  describe('countAdvanced', () => {
    it('is zero with no filters, and the sort does not count', () => {
      expect(countAdvanced(NO_ADVANCED_CRITERIA)).toBe(0);
      expect(countAdvanced({ ...NO_ADVANCED_CRITERIA, sort: 'PriceAsc' })).toBe(0);
    });

    it('counts price and area once each, and every amenity', () => {
      expect(countAdvanced({ ...NO_ADVANCED_CRITERIA, minPrice: 1, maxPrice: 2 })).toBe(1);
      expect(countAdvanced({ ...NO_ADVANCED_CRITERIA, minArea: 1, maxArea: 2 })).toBe(1);
      expect(countAdvanced(full)).toBe(9);
    });
  });

  describe('appendAdvancedParams', () => {
    it('adds nothing for no filters', () => {
      expect(appendAdvancedParams(new HttpParams(), NO_ADVANCED_CRITERIA).keys()).toEqual([]);
    });

    it('adds every filter, with the currency only because there is a price', () => {
      const params = appendAdvancedParams(new HttpParams(), full);

      expect(params.get('minPrice')).toBe('100');
      expect(params.get('maxPrice')).toBe('900');
      expect(params.get('currency')).toBe('USD');
      expect(params.get('minBedrooms')).toBe('2');
      expect(params.get('minBathrooms')).toBe('1');
      expect(params.get('minParking')).toBe('1');
      expect(params.get('minArea')).toBe('50');
      expect(params.get('maxArea')).toBe('200');
      expect(params.get('petsAllowed')).toBe('true');
      expect(params.get('furnished')).toBe('true');
      expect(params.getAll('amenities')).toEqual(['Pool', 'Gym']);
      expect(params.get('sort')).toBe('PriceDesc');
    });

    it('keeps a bedroom filter of zero (it is a value, not an absence)', () => {
      expect(appendAdvancedParams(new HttpParams(), { ...NO_ADVANCED_CRITERIA, minParking: 0 }).get('minParking')).toBe('0');
    });

    it('does not send the currency without a price', () => {
      expect(appendAdvancedParams(new HttpParams(), { ...NO_ADVANCED_CRITERIA, currency: 'USD' }).has('currency')).toBe(false);
    });
  });

  describe('the address', () => {
    it('writes only the filters that have a value', () => {
      expect(advancedToUrl(NO_ADVANCED_CRITERIA)).toEqual({});
      expect(advancedToUrl({ ...NO_ADVANCED_CRITERIA, minBedrooms: 3, amenities: ['Pool'] })).toEqual({ recamaras: '3', amenidades: 'Pool' });
    });

    it('survives a round trip', () => {
      const url = advancedToUrl(full);

      expect(url).toEqual({
        precioMin: '100',
        precioMax: '900',
        moneda: 'USD',
        recamaras: '2',
        banos: '1',
        estacionamientos: '1',
        m2Min: '50',
        m2Max: '200',
        mascotas: '1',
        amueblada: '1',
        amenidades: 'Pool,Gym',
        orden: 'PriceDesc',
      });
      expect(advancedFromUrl(fromRecord(url))).toEqual(full);
    });

    it('reads an empty address as no filters', () => {
      expect(advancedFromUrl(fromRecord({}))).toEqual(NO_ADVANCED_CRITERIA);
    });

    it('drops what it does not recognize', () => {
      expect(
        advancedFromUrl(fromRecord({ precioMin: 'x', moneda: 'EUR', recamaras: '-1', amenidades: 'Pool,Nope,,Gym,Pool', orden: 'x', mascotas: 'true' })),
      ).toEqual({ ...NO_ADVANCED_CRITERIA, amenities: ['Pool', 'Gym'] });
    });
  });

  it('builds a form that starts with no filters', () => {
    expect(createAdvancedCriteriaForm().getRawValue()).toEqual(NO_ADVANCED_CRITERIA);
  });
});