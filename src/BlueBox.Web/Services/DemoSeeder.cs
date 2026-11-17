using System.Data;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace BlueBox.Web.Services;

public static class DemoSeeder
{
    public static async Task SeedAsync(SqlDatabase database, IPasswordHasher<Employee> hasher, string password)
    {
        if (password.Length < 12) throw new InvalidOperationException("Choose a demo password of at least 12 characters.");
        await database.InTransactionAsync(async (connection, transaction, ct) =>
        {
            using var check = SqlDatabase.Command("""
                SELECT (SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK,HOLDLOCK))
                    + (SELECT COUNT_BIG(*) FROM dbo.ITEM WITH (UPDLOCK,HOLDLOCK))
                    + (SELECT COUNT_BIG(*) FROM dbo.LOCATION WITH (UPDLOCK,HOLDLOCK))
                    + (SELECT COUNT_BIG(*) FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK,HOLDLOCK))
                    + (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG WITH (UPDLOCK,HOLDLOCK));
                """, connection, transaction);
            if (Convert.ToInt64(await check.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("Demo seeding requires empty BlueBox tables. No existing records were changed.");
            var employees = new[]
            {
                new Employee { FirstName="Jordan", LastName="Lee", Username="jordan.lee", AccessRole="MANAGER" },
                new Employee { FirstName="Avery", LastName="Chen", Username="avery.chen" },
                new Employee { FirstName="Sam", LastName="Rivera", Username="sam.rivera", EmploymentStatus="INACTIVE" }
            };
            foreach (var employee in employees)
            {
                using var insert = SqlDatabase.Command("""
                    INSERT INTO dbo.EMPLOYEE (first_name,last_name,username,password_hash,access_role,employment_status)
                    OUTPUT INSERTED.employee_id VALUES (@first,@last,@username,@hash,@role,@status);
                    """, connection, transaction,
                    SqlDatabase.Parameter("@first", SqlDbType.VarChar, employee.FirstName, 50), SqlDatabase.Parameter("@last", SqlDbType.VarChar, employee.LastName, 50),
                    SqlDatabase.Parameter("@username", SqlDbType.VarChar, employee.Username, 50), SqlDatabase.Parameter("@hash", SqlDbType.VarChar, hasher.HashPassword(employee, password), 255),
                    SqlDatabase.Parameter("@role", SqlDbType.VarChar, employee.AccessRole, 20), SqlDatabase.Parameter("@status", SqlDbType.VarChar, employee.EmploymentStatus, 20));
                employee.EmployeeId = Convert.ToInt32(await insert.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            }
            var locations = new List<int>();
            foreach (var name in new[] { "North Loop", "West End" })
            {
                using var insert = SqlDatabase.Command("""
                    INSERT INTO dbo.LOCATION (location_name,address,location_status)
                    OUTPUT INSERTED.location_id VALUES (@name,@address,'ACTIVE');
                    """, connection, transaction, SqlDatabase.Parameter("@name", SqlDbType.VarChar, name, 100),
                    SqlDatabase.Parameter("@address", SqlDbType.VarChar, name == "North Loop" ? "100 Demo Street" : "200 Example Avenue", 255));
                locations.Add(Convert.ToInt32(await insert.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture));
            }
            var items = new[]
            {
                ("Moonlight Signal","MOVIE","Blu-ray","PG-13"), ("Moonlight Signal","MOVIE","DVD","PG-13"),
                ("The Last Orchard","MOVIE","DVD","PG"), ("Neon Circuit","GAME","PS5 disc","T"),
                ("Skyline Drift","GAME","Xbox Series X disc","E10+"), ("Ember Atlas","GAME","Switch cartridge","E")
            };
            for (var n = 0; n < items.Length; n++)
            {
                var item = items[n];
                using var insert = SqlDatabase.Command("""
                    INSERT INTO dbo.ITEM (item_name,item_type,media_format,age_rating,item_status)
                    OUTPUT INSERTED.item_id VALUES (@name,@type,@format,@rating,'ACTIVE');
                    """, connection, transaction, SqlDatabase.Parameter("@name", SqlDbType.VarChar, item.Item1, 150),
                    SqlDatabase.Parameter("@type", SqlDbType.VarChar, item.Item2, 10), SqlDatabase.Parameter("@format", SqlDbType.VarChar, item.Item3, 30),
                    SqlDatabase.Parameter("@rating", SqlDbType.VarChar, item.Item4, 20));
                var id = Convert.ToInt32(await insert.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
                for (var l = 0; l < locations.Count; l++)
                {
                    var quantity = n == 5 ? 0 : (l == 0 ? 12 - n : 6 - n);
                    using var stock = SqlDatabase.Command("""
                        INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
                        VALUES (@item,@location,@quantity,SYSUTCDATETIME());
                        IF @quantity > 0
                            INSERT INTO dbo.TRANSACTION_LOG (employee_id,item_id,location_id,transaction_type,quantity_moved,transaction_datetime,notes)
                            VALUES (@employee,@item,@location,'CHECK IN',@quantity,SYSUTCDATETIME(),'Opening demonstration stock');
                        """, connection, transaction, SqlDatabase.Parameter("@item", SqlDbType.Int, id),
                        SqlDatabase.Parameter("@location", SqlDbType.Int, locations[l]), SqlDatabase.Parameter("@quantity", SqlDbType.Int, quantity),
                        SqlDatabase.Parameter("@employee", SqlDbType.Int, employees[0].EmployeeId));
                    await stock.ExecuteNonQueryAsync(ct);
                }
            }
            return true;
        });
    }
}
