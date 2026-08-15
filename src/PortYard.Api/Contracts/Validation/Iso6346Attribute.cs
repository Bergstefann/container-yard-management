using System.ComponentModel.DataAnnotations;
using PortYard.Domain.Validation;

namespace PortYard.Api.Contracts.Validation;

/// <summary>
/// Rejects malformed or numerically invalid ISO 6346 container numbers at the model-binding
/// boundary, so a bad number never reaches the domain layer. Kept separate from the domain's
/// own <see cref="Iso6346"/> check (which still runs in <c>Container.Register</c>) so that
/// invalid input surfaces as an HTTP 400 here, not a 409 domain-rule conflict.
/// </summary>
public sealed class Iso6346Attribute : ValidationAttribute
{
    public Iso6346Attribute() : base("{0} is not a valid ISO 6346 container number.")
    {
    }

    public override bool IsValid(object? value) => value is string s && Iso6346.IsValid(s);
}
