namespace Zpi.Domain;

public abstract class VersionedEntity
{
    public int Version { get; set; } = 1;
}
