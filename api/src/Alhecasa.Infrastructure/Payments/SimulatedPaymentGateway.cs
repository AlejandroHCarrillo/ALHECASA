using Alhecasa.Application.Abstractions;

namespace Alhecasa.Infrastructure.Payments;

/// <summary>
/// Pasarela de pagos simulada: no mueve dinero. Sirve para construir y probar la compra de planes mientras no se
/// integra la pasarela real; esa integración solo reemplaza esta clase en <c>AddInfrastructure</c>.
/// El resultado depende del número de tarjeta, como las tarjetas de prueba de Stripe:
/// <list type="bullet">
/// <item><c>4000 0000 0000 0002</c>: tarjeta rechazada.</item>
/// <item><c>4000 0000 0000 9995</c>: fondos insuficientes.</item>
/// <item><c>4000 0000 0000 0069</c>: tarjeta vencida.</item>
/// <item>cualquier otra tarjeta con dígito de control válido, por ejemplo <c>4242 4242 4242 4242</c>: aprobada.</item>
/// </list>
/// La tarjeta se lee en memoria y no se guarda ni se escribe en registros.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public const string Declined = "4000000000000002";
    public const string InsufficientFunds = "4000000000009995";
    public const string Expired = "4000000000000069";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var number = new string(request.Card.Number.Where(char.IsAsciiDigit).ToArray());

        PaymentResult result = number switch
        {
            Declined => PaymentResult.Decline("El banco rechazó la tarjeta. Intenta con otra."),
            InsufficientFunds => PaymentResult.Decline("La tarjeta no tiene fondos suficientes."),
            Expired => PaymentResult.Decline("La tarjeta está vencida. Usa otra tarjeta."),
            _ => PaymentResult.Approve($"SIM-{Guid.NewGuid():N}"[..16].ToUpperInvariant())
        };

        return Task.FromResult(result);
    }
}
