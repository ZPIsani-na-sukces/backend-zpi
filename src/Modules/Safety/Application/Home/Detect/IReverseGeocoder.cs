namespace Zpi.Modules.Safety.Application.Home.Detect;

public interface IReverseGeocoder
{
    Task<SafetyResult<DetectedAddressDto>> DetectAsync(DetectAddressRequest request, CancellationToken cancellationToken);
}
