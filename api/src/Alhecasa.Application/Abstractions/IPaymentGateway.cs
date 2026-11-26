using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Abstractions;

/// <summary>
/// Cobro a una tarjeta. Es el puerto por el que entrará la pasarela real (Stripe, Mercado Pago…): hoy lo implementa
/// una simulación, y cambiar de proveedor no toca los servicios ni los endpoints.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Intenta cobrar. Un rechazo (tarjeta declinada, sin fondos…) no es una excepción: vuelve en el resultado.</summary>
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lo que se cobra y a qué tarjeta. La tarjeta es un dato sensible: no se guarda ni se escribe en registros
/// (<see cref="ToString"/> la oculta). Con una pasarela real lo normal es no recibirla nunca y trabajar con un token.
/// </summary>
public sealed record PaymentRequest(decimal Amount, CurrencyCode Currency, string Description, PaymentCard Card)
{
    public override string ToString() => $"PaymentRequest {{ Amount = {Amount}, Currency = {Currency} }}";
}

public sealed record PaymentCard(string Number, string HolderName, int ExpiryMonth, int ExpiryYear, string Cvv)
{
    public override string ToString() => "PaymentCard { **** }";
}

/// <param name="Approved">Si se cobró.</param>
/// <param name="TransactionId">Identificador de la operación en la pasarela. Solo si se aprobó.</param>
/// <param name="DeclineReason">Motivo del rechazo, en español y listo para mostrarse. Solo si se rechazó.</param>
public sealed record PaymentResult(bool Approved, string? TransactionId, string? DeclineReason)
{
    public static PaymentResult Approve(string transactionId) => new(true, transactionId, null);

    public static PaymentResult Decline(string reason) => new(false, null, reason);
}
