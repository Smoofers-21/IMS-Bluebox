using BlueBox.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Data;

public sealed class BlueBoxDbContext(DbContextOptions<BlueBoxDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<InventoryBalance> Inventory => Set<InventoryBalance>();
    public DbSet<StockTransaction> Transactions => Set<StockTransaction>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var employee = model.Entity<Employee>();
        employee.ToTable("EMPLOYEE");
        employee.HasKey(e => e.EmployeeId);
        employee.Property(e => e.EmployeeId).HasColumnName("employee_id");
        employee.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(50).IsUnicode(false);
        employee.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(50).IsUnicode(false);
        employee.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsUnicode(false);
        employee.HasIndex(e => e.Username).IsUnique();
        employee.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsUnicode(false);
        employee.Property(e => e.AccessRole).HasColumnName("access_role").HasMaxLength(20).IsUnicode(false);
        employee.Property(e => e.EmploymentStatus).HasColumnName("employment_status").HasMaxLength(20).IsUnicode(false);
        employee.Ignore(e => e.DisplayName);

        var item = model.Entity<Item>();
        item.ToTable("ITEM");
        item.HasKey(i => i.ItemId);
        item.Property(i => i.ItemId).HasColumnName("item_id");
        item.Property(i => i.ItemName).HasColumnName("item_name").HasMaxLength(150).IsUnicode(false);
        item.Property(i => i.ItemType).HasColumnName("item_type").HasMaxLength(10).IsUnicode(false);
        item.Property(i => i.MediaFormat).HasColumnName("media_format").HasMaxLength(30).IsUnicode(false);
        item.Property(i => i.PosterUrl).HasColumnName("poster_url").HasMaxLength(2048).IsUnicode(false);
        item.Property(i => i.AgeRating).HasColumnName("age_rating").HasMaxLength(20).IsUnicode(false);
        item.Property(i => i.ItemStatus).HasColumnName("item_status").HasMaxLength(10).IsUnicode(false);

        var location = model.Entity<Location>();
        location.ToTable("LOCATION");
        location.HasKey(l => l.LocationId);
        location.Property(l => l.LocationId).HasColumnName("location_id");
        location.Property(l => l.LocationName).HasColumnName("location_name").HasMaxLength(100).IsUnicode(false);
        location.Property(l => l.Address).HasColumnName("address").HasMaxLength(255).IsUnicode(false);
        location.Property(l => l.LocationStatus).HasColumnName("location_status").HasMaxLength(10).IsUnicode(false);

        var inventory = model.Entity<InventoryBalance>();
        inventory.ToTable("CURRENT_INVENTORY", t => t.HasCheckConstraint("CK_CURRENT_INVENTORY_quantity", "quantity_on_hand >= 0"));
        inventory.HasKey(i => new { i.ItemId, i.LocationId });
        inventory.Property(i => i.ItemId).HasColumnName("item_id");
        inventory.Property(i => i.LocationId).HasColumnName("location_id");
        inventory.Property(i => i.QuantityOnHand).HasColumnName("quantity_on_hand");
        inventory.Property(i => i.LastUpdatedAt).HasColumnName("last_updated_at");
        inventory.HasOne<Item>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
        inventory.HasOne<Location>().WithMany().HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.Restrict);

        var transaction = model.Entity<StockTransaction>();
        transaction.ToTable("TRANSACTION_LOG", t => t.HasCheckConstraint("CK_TRANSACTION_LOG_quantity", "quantity_moved > 0"));
        transaction.HasKey(t => t.TransactionId);
        transaction.Property(t => t.TransactionId).HasColumnName("transaction_id");
        transaction.Property(t => t.EmployeeId).HasColumnName("employee_id");
        transaction.Property(t => t.ItemId).HasColumnName("item_id");
        transaction.Property(t => t.LocationId).HasColumnName("location_id");
        transaction.Property(t => t.TransactionType).HasColumnName("transaction_type").HasMaxLength(10).IsUnicode(false);
        transaction.Property(t => t.QuantityMoved).HasColumnName("quantity_moved");
        transaction.Property(t => t.TransactionDatetime).HasColumnName("transaction_datetime");
        transaction.Property(t => t.Notes).HasColumnName("notes").HasMaxLength(255).IsUnicode(false);
        transaction.Property(t => t.CorrectsTransactionId).HasColumnName("corrects_transaction_id");
        transaction.HasOne<Employee>().WithMany().HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        transaction.HasOne<Item>().WithMany().HasForeignKey(t => t.ItemId).OnDelete(DeleteBehavior.Restrict);
        transaction.HasOne<Location>().WithMany().HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        transaction.HasOne<StockTransaction>().WithMany().HasForeignKey(t => t.CorrectsTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
