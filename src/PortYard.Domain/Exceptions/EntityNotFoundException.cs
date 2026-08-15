namespace PortYard.Domain.Exceptions;

/// <summary>
/// Thrown when a lookup by natural or surrogate key (container number, slot code, hold id)
/// finds nothing. The API layer maps this to HTTP 404 Not Found.
/// </summary>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}
