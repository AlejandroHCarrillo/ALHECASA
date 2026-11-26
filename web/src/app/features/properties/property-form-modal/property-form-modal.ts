import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Observable, defer, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { UserRole } from '../../../core/models/user-role';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { resolveApiError } from '../../../shared/utils/api-error';
import { formatCoordinates, parseCoordinates } from '../../../shared/utils/coordinates';
import { imageUrl } from '../../../shared/utils/image-url';
import { Agency } from '../../users/user.models';
import { PropertiesService } from '../properties.service';
import {
  AMENITY_LABELS,
  Amenity,
  CurrencyCode,
  IMAGE_LIMITS,
  OPERATION_LABELS,
  OperationType,
  PROPERTY_LIMITS,
  PROPERTY_STATUS_LABELS,
  PROPERTY_TYPE_LABELS,
  Property,
  PropertyStatus,
  PropertyType,
  UpdatePropertyRequest,
} from '../property.models';

type TextField = 'title' | 'coordinates' | 'parkingSpaces' | 'price' | 'city' | 'state' | 'agencyId' | 'bedrooms' | 'bathrooms' | 'constructionArea' | 'landArea';

/** Imagen elegida en el equipo que todavía no se sube. */
interface StagedImage {
  file: File;
  preview: string;
}

/** Una miniatura de la galería: ya guardada en la API o pendiente de subir. */
export type ImageTile =
  | { kind: 'saved'; id: string; src: string }
  | { kind: 'new'; index: number; src: string; name: string };

