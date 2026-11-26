import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs';

/** Lo que una página pública le dice a los buscadores y a las vistas previas al compartir el enlace. */
export interface SeoPage {
  /** Título de la pestaña y del resultado de búsqueda. */
  title: string;
  /** Resumen de la página (conviene de 50 a 160 caracteres). */
  description?: string;
  /** Dirección propia de la página, sin dominio (`/venta/guadalajara`). Con ella se fija la dirección canónica. */
  path?: string;
  /** Pide a los buscadores que no la incluyan (páginas sin contenido). Sigue permitiendo seguir sus enlaces. */
  noindex?: boolean;
  /** Imagen para la vista previa al compartir. Puede ser relativa al sitio. */
  image?: string;
  /** Datos estructurados (schema.org) de la página. */
  jsonLd?: object;
}

const CANONICAL = 'link[rel="canonical"]';
const JSON_LD_ID = 'seo-json-ld';
const SITE_NAME = 'ALHECASA';

/**
 * Título, descripción, dirección canónica, vista previa social (Open Graph) y datos estructurados de las páginas públicas.
 *
 * Las etiquetas viven en el `<head>` del documento y una aplicación de una sola página no lo recarga, así que se limpian en cada
 * navegación: lo que una página fijó no debe quedar en la siguiente. Cada página pública fija las suyas al cargar sus datos.
 *
 * Limitación: sin renderizado en servidor, solo ven estas etiquetas los rastreadores que ejecutan JavaScript (Google sí; muchas
 * vistas previas de redes sociales no). Ver documentacion/arquitectura.md.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  /** Ruta (sin parámetros) de la página en pantalla: cambiar solo los parámetros (filtros, página) no la renueva, y sus etiquetas siguen valiendo. */
  private path = '';

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationStart => event instanceof NavigationStart))
      .subscribe((event) => {
        const path = event.url.split(/[?#]/)[0];
        if (path !== this.path) {
          this.path = path;
          this.clear();
        }
      });
  }

  /** Fija las etiquetas de la página. Reemplaza lo anterior: lo que no se indica, se quita. */
  set(page: SeoPage): void {
    this.clear();
    this.titleService.setTitle(page.title);

    if (page.description) {
      this.meta.updateTag({ name: 'description', content: page.description });
      this.meta.updateTag({ property: 'og:description', content: page.description });
    }

    if (page.noindex) {
      this.meta.updateTag({ name: 'robots', content: 'noindex,follow' });
    }

    this.meta.updateTag({ property: 'og:title', content: page.title });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: SITE_NAME });
    this.meta.updateTag({ property: 'og:locale', content: 'es_MX' });

    if (page.path !== undefined) {
      const url = this.absolute(page.path);
      this.setCanonical(url);
      this.meta.updateTag({ property: 'og:url', content: url });
    }

    if (page.image) {
      this.meta.updateTag({ property: 'og:image', content: this.absolute(page.image) });
    }

    this.meta.updateTag({ name: 'twitter:card', content: page.image ? 'summary_large_image' : 'summary' });

    if (page.jsonLd) {
      this.setJsonLd(page.jsonLd);
    }
  }

  /** Dirección absoluta (con el dominio desde el que se sirve el sitio) de una ruta o de una dirección relativa. */
  absolute(path: string): string {
    return new URL(path, this.document.location.origin).toString();
  }

  private clear(): void {
    for (const selector of [
      'name="description"',
      'name="robots"',
      'name="twitter:card"',
      'property="og:title"',
      'property="og:description"',
      'property="og:type"',
      'property="og:site_name"',
      'property="og:locale"',
      'property="og:url"',
      'property="og:image"',
    ]) {
      this.meta.removeTag(selector);
    }

    this.document.head.querySelector(CANONICAL)?.remove();
    this.document.getElementById(JSON_LD_ID)?.remove();
  }

  private setCanonical(url: string): void {
    const link = this.document.createElement('link');
    link.setAttribute('rel', 'canonical');
    link.setAttribute('href', url);
    this.document.head.appendChild(link);
  }

  private setJsonLd(data: object): void {
    const script = this.document.createElement('script');
    script.id = JSON_LD_ID;
    script.type = 'application/ld+json';
    // `<` se escapa para que un texto como «</script>» no pueda cerrar la etiqueta.
    script.textContent = JSON.stringify(data).replace(/</g, '\\u003c');
    this.document.head.appendChild(script);
  }
}
