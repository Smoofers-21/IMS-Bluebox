namespace BlueBox.Web.Models;

public static class PasswordPolicy
{
    public const int MinimumSetupLength = 12;
    public const int MaximumLength = 256;

    public static bool IsValidForSetup(string? password) =>
        password is { Length: >= MinimumSetupLength and <= MaximumLength };
}
