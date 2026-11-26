import { TestBed } from '@angular/core/testing';
import { PrivacyNotice } from './privacy-notice';

describe('PrivacyNotice', () => {
  const render = (action?: string) => {
    const fixture = TestBed.createComponent(PrivacyNotice);
    if (action) {
      fixture.componentRef.setInput('action', action);
    }
    fixture.detectChanges();
    return fixture;
  };
  const root = (fixture: ReturnType<typeof render>) => fixture.nativeElement as HTMLElement;
  const click = (fixture: ReturnType<typeof render>, id: string) => {
    (root(fixture).querySelector(`[data-testid="${id}"]`) as HTMLElement).click();
    fixture.detectChanges();
    fixture.detectChanges();
  };

  beforeEach(() => {
    // jsdom no implementa <dialog>.showModal: basta con que no falle.
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  it('shows the legend with the action of the form and does not open the notice by itself', () => {
    const fixture = render('enviar la solicitud');

    expect(root(fixture).querySelector('[data-testid="privacy-legend"]')?.textContent?.replace(/\s+/g, ' ')).toContain(
      'Al enviar la solicitud, tus datos se tratarán conforme al aviso de privacidad.',
    );
    expect(root(fixture).querySelector('app-privacy-notice-modal')).toBeNull();
  });

  it('opens the notice in a modal and closes it with "Entendido"', () => {
    const fixture = render();

    click(fixture, 'privacy-open');
    const notice = root(fixture).querySelector('[data-testid="privacy-notice"]') as HTMLElement;
    expect(root(fixture).querySelector('dialog')).not.toBeNull();
    expect(root(fixture).textContent).toContain('Aviso de privacidad');
    expect(notice.textContent).toContain('Qué datos recabamos');
    expect(notice.textContent).toContain('Con quién se comparten');
    expect(notice.textContent).toContain('derechos ARCO');

    click(fixture, 'privacy-close');
    expect(root(fixture).querySelector('app-privacy-notice-modal')).toBeNull();
  });

  it('never submits the form it lives in when the link is pressed', () => {
    const fixture = render();
    const link = root(fixture).querySelector('[data-testid="privacy-open"]') as HTMLButtonElement;

    expect(link.type).toBe('button');
  });
});