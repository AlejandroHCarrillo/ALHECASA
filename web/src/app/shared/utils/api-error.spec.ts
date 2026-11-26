import { HttpErrorResponse } from '@angular/common/http';
import { resolveApiError } from './api-error';

describe('resolveApiError', () => {
  it('maps 409 to the duplicated email message', () => {
    expect(resolveApiError(new HttpErrorResponse({ status: 409 }))).toBe('El correo ya está registrado.');
  });

  it('includes server validation details on 400', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Email: ['Email is invalid.'] } },
    });
    expect(resolveApiError(error)).toContain('Email is invalid.');
  });

  it('reports connectivity problems', () => {
    expect(resolveApiError(new HttpErrorResponse({ status: 0 }))).toContain('No se pudo conectar');
  });

  it('handles unknown errors', () => {
    expect(resolveApiError(new Error('boom'))).toContain('error inesperado');
  });
  it('shows the plan limit message the API sends on 422', () => {
    const error = new HttpErrorResponse({ status: 422, error: { detail: 'Tu cuenta gratuita permite hasta 3 propiedades.' } });
    expect(resolveApiError(error)).toBe('Tu cuenta gratuita permite hasta 3 propiedades.');
  });

  it('has a generic message for a 422 without detail', () => {
    expect(resolveApiError(new HttpErrorResponse({ status: 422 }))).toContain('límite');
  });

  it('shows the reason the payment gateway sends on 402', () => {
    const error = new HttpErrorResponse({ status: 402, error: { detail: 'La tarjeta no tiene fondos suficientes.' } });
    expect(resolveApiError(error)).toBe('La tarjeta no tiene fondos suficientes.');
  });

  it('has a generic message for a 402 without detail', () => {
    expect(resolveApiError(new HttpErrorResponse({ status: 402 }))).toContain('pago');
  });
});
