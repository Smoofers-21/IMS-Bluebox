using System.ComponentModel.DataAnnotations;

namespace BlueBox.Web.Models;

public sealed class ItemInput
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, RegularExpression("^(MOVIE|GAME)$")] public string Type { get; set; } = "";
    [Required, StringLength(30)] public string Format { get; set; } = "";
    [StringLength(2048), HttpsUrl] public string? PosterUrl { get; set; }
    [StringLength(20)] public string? AgeRating { get; set; }
    [Required, RegularExpression("^(ACTIVE|ARCHIVED)$")] public string Status { get; set; } = "";
}

public sealed class LocationInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(255)] public string Address { get; set; } = "";
    [Required, RegularExpression("^(ACTIVE|INACTIVE)$")] public string Status { get; set; } = "";
}

public sealed class EmployeeInput
{
    [Required, StringLength(50)] public string FirstName { get; set; } = "";
    [Required, StringLength(50)] public string LastName { get; set; } = "";
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [StringLength(PasswordPolicy.MaximumLength, MinimumLength = PasswordPolicy.MinimumSetupLength)]
    public string? Password { get; set; }
    [Required, RegularExpression("^(STAFF|MANAGER)$")] public string Role { get; set; } = "";
    [Required, RegularExpression("^(ACTIVE|INACTIVE|TERMINATED)$")] public string Status { get; set; } = "";
}

public sealed class HttpsUrlAttribute : ValidationAttribute
{
    public HttpsUrlAttribute() => ErrorMessage = "Enter an HTTPS poster URL, or leave it empty.";
    public override bool IsValid(object? value) => value is null || value is string text &&
        (string.IsNullOrWhiteSpace(text) || Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo));
}

public static class InputValidation
{
    public static bool IsValid(object input) => Validator.TryValidateObject(input, new ValidationContext(input), [], true);
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
