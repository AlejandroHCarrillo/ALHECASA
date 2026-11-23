import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-paginator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-between gap-2 sm:flex-row">
      <span class="text-sm text-base-content/70">{{ summary() }}</span>

      <div class="join" role="navigation" aria-label="Paginación">
        <button
          type="button"
          class="btn btn-sm join-item"
          [disabled]="page() <= 1"
          (click)="pageChange.emit(page() - 1)"
          aria-label="Página anterior"
        >
          <i class="fa-solid fa-chevron-left" aria-hidden="true"></i>
        </button>
        <span class="btn btn-sm join-item pointer-events-none">Página {{ page() }} de {{ totalPages() || 1 }}</span>
        <button
          type="button"
          class="btn btn-sm join-item"
          [disabled]="page() >= totalPages()"
          (click)="pageChange.emit(page() + 1)"
          aria-label="Página siguiente"
        >
          <i class="fa-solid fa-chevron-right" aria-hidden="true"></i>
        </button>
      </div>
    </div>
  `,
})
export class Paginator {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  protected readonly summary = computed(() => {
    const total = this.totalCount();
    if (total === 0) {
      return 'Sin registros';
    }
    const from = (this.page() - 1) * this.pageSize() + 1;
    const to = Math.min(this.page() * this.pageSize(), total);
    return `Mostrando ${from}-${to} de ${total}`;
  });
}
