using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record LoginRequest(
    [property: Required, StringLength(256, MinimumLength = 1)] string UserName,
    [property: Required, StringLength(256, MinimumLength = 1)] string Password);

public sealed record CurrentUserResponse(string UserName, IReadOnlyList<string> Roles);
