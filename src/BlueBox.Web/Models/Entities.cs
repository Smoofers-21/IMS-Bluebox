namespace BlueBox.Web.Models;

public sealed class Employee
{
    public int EmployeeId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string AccessRole { get; set; } = "STAFF";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public string DisplayName => $"{FirstName} {LastName}";
}

public sealed class Item
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "MOVIE";
    public string MediaFormat { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string? AgeRating { get; set; }
    public string ItemStatus { get; set; } = "ACTIVE";
}

public sealed class Location
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string Address { get; set; } = "";
    public string LocationStatus { get; set; } = "ACTIVE";
}
