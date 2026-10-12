using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Application.Contacts;

public sealed record ContactDto(Guid ContactId, Guid SeniorId, string DisplayName, string? FirstName,
    string? LastName, string? RelationshipLabel, string PhoneNumber, bool IsActive, AccountSummary? CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version)
{
    public static ContactDto From(SeniorContactPerson contact, AccountSummary? creator) => new(contact.Id,
        contact.SeniorId, contact.DisplayName, contact.FirstName, contact.LastName, contact.RelationshipLabel,
        contact.PhoneNumber, contact.IsActive, creator, contact.CreatedAt, contact.UpdatedAt, contact.Version);
}
