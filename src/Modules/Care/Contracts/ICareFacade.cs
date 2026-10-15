namespace Zpi.Modules.Care.Contracts;

public interface ICareFacade
{
    Task<IReadOnlyCollection<Guid>> GetActiveCaretakerIdsAsync(Guid seniorId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetAccessibleSeniorIdsAsync(Guid accountId, IReadOnlyCollection<Guid> seniorIds, CancellationToken cancellationToken);
    Task<bool> CanAccessSeniorAsync(Guid accountId, Guid seniorId, CancellationToken cancellationToken);
    Task<bool> HasActiveRelationshipAsync(Guid caretakerId, Guid seniorId, CancellationToken cancellationToken);
}
