using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Inquiries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

/// <summary>Bandeja de solicitudes de informes de la inmobiliaria: ver al prospecto, responderle y darle seguimiento.</summary>
[ApiController]
[Route("api/inquiries")]
[Authorize(Roles = RoleNames.AgencyStaff)]
public sealed class InquiriesController(IInquiryService inquiryService) : ControllerBase
{
    /// <summary>Lista las solicitudes (más recientes primero), con filtro por etapa, búsqueda y paginación.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<InquiryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InquiryDto>>> GetPaged([FromQuery] InquiryListQuery query, CancellationToken cancellationToken) =>
        Ok(await inquiryService.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InquiryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InquiryDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await inquiryService.GetByIdAsync(id, cancellationToken));

    /// <summary>Registra una respuesta al prospecto o una nota interna. Una respuesta pasa la solicitud a «En seguimiento».</summary>
    [HttpPost("{id:guid}/follow-ups")]
    [ProducesResponseType<InquiryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InquiryDto>> AddFollowUp(Guid id, AddFollowUpRequest request, CancellationToken cancellationToken) =>
        Ok(await inquiryService.AddFollowUpAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<InquiryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InquiryDto>> ChangeStatus(Guid id, ChangeInquiryStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await inquiryService.ChangeStatusAsync(id, request, cancellationToken));
}