using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record LoginRequest(
    [property: Required, StringLength(256, MinimumLength = 1)] string UserName,
    [property: Required, StringLength(256, MinimumLength = 1)] string Password);

public sealed record CurrentUserResponse(string UserName, IReadOnlyList<string> Roles);

public sealed record ChangePasswordRequest(
    [property: Required, StringLength(256, MinimumLength = 1)] string CurrentPassword,
    [property: Required, StringLength(256, MinimumLength = 12)] string NewPassword);
