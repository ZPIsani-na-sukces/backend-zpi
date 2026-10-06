namespace Zpi.Modules.Identity.Domain;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public AccountRole Code { get; set; }
    public required string Name { get; set; }
}
