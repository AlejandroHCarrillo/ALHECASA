namespace Alhecasa.Application.Common;

/// <summary>El recurso no existe o no es visible para el usuario actual.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>El estado actual del recurso entra en conflicto con la operación (p. ej. correo duplicado).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>El usuario autenticado no tiene permiso para la operación.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>La operación excede el tope del plan de la cuenta. El mensaje se muestra al usuario (en español).</summary>
public sealed class PlanLimitException(string message) : Exception(message);

/// <summary>La pasarela rechazó el cobro. El mensaje se muestra al usuario (en español).</summary>
public sealed class PaymentDeclinedException(string message) : Exception(message);

/// <summary>Credenciales inválidas o cuenta no disponible.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
