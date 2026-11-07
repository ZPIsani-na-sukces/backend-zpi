using Microsoft.AspNetCore.Mvc;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Api;

internal static class AuthenticationFailureResponses
{
    public static IActionResult Failure<T>(this ControllerBase controller, AuthenticationResult<T> result)
    {
        if (result.Failure == AuthenticationFailure.PhoneVerificationRequired)
        {
            var extensions = new Dictionary<string, object?>
            {
                ["code"] = "phoneVerificationRequired",
                ["verificationTicket"] = result.Verification!.VerificationTicket,
                ["ticketExpiresAt"] = result.Verification.TicketExpiresAt
            };
            if (result.Verification.PhoneChallenge is not null)
                extensions["phoneChallenge"] = result.Verification.PhoneChallenge;
            return controller.Problem(statusCode: 403, detail: "Verify your phone number before signing in.", extensions: extensions);
        }
        if (result.Failure == AuthenticationFailure.InvalidPhoneNumber)
            return controller.Problem(statusCode: 400, detail: "Invalid request data.", extensions: new Dictionary<string, object?>
            {
                ["code"] = "validationFailed",
                ["errors"] = new Dictionary<string, string[]> { ["phoneNumber"] = ["This phone number cannot receive SMS messages."] }
            });
        if (result.Failure == AuthenticationFailure.ResendCooldown)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(result.RetryAfter!.Value.TotalSeconds));
            controller.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return ProblemResponse(controller, 429, "rateLimitExceeded", "Wait before requesting another code.");
        }
        var (status, code, description) = result.Failure switch
        {
            AuthenticationFailure.DuplicateAccount => (409, "invalidState", "An account with these details already exists."),
            AuthenticationFailure.InvalidCredentials => (401, "invalidCredentials", "Invalid credentials."),
            AuthenticationFailure.InvalidCode => (400, "invalidCode", "Invalid or expired verification code or ticket."),
            AuthenticationFailure.InvalidState => (409, "invalidState", "This operation is not available in the current state."),
            AuthenticationFailure.DeliveryUnavailable => (503, "deliveryUnavailable", "Message delivery is unavailable."),
            _ => (403, "operationForbidden", "This operation is not allowed.")
        };
        return ProblemResponse(controller, status, code, description);
    }

    private static ObjectResult ProblemResponse(ControllerBase controller, int status, string code, string detail) =>
        controller.Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
