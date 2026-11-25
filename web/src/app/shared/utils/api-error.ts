import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Convierte un error de la API (ProblemDetails) en un mensaje en español para el usuario.
 * El mensaje depende del código HTTP; en 400 se agregan los detalles de validación del servidor.
 */
export function resolveApiError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Ocurrió un error inesperado. Intenta de nuevo.';
  }

  switch (error.status) {
    case 0:
      return 'No se pudo conectar con el servidor. Verifica tu conexión.';
    case 400: {
      const problem = error.error as ProblemDetails | null;
      const details = problem?.errors ? Object.values(problem.errors).flat() : [];
      return details.length > 0
        ? `Revisa los datos capturados: ${details.join(' ')}`
        : 'Los datos enviados no son válidos.';
    }
    case 401:
      return 'Tu sesión expiró. Ingresa de nuevo.';
    case 403:
      return 'No tienes permiso para realizar esta acción.';
    case 404:
      return 'El registro no existe o ya no está disponible.';
    case 409:
      return 'El correo ya está registrado.';
    case 422:
      // Regla de plan (p. ej. tope de propiedades de la cuenta gratuita): la API manda un mensaje pensado para mostrarse.
      return (error.error as ProblemDetails | null)?.detail ?? 'La operación excede el límite de tu cuenta.';
    case 429:
      return 'Demasiados intentos. Espera un minuto e inténtalo de nuevo.';
    default:
      return 'El servidor no pudo completar la operación. Intenta más tarde.';
  }
}
