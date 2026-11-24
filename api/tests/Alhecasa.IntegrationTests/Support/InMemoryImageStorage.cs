using System.Collections.Concurrent;
using Alhecasa.Application.Abstractions;

namespace Alhecasa.IntegrationTests.Support;

/// <summary>Almacenamiento en memoria: las pruebas no escriben en disco.</summary>
public sealed class InMemoryImageStorage : IImageStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public int Count => _files.Count;

    public bool Contains(string key) => _files.ContainsKey(key);

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _files[key] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
