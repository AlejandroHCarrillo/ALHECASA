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
});
