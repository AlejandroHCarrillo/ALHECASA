import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { LikesService } from '../../../core/likes/likes.service';

/**
 * Botón de corazón para marcar una publicación con «me gusta». Funciona sin sesión: el estado vive en el navegador.
 * Sin `showLabel` es un botón redondo pensado para ir encima de una foto; con él, un botón con texto.
 * Detiene el clic para que, si se coloca dentro de un enlace, no abra el detalle.
 */
@Component({
  selector: 'app-like-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="btn btn-sm"
      [class]="showLabel() ? 'btn-outline gap-2' : 'btn-circle border-0 bg-base-100/90 shadow'"
      [attr.aria-pressed]="liked()"
      [attr.aria-label]="label()"
      [attr.title]="label()"
      (click)="toggle($event)"
    >
      <i
        class="fa-heart"
        [class.fa-solid]="liked()"
        [class.fa-regular]="!liked()"
        [class.text-error]="liked()"
        aria-hidden="true"
      ></i>
      @if (showLabel()) {
        <span>{{ liked() ? 'Te gusta' : 'Me gusta' }}</span>
      }
    </button>
  `,
})
export class LikeButton {
  private readonly likes = inject(LikesService);

  readonly propertyId = input.required<string>();
  /** Título de la publicación, solo para el texto accesible. */
  readonly title = input('');
  readonly showLabel = input(false);

  protected readonly liked = computed(() => this.likes.isLiked(this.propertyId()));
  protected readonly label = computed(() => {
    const subject = this.title() ? `: ${this.title()}` : '';
    return this.liked() ? `Quitar me gusta${subject}` : `Me gusta${subject}`;
  });

  protected toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.likes.toggle(this.propertyId());
  }
}