/** Modal compartido para crear (property = null) y editar una propiedad. */
@Component({
  selector: 'app-property-form-modal',
  imports: [ReactiveFormsModule, Modal, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './property-form-modal.html',
})
export class PropertyFormModal implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly propertiesService = inject(PropertiesService);
  private readonly auth = inject(AuthService);

  readonly property = input<Property | null>(null);
  readonly agencies = input<Agency[]>([]);
  readonly saved = output<string>();
  readonly cancelled = output<void>();

  protected readonly limits = PROPERTY_LIMITS;
  protected readonly types = Object.values(PropertyType);
  protected readonly operations = Object.values(OperationType);
  protected readonly statuses = Object.values(PropertyStatus);
  protected readonly currencies = Object.values(CurrencyCode);
  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly statusLabels = PROPERTY_STATUS_LABELS;
  protected readonly amenities = Object.values(Amenity);
  protected readonly amenityLabels = AMENITY_LABELS;

  protected readonly isEdit = computed(() => this.property() !== null);

  /** Solo el administrador de plataforma elige la inmobiliaria, y solo al crear (no se puede cambiar después). */
  protected readonly showAgency = computed(() => this.auth.hasRole(UserRole.PlatformAdmin) && !this.isEdit());

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  // Galería: las imágenes ya guardadas, las que se quitarán y las nuevas elegidas en el equipo.
  protected readonly imageLimits = IMAGE_LIMITS;
  protected readonly acceptTypes = IMAGE_LIMITS.acceptedTypes.join(',');
  private readonly propertyId = signal<string | null>(null);
  private readonly savedImageIds = signal<string[]>([]);
  private readonly removedIds = signal<string[]>([]);
  private readonly staged = signal<StagedImage[]>([]);
  protected readonly imageProblems = signal<string[]>([]);
  protected readonly imagesAttempted = signal(false);

  protected readonly tiles = computed<ImageTile[]>(() => [
    ...this.savedImageIds().map((id): ImageTile => ({ kind: 'saved', id, src: imageUrl(id) })),
    ...this.staged().map((image, index): ImageTile => ({ kind: 'new', index, src: image.preview, name: image.file.name })),
  ]);
  protected readonly totalImages = computed(() => this.savedImageIds().length + this.staged().length);
  protected readonly imagesMissing = computed(() => this.imagesAttempted() && this.totalImages() < IMAGE_LIMITS.min);

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(PROPERTY_LIMITS.title)]],
    description: ['', [Validators.maxLength(PROPERTY_LIMITS.description)]],
    type: [PropertyType.House as PropertyType, [Validators.required]],
    operation: [OperationType.Sale as OperationType, [Validators.required]],
    status: [PropertyStatus.Draft as PropertyStatus, [Validators.required]],
    price: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
      Validators.max(PROPERTY_LIMITS.maxPrice),
    ]),
    currency: [CurrencyCode.MXN as CurrencyCode, [Validators.required]],
    street: ['', [Validators.maxLength(PROPERTY_LIMITS.street)]],
    neighborhood: ['', [Validators.maxLength(PROPERTY_LIMITS.neighborhood)]],
    city: ['', [Validators.required, Validators.maxLength(PROPERTY_LIMITS.city)]],
    state: ['', [Validators.required, Validators.maxLength(PROPERTY_LIMITS.state)]],
    postalCode: ['', [Validators.maxLength(PROPERTY_LIMITS.postalCode)]],
    bedrooms: new FormControl<number | null>(null, [Validators.min(0), Validators.max(PROPERTY_LIMITS.maxRooms)]),
    bathrooms: new FormControl<number | null>(null, [Validators.min(0), Validators.max(PROPERTY_LIMITS.maxRooms)]),
    constructionArea: new FormControl<number | null>(null, [Validators.min(0), Validators.max(PROPERTY_LIMITS.maxArea)]),
    landArea: new FormControl<number | null>(null, [Validators.min(0), Validators.max(PROPERTY_LIMITS.maxArea)]),
    coordinates: ['', [coordinatesValidator]],
    parkingSpaces: new FormControl<number | null>(null, [Validators.min(0), Validators.max(PROPERTY_LIMITS.maxParking)]),
    petsAllowed: [false],
    furnished: [false],
    amenities: [[] as Amenity[]],
    agencyId: [''],
  });

  ngOnInit(): void {
    const property = this.property();

    if (property) {
      this.propertyId.set(property.id);
      this.savedImageIds.set([...property.imageIds]);
      this.form.patchValue({
        title: property.title,
        description: property.description ?? '',
        type: property.type,
        operation: property.operation,
        status: property.status,
        price: property.price,
        currency: property.currency,
        street: property.street ?? '',
        neighborhood: property.neighborhood ?? '',
        city: property.city,
        state: property.state,
        postalCode: property.postalCode ?? '',
        bedrooms: property.bedrooms,
        bathrooms: property.bathrooms,
        constructionArea: property.constructionArea,
        landArea: property.landArea,
        coordinates: formatCoordinates(property.latitude, property.longitude),
        parkingSpaces: property.parkingSpaces,
        petsAllowed: property.petsAllowed,
        furnished: property.furnished,
        amenities: [...property.amenities],
      });
    }

    if (this.showAgency()) {
      this.form.controls.agencyId.addValidators(Validators.required);
      this.form.controls.agencyId.updateValueAndValidity();
    }
  }

  protected hasAmenity(amenity: Amenity): boolean {
    return this.form.controls.amenities.value.includes(amenity);
  }

  protected toggleAmenity(amenity: Amenity): void {
    const current = this.form.controls.amenities.value;
    this.form.controls.amenities.setValue(current.includes(amenity) ? current.filter((a) => a !== amenity) : [...current, amenity]);
  }

  protected hasError(control: TextField): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected submit(): void {
    this.imagesAttempted.set(true);
    if (this.form.invalid || this.totalImages() < IMAGE_LIMITS.min) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const coordinates = parseCoordinates(value.coordinates);
    const fields: UpdatePropertyRequest = {
      title: value.title.trim(),
      description: blankToNull(value.description),
      type: value.type,
      operation: value.operation,
      status: value.status,
      price: value.price ?? 0,
      currency: value.currency,
      street: blankToNull(value.street),
      neighborhood: blankToNull(value.neighborhood),
      city: value.city.trim(),
      state: value.state.trim(),
      postalCode: blankToNull(value.postalCode),
      bedrooms: value.bedrooms,
      bathrooms: value.bathrooms,
      constructionArea: value.constructionArea,
      landArea: value.landArea,
      latitude: coordinates === null || coordinates === 'invalid' ? null : coordinates.latitude,
      longitude: coordinates === null || coordinates === 'invalid' ? null : coordinates.longitude,
      parkingSpaces: value.parkingSpaces,
      petsAllowed: value.petsAllowed,
      furnished: value.furnished,
      amenities: value.amenities,
    };

    const wasEdit = this.property() !== null;

    this.submitting.set(true);
    this.error.set(null);

    this.persist(fields, this.showAgency() ? value.agencyId : null).subscribe({
      next: () => {
        this.submitting.set(false);
        this.saved.emit(wasEdit ? 'Propiedad actualizada correctamente.' : 'Propiedad creada correctamente.');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(resolveApiError(err));
      },
    });
  }

  /**
   * Guarda los datos y la galería. Son dos llamadas a la API, así que el orden importa:
   * - Una propiedad nueva se crea siempre como borrador (aún no tiene imágenes), se suben las imágenes
   *   y, si se eligió otro estado, se actualiza al final.
   * - Al editar, si el estado final es Publicada las imágenes van primero (la API exige al menos una);
   *   en los demás casos van después de los datos.
   * Si un paso falla, lo ya guardado se conserva y al reintentar se continúa desde ahí
   * (la propiedad creada queda en `propertyId`; las imágenes ya subidas dejan de estar pendientes).
   */
  private persist(fields: UpdatePropertyRequest, agencyId: string | null): Observable<unknown> {
    const id = this.propertyId();

    if (id === null) {
      return this.propertiesService.create({ ...fields, status: PropertyStatus.Draft, agencyId }).pipe(
        tap((created) => this.propertyId.set(created.id)),
        switchMap((created) => this.applyImages(created.id)),
        switchMap(() => (fields.status === PropertyStatus.Draft ? of(null) : this.propertiesService.update(this.propertyId()!, fields))),
      );
    }

    const update$ = defer(() => this.propertiesService.update(id, fields));
    const images$ = defer(() => this.applyImages(id));
    return fields.status === PropertyStatus.Published
      ? images$.pipe(switchMap(() => update$))
      : update$.pipe(switchMap(() => images$));
  }

  private applyImages(id: string): Observable<unknown> {
    const files = this.staged().map((image) => image.file);
    const removeIds = this.removedIds();
    if (files.length === 0 && removeIds.length === 0) {
      return of(null);
    }

    return this.propertiesService.uploadImages(id, files, removeIds).pipe(
      tap((updated) => {
        this.savedImageIds.set(updated.imageIds);
        this.removedIds.set([]);
        this.clearStaged();
      }),
    );
  }

  // --- Imágenes ---------------------------------------------------------------------------

  protected onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = ''; // permite elegir de nuevo el mismo archivo

    const problems: string[] = [];
    const accepted: StagedImage[] = [];
    let room = IMAGE_LIMITS.max - this.totalImages();

    for (const file of files) {
      if (!(IMAGE_LIMITS.acceptedTypes as readonly string[]).includes(file.type)) {
        problems.push(`"${file.name}" no es una imagen JPG, PNG o WebP.`);
      } else if (file.size > IMAGE_LIMITS.maxBytes) {
        problems.push(`"${file.name}" pesa más de ${IMAGE_LIMITS.maxBytes / (1024 * 1024)} MB.`);
      } else if (room <= 0) {
        problems.push(`"${file.name}" no se agregó: el máximo es de ${IMAGE_LIMITS.max} imágenes.`);
      } else {
        accepted.push({ file, preview: previewUrl(file) });
        room--;
      }
    }

    this.imageProblems.set(problems);
    if (accepted.length > 0) {
      this.staged.update((current) => [...current, ...accepted]);
    }
  }

  protected removeTile(tile: ImageTile): void {
    this.imageProblems.set([]);
    if (tile.kind === 'saved') {
      this.savedImageIds.update((ids) => ids.filter((id) => id !== tile.id));
      this.removedIds.update((ids) => [...ids, tile.id]);
      return;
    }

    const image = this.staged()[tile.index];
    if (image) {
      revokePreview(image.preview);
      this.staged.update((current) => current.filter((_, index) => index !== tile.index));
    }
  }

  private clearStaged(): void {
    for (const image of this.staged()) {
      revokePreview(image.preview);
    }
    this.staged.set([]);
  }

  ngOnDestroy(): void {
    this.clearStaged();
  }
}

function previewUrl(file: File): string {
  return typeof URL.createObjectURL === 'function' ? URL.createObjectURL(file) : '';
}

function revokePreview(url: string): void {
  if (url && typeof URL.revokeObjectURL === 'function') {
    URL.revokeObjectURL(url);
  }
}

/** Las coordenadas son opcionales, pero si se escriben deben ser latitud y longitud válidas. */
function coordinatesValidator(control: AbstractControl<string>): ValidationErrors | null {
  return parseCoordinates(control.value) === 'invalid' ? { coordinates: true } : null;
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
