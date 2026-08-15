namespace PortYard.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would violate a yard business invariant (an illegal status
/// transition, a slot capacity breach, gating out under an active hold, and so on).
/// The API layer maps this to HTTP 409 Conflict.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message)
    {
    }
}
