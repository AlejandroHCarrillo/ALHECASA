namespace Alhecasa.Domain.Common;

/// <summary>
/// Entidad base con identificador, auditoría y borrado lógico.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void MarkAsDeleted(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
    }
}
