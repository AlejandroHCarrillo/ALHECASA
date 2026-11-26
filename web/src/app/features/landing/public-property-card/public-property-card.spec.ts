import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicProperty } from '../public-properties.service';
import { PublicPropertyCard } from './public-property-card';

const sample = (overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id: 'p-1',
  title: 'Casa moderna',
  description: null,
  type: 'House',
  operation: 'Sale',
  price: 1_000_000,
  currency: 'MXN',
  neighborhood: null,
  city: 'Puebla',
  state: 'Puebla',
  bedrooms: null,
  bathrooms: null,
  constructionArea: null,
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: [],
  agencyName: null,
  agencyId: null,
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  ...overrides,
});

describe('PublicPropertyCard: new details', () => {
  const render = (data: PublicProperty): ComponentFixture<PublicPropertyCard> => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(PublicPropertyCard);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  };

  const query = (fixture: ComponentFixture<PublicPropertyCard>, selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement | null;

  beforeEach(() => localStorage.clear());

  it('shows nothing extra when the property has none of the new details', () => {
    const fixture = render(sample());

    expect(query(fixture, '[data-testid="parking"]')).toBeNull();
    expect(query(fixture, '[data-testid="badge-pets"]')).toBeNull();
    expect(query(fixture, '[data-testid="badge-furnished"]')).toBeNull();
  });

  it('shows the parking spaces, with the right plural', () => {
    expect(query(render(sample({ parkingSpaces: 1 })), '[data-testid="parking"]')?.textContent).toContain('1 estac.');
    TestBed.resetTestingModule();
    expect(query(render(sample({ parkingSpaces: 3 })), '[data-testid="parking"]')?.textContent).toContain('3 estacs.');
  });

  it('shows zero parking spaces as a fact, not as a missing value', () => {
    expect(query(render(sample({ parkingSpaces: 0 })), '[data-testid="parking"]')?.textContent).toContain('0 estacs.');
  });

  it('badges the pet-friendly and furnished properties', () => {
    const fixture = render(sample({ petsAllowed: true, furnished: true }));

    expect(query(fixture, '[data-testid="badge-pets"]')?.textContent).toContain('Mascotas');
    expect(query(fixture, '[data-testid="badge-furnished"]')?.textContent).toContain('Amueblada');
  });

  it('badges a featured property and only a featured one', () => {
    expect(query(render(sample({ isFeatured: true })), '[data-testid="featured-badge"]')?.textContent).toContain('Destacado');
    TestBed.resetTestingModule();
    expect(query(render(sample({ isFeatured: false })), '[data-testid="featured-badge"]')).toBeNull();
  });
});

describe('PublicPropertyCard: who publishes', () => {
  const render = (data: PublicProperty): ComponentFixture<PublicPropertyCard> => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(PublicPropertyCard);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => localStorage.clear());

  it('links the agency name to all of its active publications', () => {
    const fixture = render(sample({ agencyName: 'Demo Real Estate', agencyId: 'agency-7' }));

    const link = fixture.nativeElement.querySelector('[data-testid="agency-link"]') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Demo Real Estate');
    expect(link.getAttribute('href')).toBe('/inmobiliarias/agency-7');
    expect(fixture.nativeElement.textContent).toContain('Publica:');
  });

  it('shows an individual as plain text: it is anonymous and has no page', () => {
    const fixture = render(sample({ agencyName: 'Particular', agencyId: null }));

    expect(fixture.nativeElement.querySelector('[data-testid="agency-link"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Publica:');
    expect(fixture.nativeElement.textContent).toContain('Particular');
  });

  it('shows no publisher line when there is no name', () => {
    const fixture = render(sample({ agencyName: null, agencyId: null }));

    expect(fixture.nativeElement.textContent).not.toContain('Publica:');
  });
});
