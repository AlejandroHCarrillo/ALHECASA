import { TestBed } from '@angular/core/testing';
import { Logo } from './logo';

describe('Logo', () => {
  const render = (inputs: Record<string, unknown> = {}) => {
    const fixture = TestBed.createComponent(Logo);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const fills = (el: HTMLElement) => Array.from(el.querySelectorAll('path')).map((p) => p.getAttribute('fill'));

  it('uses the brand blue and petrol green on the main variant', () => {
    const el = render();

    expect(fills(el)).toEqual(['#0F766E', '#142D4E']);
    expect(el.textContent).toContain('ALHECASA');
    expect(el.textContent).not.toContain('El lugar donde comienza tu historia.');
  });

  it('turns white on the negative variant, for blue backgrounds', () => {
    const el = render({ negative: true });

    expect(fills(el)[1]).toBe('#ffffff');
    expect(el.querySelector('.text-white')).not.toBeNull();
  });

  it('shows only the mark when collapsed, and hides the svg from screen readers', () => {
    const el = render({ markOnly: true });

    expect(el.textContent).not.toContain('ALHECASA');
    expect(el.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('shows the tagline when asked', () => {
    expect(render({ tagline: true }).textContent).toContain('El lugar donde comienza tu historia.');
  });
});
