import { TestBed } from '@angular/core/testing';
import { MortgageDisclaimer } from './mortgage-disclaimer';

describe('MortgageDisclaimer', () => {
  it('says the numbers are only a reference, not a promise, and sends the user to the bank', () => {
    const fixture = TestBed.createComponent(MortgageDisclaimer);
    fixture.detectChanges();

    const note = fixture.nativeElement.querySelector('[data-testid="disclaimer"]') as HTMLElement;
    const text = note.textContent?.replace(/\s+/g, ' ') as string;
    expect(note.getAttribute('role')).toBe('note');
    expect(text).toContain('solo un parámetro para darte una idea');
    expect(text).toContain('no una promesa');
    expect(text).toContain('consulta a tu banco o institución de financiamiento');
    expect(text).toContain('CAT');
  });
});