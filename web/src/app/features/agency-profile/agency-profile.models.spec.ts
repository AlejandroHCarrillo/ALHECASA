import { environment } from '../../../environments/environment';
import { agencyLogoUrl, formatAddress } from './agency-profile.models';

describe('agencyLogoUrl', () => {
  it('points at the public logo of the agency', () => {
    expect(agencyLogoUrl('a-1')).toBe(`${environment.apiUrl}/public/agencies/a-1/logo`);
  });

  it('adds a version so the browser asks again after a change', () => {
    expect(agencyLogoUrl('a-1', 123)).toBe(`${environment.apiUrl}/public/agencies/a-1/logo?v=123`);
  });
});

describe('formatAddress', () => {
  it('joins what there is, in order', () => {
    expect(formatAddress({ street: 'Av. Reforma 100', city: 'Ciudad de México', state: 'CDMX', postalCode: '06600' })).toBe(
      'Av. Reforma 100, Ciudad de México, CDMX, 06600',
    );
  });

  it('skips what is missing', () => {
    expect(formatAddress({ street: null, city: 'Puebla', state: 'Puebla', postalCode: null })).toBe('Puebla, Puebla');
  });

  it('is empty when nothing was captured', () => {
    expect(formatAddress({ street: null, city: null, state: null, postalCode: null })).toBe('');
  });
});