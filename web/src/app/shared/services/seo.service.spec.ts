import { DOCUMENT } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta, Title } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { SeoService } from './seo.service';

@Component({ selector: 'app-blank', template: '' })
class Blank {}

describe('SeoService', () => {
  let seo: SeoService;
  let router: Router;
  let doc: Document;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'one', component: Blank },
          { path: 'two', component: Blank },
        ]),
      ],
    });
    seo = TestBed.inject(SeoService);
    router = TestBed.inject(Router);
    doc = TestBed.inject(DOCUMENT);
  });

  afterEach(() => {
    // Lo que la prueba dejó en el <head> no debe pasar a la siguiente.
    seo.set({ title: 'reset' });
    doc.head.querySelectorAll('meta,link[rel="canonical"],script#seo-json-ld').forEach((e) => e.remove());
  });

  const meta = (selector: string) => TestBed.inject(Meta).getTag(selector)?.getAttribute('content') ?? null;
  const canonical = () => doc.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;

  it('sets the title, description, social preview and canonical address', () => {
    seo.set({ title: 'Casas en Zapopan', description: 'Las mejores casas.', path: '/venta/zapopan', image: '/api/images/a' });

    expect(TestBed.inject(Title).getTitle()).toBe('Casas en Zapopan');
    expect(meta('name="description"')).toBe('Las mejores casas.');
    expect(meta('property="og:title"')).toBe('Casas en Zapopan');
    expect(meta('property="og:description"')).toBe('Las mejores casas.');
    expect(meta('property="og:locale"')).toBe('es_MX');
    expect(meta('name="twitter:card"')).toBe('summary_large_image');
    expect(canonical()).toBe(`${location.origin}/venta/zapopan`);
    expect(meta('property="og:url"')).toBe(`${location.origin}/venta/zapopan`);
    expect(meta('property="og:image"')).toBe(`${location.origin}/api/images/a`);
  });

  it('does not index a page marked noindex, but lets its links be followed', () => {
    seo.set({ title: 'Vacía', noindex: true });
    expect(meta('name="robots"')).toBe('noindex,follow');

    seo.set({ title: 'Con contenido' });
    expect(meta('name="robots"')).toBeNull();
  });

  it('replaces what the previous page set instead of adding to it', () => {
    seo.set({ title: 'A', description: 'Descripción A', path: '/a', image: '/a.png', noindex: true });
    seo.set({ title: 'B' });

    expect(meta('name="description"')).toBeNull();
    expect(meta('property="og:image"')).toBeNull();
    expect(meta('name="twitter:card"')).toBe('summary');
    expect(canonical()).toBeNull();
    expect(doc.head.querySelectorAll('link[rel="canonical"]').length).toBe(0);
  });

  it('keeps a single canonical link and a single JSON-LD block however many times it is set', () => {
    seo.set({ title: 'A', path: '/a', jsonLd: { '@type': 'A' } });
    seo.set({ title: 'B', path: '/b', jsonLd: { '@type': 'B' } });

    expect(doc.head.querySelectorAll('link[rel="canonical"]').length).toBe(1);
    expect(canonical()).toBe(`${location.origin}/b`);
    const scripts = doc.head.querySelectorAll('script#seo-json-ld');
    expect(scripts.length).toBe(1);
    expect(JSON.parse(scripts[0].textContent ?? '')).toEqual({ '@type': 'B' });
  });

  it('escapes "<" in the structured data so a text cannot close the script', () => {
    seo.set({ title: 'X', jsonLd: { name: '</script><b>' } });

    const raw = doc.head.querySelector('script#seo-json-ld')?.textContent ?? '';
    expect(raw).not.toContain('<');
    expect(JSON.parse(raw)).toEqual({ name: '</script><b>' });
  });

  it('clears the tags when navigating to another page', async () => {
    await router.navigateByUrl('/one');
    seo.set({ title: 'Uno', description: 'd', path: '/one' });

    await router.navigateByUrl('/two');

    expect(meta('name="description"')).toBeNull();
    expect(canonical()).toBeNull();
  });

  it('keeps the tags when only the query parameters change', async () => {
    await router.navigateByUrl('/one');
    seo.set({ title: 'Uno', description: 'd', path: '/one' });

    await router.navigateByUrl('/one?page=2');

    expect(meta('name="description"')).toBe('d');
    expect(canonical()).toBe(`${location.origin}/one`);
  });

  it('builds absolute addresses from the site origin', () => {
    expect(seo.absolute('/venta/zapopan')).toBe(`${location.origin}/venta/zapopan`);
  });
});
