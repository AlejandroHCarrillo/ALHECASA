import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LIKES_KEY } from '../../../core/likes/likes.service';
import { LikeButton } from './like-button';

@Component({
  selector: 'app-like-button-host',
  imports: [LikeButton],
  template: `
    <a href="#detalle" (click)="navigated = true">
      <app-like-button [propertyId]="id" title="Casa bonita" [showLabel]="showLabel" />
    </a>
  `,
})
class Host {
  id = 'p-1';
  showLabel = false;
  navigated = false;
}

describe('LikeButton', () => {
  let fixture: ComponentFixture<Host>;

  const create = (setup: (host: Host) => void = () => undefined): void => {
    fixture = TestBed.createComponent(Host);
    setup(fixture.componentInstance);
    fixture.detectChanges();
  };
  const button = () => fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  const heart = () => fixture.nativeElement.querySelector('i') as HTMLElement;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows an empty heart that is not pressed, with an accessible name', () => {
    create();

    expect(heart().classList).toContain('fa-regular');
    expect(heart().classList).not.toContain('fa-solid');
    expect(button().getAttribute('aria-pressed')).toBe('false');
    expect(button().getAttribute('aria-label')).toBe('Me gusta: Casa bonita');
  });

  it('fills the heart when clicked, saves it, and clears it on a second click', () => {
    create();

    button().click();
    fixture.detectChanges();

    expect(heart().classList).toContain('fa-solid');
    expect(heart().classList).toContain('text-error');
    expect(button().getAttribute('aria-pressed')).toBe('true');
    expect(button().getAttribute('aria-label')).toBe('Quitar me gusta: Casa bonita');
    expect(JSON.parse(localStorage.getItem(LIKES_KEY) ?? '[]')).toEqual(['p-1']);

    button().click();
    fixture.detectChanges();

    expect(heart().classList).toContain('fa-regular');
    expect(JSON.parse(localStorage.getItem(LIKES_KEY) ?? '[]')).toEqual([]);
  });

  it('starts filled when the publication was already liked', () => {
    localStorage.setItem(LIKES_KEY, JSON.stringify(['p-1']));

    create();

    expect(heart().classList).toContain('fa-solid');
    expect(button().getAttribute('aria-pressed')).toBe('true');
  });

  it('does not trigger the link that surrounds it', () => {
    create();

    button().click();

    expect(fixture.componentInstance.navigated).toBe(false);
  });

  it('shows a text label only when asked', () => {
    create();
    expect(button().textContent?.trim()).toBe('');

    TestBed.resetTestingModule();
    create((host) => (host.showLabel = true));
    expect(button().textContent).toContain('Me gusta');

    button().click();
    fixture.detectChanges();
    expect(button().textContent).toContain('Te gusta');
  });
});
