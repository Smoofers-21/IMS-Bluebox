using BlueBox.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BlueBox.Web.Services;

public sealed record CatalogRow
{
    public int ItemId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Format { get; init; } = "";
    public string? Rating { get; init; }
    public string Status { get; init; } = "";
    public long OnHand { get; init; }
}
public sealed record InventoryRow
{
    public int ItemId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Format { get; init; } = "";
    public string ItemStatus { get; init; } = "";
    public int LocationId { get; init; }
    public string Location { get; init; } = "";
    public string LocationStatus { get; init; } = "";
    public int OnHand { get; init; }
}
public sealed record ActivityRow
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Format { get; init; } = "";
    public string Location { get; init; } = "";
    public string Direction { get; init; } = "";
    public int Quantity { get; init; }
    public string Employee { get; init; } = "";
    public DateTime Time { get; init; }
    public string? Notes { get; init; }
}

public sealed class InventoryQueries(BlueBoxDbContext db)
{
    public IQueryable<CatalogRow> Catalog(string? search, string? type) =>
        db.Items.AsNoTracking()
            .Where(i => string.IsNullOrEmpty(search) || i.ItemName.Contains(search) || i.ItemId.ToString() == search)
            .Where(i => string.IsNullOrEmpty(type) || i.ItemType == type)
            .Select(i => new CatalogRow
            {
                ItemId = i.ItemId,
                Name = i.ItemName,
                Type = i.ItemType,
                Format = i.MediaFormat,
                Rating = i.AgeRating,
                Status = i.ItemStatus,
                OnHand = db.Inventory.Where(b => b.ItemId == i.ItemId).Sum(b => (long?)b.QuantityOnHand) ?? 0
            });

    public IQueryable<InventoryRow> Inventory(string? search, string? type, int? location) =>
        from item in db.Items.AsNoTracking()
        from warehouse in db.Locations.AsNoTracking()
        join balance in db.Inventory on new { item.ItemId, warehouse.LocationId } equals new { balance.ItemId, balance.LocationId } into balances
        from balance in balances.DefaultIfEmpty()
        where (string.IsNullOrEmpty(search) || item.ItemName.Contains(search) || item.ItemId.ToString() == search)
            && (string.IsNullOrEmpty(type) || item.ItemType == type)
            && (!location.HasValue || warehouse.LocationId == location.Value)
        select new InventoryRow
        {
            ItemId = item.ItemId,
            Name = item.ItemName,
            Type = item.ItemType,
            Format = item.MediaFormat,
            ItemStatus = item.ItemStatus,
            LocationId = warehouse.LocationId,
            Location = warehouse.LocationName,
            LocationStatus = warehouse.LocationStatus,
            OnHand = balance == null ? 0 : balance.QuantityOnHand
        };

    public IQueryable<ActivityRow> Activity(string? search, string? direction, int? location) =>
        from movement in db.Transactions.AsNoTracking()
        join item in db.Items on movement.ItemId equals item.ItemId
        join warehouse in db.Locations on movement.LocationId equals warehouse.LocationId
        join employee in db.Employees on movement.EmployeeId equals employee.EmployeeId
        where (string.IsNullOrEmpty(search) || item.ItemName.Contains(search) || item.ItemId.ToString() == search)
            && (string.IsNullOrEmpty(direction) || movement.TransactionType == direction)
            && (!location.HasValue || warehouse.LocationId == location.Value)
        select new ActivityRow
        {
            Id = movement.TransactionId,
            Name = item.ItemName,
            Format = item.MediaFormat,
            Location = warehouse.LocationName,
            Direction = movement.TransactionType,
            Quantity = movement.QuantityMoved,
            Employee = employee.FirstName + " " + employee.LastName,
            Time = movement.TransactionDatetime,
            Notes = movement.Notes
        };
}
