import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** Clave en localStorage. Es independiente de la sesión: cerrar sesión no borra los «me gusta». */
export const LIKES_KEY = 'alhecasa.likes';

/**
 * Tope de publicaciones guardadas. Es el mismo máximo que acepta el catálogo público por solicitud (PublicPropertyQuery.MaxIds),
 * porque «Mis favoritas» las pide todas juntas en la dirección.
 */
const MAX_LIKES = 100;

/** Un Guid mide 36 caracteres; se deja margen, pero se descarta cualquier cosa que no parezca un identificador. */
const MAX_ID_LENGTH = 64;

/**
 * Publicaciones que le gustan al visitante. Se guardan en el navegador (localStorage), no en el servidor,
 * para que funcionen igual con o sin sesión iniciada. Consecuencias: no viajan entre dispositivos ni navegadores,
 * y se pierden si el visitante borra los datos del sitio.
 */
@Injectable({ providedIn: 'root' })
export class LikesService {
  private readonly liked = signal<string[]>(readLikes());
  private readonly likedSet = computed(() => new Set(this.liked()));

  /** Identificadores de las publicaciones que le gustan, de la más antigua a la más reciente. */
  readonly ids = this.liked.asReadonly();

  /** Cantidad de publicaciones que le gustan al visitante. */
  readonly count = computed(() => this.liked().length);

  constructor() {
    // Si el visitante cambia un «me gusta» en otra pestaña, esta pestaña se entera.
    const onStorage = (event: StorageEvent): void => {
      if (event.key === LIKES_KEY || event.key === null) {
        this.liked.set(readLikes());
      }
    };
    window.addEventListener('storage', onStorage);
    inject(DestroyRef).onDestroy(() => window.removeEventListener('storage', onStorage));
  }

  isLiked(propertyId: string): boolean {
    return this.likedSet().has(propertyId);
  }

  /** Agrega o quita el «me gusta». Devuelve true si la publicación quedó como «me gusta». */
  toggle(propertyId: string): boolean {
    const current = this.liked();
    const nowLiked = !current.includes(propertyId);

    // Las más recientes quedan al final; al llegar al tope se descartan las más antiguas.
    const next = nowLiked ? [...current, propertyId].slice(-MAX_LIKES) : current.filter((id) => id !== propertyId);

    this.liked.set(next);
    writeLikes(next);
    return nowLiked;
  }
}

/** Lee la lista guardada; ante datos dañados o almacenamiento no disponible, empieza vacía. */
function readLikes(): string[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(LIKES_KEY) ?? '[]');
    if (!Array.isArray(parsed)) {
      return [];
    }

    const ids = parsed.filter((item): item is string => typeof item === 'string' && item.length > 0 && item.length <= MAX_ID_LENGTH);
    return [...new Set(ids)].slice(-MAX_LIKES);
  } catch {
    return [];
  }
}

/**
 * Si el navegador no deja guardar (modo privado, cuota llena, almacenamiento desactivado), el «me gusta»
 * sigue marcado mientras la pestaña esté abierta, pero no se conserva.
 */
function writeLikes(ids: string[]): void {
  try {
    localStorage.setItem(LIKES_KEY, JSON.stringify(ids));
  } catch {
    /* sin persistencia */
  }
}
