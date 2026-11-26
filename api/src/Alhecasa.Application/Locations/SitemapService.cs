using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Locations;

/// <summary>Configuración del posicionamiento en buscadores.</summary>
/// <param name="SiteUrl">Dirección pública del sitio web (la del frontend, no la de la API), por ejemplo <c>https://alhecasa.com</c>. Las direcciones del mapa del sitio se arman con ella.</param>
public sealed record SeoSettings(string SiteUrl = SeoSettings.DefaultSiteUrl)
{
    public const string DefaultSiteUrl = "http://localhost:4200";

    /// <summary>La dirección sin diagonal final. <see cref="InvalidOperationException"/> si no es una dirección http o https absoluta.</summary>
    public string NormalizedSiteUrl()
    {
        if (!Uri.TryCreate(SiteUrl?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Seo:SiteUrl must be an absolute http or https address.");
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}

/// <summary>Una propiedad publicada, con lo mínimo que necesita el mapa del sitio.</summary>
public sealed record SitemapProperty(Guid Id, DateTimeOffset LastModified, Guid AgencyId, bool AgencyIsIndividual);

public interface ISitemapService
{
    /// <summary>El mapa del sitio (sitemaps.org) con las páginas públicas indexables: portada, zonas con propiedades, inmobiliarias y publicaciones.</summary>
    Task<string> BuildAsync(CancellationToken cancellationToken = default);
}

public sealed class SitemapService(IPropertyRepository properties, SeoSettings seo) : ISitemapService
{
    /// <summary>Límite de direcciones de un solo mapa del sitio según el protocolo.</summary>
    public const int MaxUrls = 50_000;

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        var site = seo.NormalizedSiteUrl();
        var urls = new List<(string Path, DateTimeOffset? LastModified)>
        {
            ("/", null),
            ("/calculadora-hipotecaria", null)
        };

        var zones = await properties.GetPublishedZoneCountsAsync(cancellationToken);
        foreach (var operation in new[] { OperationType.Sale, OperationType.Rent })
        {
            var prefix = operation == OperationType.Sale ? "/venta" : "/renta";
            var ofOperation = zones.Where(z => z.Operation == operation && z.Count > 0).ToList();

            foreach (var city in ofOperation.Select(z => z.CitySlug).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                urls.Add(($"{prefix}/{city}", null));
            }

            foreach (var zone in ofOperation
                .Where(z => z.NeighborhoodSlug is not null)
                .Select(z => (z.CitySlug, Neighborhood: z.NeighborhoodSlug!))
                .Distinct()
                .OrderBy(z => z.CitySlug, StringComparer.Ordinal)
                .ThenBy(z => z.Neighborhood, StringComparer.Ordinal))
            {
                urls.Add(($"{prefix}/{zone.CitySlug}/{zone.Neighborhood}", null));
            }
        }

        var published = await properties.GetPublishedForSitemapAsync(MaxUrls, cancellationToken);

        foreach (var agency in published
            .Where(p => !p.AgencyIsIndividual)
            .GroupBy(p => p.AgencyId)
            .Select(g => (Id: g.Key, LastModified: g.Max(p => p.LastModified)))
            .OrderBy(a => a.Id))
        {
            urls.Add(($"/inmobiliarias/{agency.Id}", agency.LastModified));
        }

        foreach (var property in published.OrderBy(p => p.Id))
        {
            urls.Add(($"/publicaciones/{property.Id}", property.LastModified));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                Ns + "urlset",
                urls.Take(MaxUrls).Select(u => Entry(site + u.Path, u.LastModified))));

        // Se escribe en UTF-8 (no en UTF-16, que es lo que declararía un StringBuilder): así la declaración del documento es verdadera.
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
        {
            document.Save(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static XElement Entry(string location, DateTimeOffset? lastModified) =>
        lastModified is null
            ? new XElement(Ns + "url", new XElement(Ns + "loc", location))
            : new XElement(
                Ns + "url",
                new XElement(Ns + "loc", location),
                new XElement(Ns + "lastmod", lastModified.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
}
