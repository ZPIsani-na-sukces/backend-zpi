using Zpi.Domain;

namespace Zpi.Modules.SeniorProfile.Domain;

public sealed class ContactSettings : VersionedEntity
{
    public Guid SeniorId { get; set; }
    public bool SeniorEditingLocked { get; set; }
}
