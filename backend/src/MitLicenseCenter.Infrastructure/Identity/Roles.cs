namespace MitLicenseCenter.Infrastructure.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Viewer];
}
