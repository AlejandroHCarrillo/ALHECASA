using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class InquiryRepository(AlhecasaDbContext db) : IInquiryRepository
{
    // Los filtros globales aíslan por inmobiliaria: el personal solo alcanza las solicitudes de la suya.
    public Task<Inquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Inquiries
            .Include(i => i.FollowUps)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<PagedResult<Inquiry>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Inquiry> source = db.Inquiries.AsNoTracking().Include(i => i.FollowUps);

        if (query.Status is not null)
        {
            source = source.Where(i => i.Status == query.Status);
        }

        if (query.Search is not null)
        {
            var pattern = $"%{EscapeLike(query.Search)}%";
            source = source.Where(i =>
                EF.Functions.Like(i.Name, pattern, "\\")
                || EF.Functions.Like(i.Email, pattern, "\\")
                || EF.Functions.Like(i.Phone, pattern, "\\")
                || EF.Functions.Like(i.PropertyTitle, pattern, "\\"));
        }

        var total = await source.CountAsync(cancellationToken);

        // Lo más reciente primero; el Id (Guid v7, ordenable por tiempo) desempata.
        var items = await source
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Inquiry>(items, query.Page, query.PageSize, total);
    }

    public async Task<InquiryStats> GetStatsAsync(Guid agencyId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var byStatus = await db.Inquiries.AsNoTracking()
            .Where(i => i.AgencyId == agencyId)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var recent = await db.Inquiries.AsNoTracking()
            .CountAsync(i => i.AgencyId == agencyId && i.CreatedAt >= since, cancellationToken);

        int Of(InquiryStatus status) => byStatus.Where(r => r.Status == status).Sum(r => r.Count);

        return new InquiryStats(Of(InquiryStatus.New), Of(InquiryStatus.InProgress), Of(InquiryStatus.Closed), recent);
    }

    public async Task<IReadOnlyList<Inquiry>> ListLatestNewAsync(Guid agencyId, int count, CancellationToken cancellationToken = default) =>
        await db.Inquiries.AsNoTracking()
            .Where(i => i.AgencyId == agencyId && i.Status == InquiryStatus.New)
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default) =>
        await db.Inquiries.AddAsync(inquiry, cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}