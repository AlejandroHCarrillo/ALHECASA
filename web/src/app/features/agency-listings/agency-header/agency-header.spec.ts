import { TestBed } from '@angular/core/testing';
import { AgencyProfile } from '../../agency-profile/agency-profile.models';
import { AgencyHeader } from './agency-header';

const agency = (overrides: Partial<AgencyProfile> = {}): AgencyProfile => ({
  id: 'agency-1',
  name: 'Inmobiliaria Norte',
  email: 'contacto@norte.test',
  phone: '(55) 1234-5678',
  description: 'Casas y departamentos.\nMás de 20 años.',
  website: 'https://norte.test',
  facebookUrl: 'https://facebook.com/norte',
  instagramUrl: 'https://instagram.com/norte',
  openingHours: 'Lun a Vie 9 a 18',
  street: 'Av. Reforma 100',
  city: 'Ciudad de México',
  state: 'CDMX',
  postalCode: '06600',
  hasLogo: true,
  ...overrides,
});

describe('AgencyHeader', () => {
  const render = (value: AgencyProfile) => {
    const fixture = TestBed.createComponent(AgencyHeader);
    fixture.componentRef.setInput('agency', value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const byId = (root: HTMLElement, id: string) => root.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  it('shows everything the agency configured', () => {
    const root = render(agency());

    expect(byId(root, 'agency-name')?.textContent).toBe('Inmobiliaria Norte');
    expect(byId(root, 'description')?.textContent).toContain('Casas y departamentos.');
    expect(byId(root, 'address')?.textContent).toContain('Av. Reforma 100, Ciudad de México, CDMX, 06600');
    expect(byId(root, 'hours')?.textContent).toContain('Lun a Vie 9 a 18');
    expect(byId(root, 'logo')?.getAttribute('src')).toContain('/public/agencies/agency-1/logo');
    expect(byId(root, 'email')?.getAttribute('href')).toBe('mailto:contacto@norte.test');
  });

  it('links the phone with only its digits, so it can be called', () => {
    const root = render(agency());

    expect(byId(root, 'phone')?.getAttribute('href')).toBe('tel:5512345678');
    expect(byId(root, 'phone')?.textContent).toBe('(55) 1234-5678');
  });

  it('opens the external links in another tab without leaking the page or passing ranking', () => {
    const root = render(agency());

    for (const id of ['website', 'facebook', 'instagram']) {
      const link = byId(root, id) as HTMLAnchorElement;
      expect(link.target).toBe('_blank');
      expect(link.rel).toContain('noopener');
      expect(link.rel).toContain('noreferrer');
      expect(link.rel).toContain('nofollow');
    }
    expect(byId(root, 'website')?.getAttribute('href')).toBe('https://norte.test');
    expect(byId(root, 'facebook')?.getAttribute('href')).toBe('https://facebook.com/norte');
    expect(byId(root, 'instagram')?.getAttribute('href')).toBe('https://instagram.com/norte');
  });

  it('shows only what exists', () => {
    const root = render(
      agency({ phone: null, description: null, website: null, facebookUrl: null, instagramUrl: null, openingHours: null, street: null, city: null, state: null, postalCode: null, hasLogo: false }),
    );

    expect(byId(root, 'agency-name')?.textContent).toBe('Inmobiliaria Norte');
    expect(byId(root, 'email')).not.toBeNull();
    for (const id of ['phone', 'description', 'website', 'facebook', 'instagram', 'hours', 'address', 'logo']) {
      expect(byId(root, id), id).toBeNull();
    }
  });

  it('does not turn a link that is not http into something clickable', () => {
    // La API ya lo rechaza al guardar; aquí se comprueba que Angular, por su cuenta, no deja pasar un esquema peligroso.
    const root = render(agency({ website: 'javascript:alert(1)' }));

    expect(byId(root, 'website')?.getAttribute('href')).not.toBe('javascript:alert(1)');
  });
});