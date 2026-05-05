using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IDocumentIdentityValidator
{
    IdentityValidationResult Validate(DocumentIdentity identity, string? patientName, string? patientCnp);
}
