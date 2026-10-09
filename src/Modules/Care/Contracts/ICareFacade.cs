namespace Zpi.Modules.Care.Contracts;

public interface ICareFacade
{
    Task<bool> CanAccessSeniorAsync(Guid accountId, Guid seniorId, CancellationToken cancellationToken);
    Task<bool> HasActiveRelationshipAsync(Guid caretakerId, Guid seniorId, CancellationToken cancellationToken);
}
