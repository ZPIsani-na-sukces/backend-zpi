using Zpi.Modules.Care.Contracts;

namespace Zpi.Modules.Safety.Application.Home.Detect;

public sealed class DetectAddressHandler(ICareFacade care, IReverseGeocoder geocoder)
{
    public async Task<SafetyResult<DetectedAddressDto>> Handle(Guid accountId, Guid seniorId,
        DetectAddressRequest request, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(accountId, seniorId, cancellationToken))
            return SafetyResult<DetectedAddressDto>.Failed(SafetyFailure.ResourceNotFound);
        return await geocoder.DetectAsync(request, cancellationToken);
    }
}
