using System.Data;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Services;

public static class ManagerBootstrap
{
    public static async Task CreateAsync(BlueBoxDbContext db, IPasswordHasher<Employee> hasher, string username, string password)
    {
        username = username.Trim();
        if (username.Length is < 1 or > 50 || password.Length < 12)
            throw new InvalidOperationException("Choose a username of 1–50 characters and a password of at least 12 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.Employees.AnyAsync(e => e.AccessRole == "MANAGER" && e.EmploymentStatus == "ACTIVE"))
            throw new InvalidOperationException("An active manager already exists. Initial-manager setup cannot be used to add another.");
        if (await db.Employees.AnyAsync(e => e.Username == username))
            throw new InvalidOperationException("That username already exists. No account was changed.");
        var employee = new Employee { FirstName = "BlueBox", LastName = "Manager", Username = username, AccessRole = "MANAGER" };
        employee.PasswordHash = hasher.HashPassword(employee, password);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
