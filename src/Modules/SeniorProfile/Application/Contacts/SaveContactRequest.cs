using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Application.Contacts;

public sealed record SaveContactRequest(string DisplayName, string? FirstName, string? LastName,
    string? RelationshipLabel, string PhoneNumber, bool IsActive)
{
    public void ApplyTo(SeniorContactPerson contact)
    {
        contact.DisplayName = DisplayName.Trim();
        contact.FirstName = string.IsNullOrWhiteSpace(FirstName) ? null : FirstName.Trim();
        contact.LastName = string.IsNullOrWhiteSpace(LastName) ? null : LastName.Trim();
        contact.RelationshipLabel = string.IsNullOrWhiteSpace(RelationshipLabel) ? null : RelationshipLabel.Trim();
        contact.PhoneNumber = PhoneNumber.Trim();
        contact.IsActive = IsActive;
    }
}
