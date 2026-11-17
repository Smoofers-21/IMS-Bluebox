using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Services;

public static class DemoSeeder
{
    public static async Task SeedAsync(BlueBoxDbContext db, IPasswordHasher<Employee> hasher, string password)
    {
        if (password.Length < 12) throw new InvalidOperationException("Choose a demo password of at least 12 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Employees.AnyAsync() || await db.Items.AnyAsync() || await db.Locations.AnyAsync()
            || await db.Inventory.AnyAsync() || await db.Transactions.AnyAsync())
            throw new InvalidOperationException("Demo seeding requires empty BlueBox tables. No existing records were changed.");

        var manager = new Employee { FirstName = "Jordan", LastName = "Lee", Username = "jordan.lee", AccessRole = "MANAGER" };
        var staff = new Employee { FirstName = "Avery", LastName = "Chen", Username = "avery.chen" };
        var inactive = new Employee { FirstName = "Sam", LastName = "Rivera", Username = "sam.rivera", EmploymentStatus = "INACTIVE" };
        foreach (var employee in new[] { manager, staff, inactive }) employee.PasswordHash = hasher.HashPassword(employee, password);
        db.Employees.AddRange(manager, staff, inactive);
        var north = new Location { LocationName = "North Loop", Address = "100 Demo Street" };
        var west = new Location { LocationName = "West End", Address = "200 Example Avenue" };
        db.Locations.AddRange(north, west);
        var items = new[]
        {
            new Item { ItemName = "Moonlight Signal", MediaFormat = "Blu-ray", AgeRating = "PG-13" },
            new Item { ItemName = "Moonlight Signal", MediaFormat = "DVD", AgeRating = "PG-13" },
            new Item { ItemName = "The Last Orchard", MediaFormat = "DVD", AgeRating = "PG" },
            new Item { ItemName = "Neon Circuit", ItemType = "GAME", MediaFormat = "PS5 disc", AgeRating = "T" },
            new Item { ItemName = "Skyline Drift", ItemType = "GAME", MediaFormat = "Xbox Series X disc", AgeRating = "E10+" },
            new Item { ItemName = "Ember Atlas", ItemType = "GAME", MediaFormat = "Switch cartridge", AgeRating = "E" }
        };
        db.Items.AddRange(items);
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        for (var n = 0; n < items.Length; n++)
        {
            foreach (var location in new[] { north, west })
            {
                var quantity = n == 5 ? 0 : (location == north ? 12 - n : 6 - n);
                db.Inventory.Add(new InventoryBalance { ItemId = items[n].ItemId, LocationId = location.LocationId, QuantityOnHand = quantity, LastUpdatedAt = now });
                if (quantity > 0) db.Transactions.Add(new StockTransaction
                {
                    EmployeeId = manager.EmployeeId,
                    ItemId = items[n].ItemId,
                    LocationId = location.LocationId,
                    TransactionType = "CHECK IN",
                    QuantityMoved = quantity,
                    TransactionDatetime = now,
                    Notes = "Opening demonstration stock"
                });
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
