namespace Alhecasa.Application.Dashboard;

/// <summary>Cuántas propiedades tiene la cuenta en cada estado. No cuenta las eliminadas.</summary>
/// <param name="Total">Todas, borradores incluidos.</param>
/// <param name="Published">Activas: visibles para los interesados.</param>
/// <param name="Draft">Capturadas, todavía no visibles.</param>
/// <param name="Paused">Retiradas temporalmente.</param>
/// <param name="Closed">Operación concluida (vendidas o rentadas).</param>
/// <param name="Featured">Destacadas en este momento (las que cuentan contra el paquete de destacados).</param>
public sealed record PropertyStats(int Total, int Published, int Draft, int Paused, int Closed, int Featured);

/// <summary>Solicitudes de informes de la cuenta por etapa de seguimiento.</summary>
/// <param name="New">Nuevas: nadie las ha atendido.</param>
/// <param name="InProgress">En seguimiento.</param>
/// <param name="Closed">Cerradas.</param>
/// <param name="ReceivedRecently">Las que llegaron en los últimos días (<see cref="DashboardService.RecentDays"/>), sin importar su etapa.</param>
public sealed record InquiryStats(int New, int InProgress, int Closed, int ReceivedRecently)
{
    public int Total => New + InProgress + Closed;
}

/// <summary>Una solicitud nueva, resumida para la lista del tablero.</summary>
public sealed record RecentInquiryDto(Guid Id, Guid PropertyId, string PropertyTitle, string Name, DateTimeOffset CreatedAt);

/// <summary>Resumen de la inmobiliaria (o del espacio del particular) para su página de inicio.</summary>
/// <param name="RecentDays">A cuántos días se refiere <see cref="InquiryStats.ReceivedRecently"/>.</param>
/// <param name="LatestNewInquiries">Las solicitudes nuevas más recientes, la más reciente primero.</param>
public sealed record AgencyDashboardDto(
    PropertyStats Properties,
    InquiryStats Inquiries,
    int RecentDays,
    IReadOnlyList<RecentInquiryDto> LatestNewInquiries);