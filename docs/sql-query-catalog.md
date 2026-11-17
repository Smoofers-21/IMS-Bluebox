# Runtime SQL query catalog

This catalog lists SQL literals used by the application after the instructor-template refactor. Queries use typed parameters; credential values are not included. Schema creation/alteration and test-only utility queries are excluded. Some entries are transaction batches containing more than one SELECT/INSERT/UPDATE statement.

No runtime DELETE statement is used. Retiring records and manager corrections remain Milestone 2. Recheck this catalog when changing runtime SQL.

## 1. EmployeeRepository

Source: [src/BlueBox.Web/Data/EmployeeRepository.cs](../src/BlueBox.Web/Data/EmployeeRepository.cs#L11)

```sql
SELECT employee_id, first_name, last_name, username, password_hash, access_role, employment_status FROM dbo.EMPLOYEE WHERE username = @username;
```

## 2. EmployeeRepository

Source: [src/BlueBox.Web/Data/EmployeeRepository.cs](../src/BlueBox.Web/Data/EmployeeRepository.cs#L18)

```sql
SELECT employee_id, first_name, last_name, username, password_hash, access_role, employment_status FROM dbo.EMPLOYEE WHERE employee_id = @id;
```

## 3. EmployeeRepository

Source: [src/BlueBox.Web/Data/EmployeeRepository.cs](../src/BlueBox.Web/Data/EmployeeRepository.cs#L25)

```sql
UPDATE dbo.EMPLOYEE SET password_hash = @hash WHERE employee_id = @id;
```

## 4. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L16)

```sql
SELECT (SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK,HOLDLOCK))
+ (SELECT COUNT_BIG(*) FROM dbo.ITEM WITH (UPDLOCK,HOLDLOCK))
+ (SELECT COUNT_BIG(*) FROM dbo.LOCATION WITH (UPDLOCK,HOLDLOCK))
+ (SELECT COUNT_BIG(*) FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK,HOLDLOCK))
+ (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG WITH (UPDLOCK,HOLDLOCK));
```

## 5. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L33)

```sql
INSERT INTO dbo.EMPLOYEE (first_name,last_name,username,password_hash,access_role,employment_status)
OUTPUT INSERTED.employee_id VALUES (@first,@last,@username,@hash,@role,@status);
```

## 6. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L45)

```sql
INSERT INTO dbo.LOCATION (location_name,address,location_status)
OUTPUT INSERTED.location_id VALUES (@name,@address,'ACTIVE');
```

## 7. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L61)

```sql
INSERT INTO dbo.ITEM (item_name,item_type,media_format,age_rating,item_status)
OUTPUT INSERTED.item_id VALUES (@name,@type,@format,@rating,'ACTIVE');
```

## 8. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L71)

```sql
INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
VALUES (@item,@location,@quantity,SYSUTCDATETIME());
IF @quantity > 0
    INSERT INTO dbo.TRANSACTION_LOG (employee_id,item_id,location_id,transaction_type,quantity_moved,transaction_datetime,notes)
    VALUES (@employee,@item,@location,'CHECK IN',@quantity,SYSUTCDATETIME(),'Opening demonstration stock');
```

## 9. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L50)

```sql
SELECT TOP (100) i.item_id, i.item_name, i.item_type, i.media_format,
    i.age_rating, i.item_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS on_hand
FROM dbo.ITEM AS i
LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.item_id = i.item_id
WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
    AND (@type = '' OR i.item_type = @type)
GROUP BY i.item_id, i.item_name, i.item_type, i.media_format, i.age_rating, i.item_status
ORDER BY i.item_name, i.media_format, i.item_id;
```

## 10. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L74)

```sql
SELECT TOP (100) i.item_id, i.item_name, i.item_type, i.media_format, i.item_status,
    l.location_id, l.location_name, l.location_status, COALESCE(b.quantity_on_hand, 0) AS on_hand
FROM dbo.ITEM AS i CROSS JOIN dbo.LOCATION AS l
LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.item_id = i.item_id AND b.location_id = l.location_id
WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
    AND (@type = '' OR i.item_type = @type) AND (@location IS NULL OR l.location_id = @location)
    AND (@item IS NULL OR i.item_id = @item)
ORDER BY i.item_name, l.location_name, i.media_format, i.item_id, l.location_id;
```

## 11. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L103)

```sql
SELECT t.transaction_id, i.item_name, i.media_format, l.location_name,
    t.transaction_type, t.quantity_moved, e.first_name + ' ' + e.last_name AS employee_name,
    t.transaction_datetime, t.notes
FROM dbo.TRANSACTION_LOG AS t
INNER JOIN dbo.ITEM AS i ON i.item_id = t.item_id
INNER JOIN dbo.LOCATION AS l ON l.location_id = t.location_id
INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id = t.employee_id
WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
    AND (@direction = '' OR t.transaction_type = @direction) AND (@location IS NULL OR l.location_id = @location)
ORDER BY t.transaction_datetime DESC, t.transaction_id DESC
OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
```

## 12. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L138)

```sql
SELECT location_id, location_name, address, location_status FROM dbo.LOCATION WHERE @active = 0 OR location_status = 'ACTIVE' ORDER BY location_name, location_id;
```

## 13. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L151)

```sql
SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_status = 'ACTIVE' ORDER BY item_name, media_format, item_id;
```

## 14. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L157)

```sql
SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_id = @id;
```

## 15. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L165)

```sql
SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id = @item AND location_id = @location;
```

## 16. ManagerBootstrap

Source: [src/BlueBox.Web/Services/ManagerBootstrap.cs](../src/BlueBox.Web/Services/ManagerBootstrap.cs#L17)

```sql
SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK, HOLDLOCK)
WHERE (access_role = 'MANAGER' AND employment_status = 'ACTIVE') OR username = @username;
```

## 17. ManagerBootstrap

Source: [src/BlueBox.Web/Services/ManagerBootstrap.cs](../src/BlueBox.Web/Services/ManagerBootstrap.cs#L24)

```sql
INSERT INTO dbo.EMPLOYEE (first_name, last_name, username, password_hash, access_role, employment_status)
VALUES ('BlueBox', 'Manager', @username, @hash, 'MANAGER', 'ACTIVE');
```

## 18. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L20)

```sql
SELECT CASE
WHEN NOT EXISTS (SELECT 1 FROM dbo.EMPLOYEE WITH (HOLDLOCK)
    WHERE employee_id = @employee AND employment_status = 'ACTIVE' AND access_role IN ('STAFF', 'MANAGER')) THEN 1
WHEN NOT EXISTS (SELECT 1 FROM dbo.ITEM WITH (HOLDLOCK)
    WHERE item_id = @item AND item_status = 'ACTIVE') THEN 2
WHEN NOT EXISTS (SELECT 1 FROM dbo.LOCATION WITH (HOLDLOCK)
    WHERE location_id = @location AND location_status = 'ACTIVE') THEN 3
ELSE 0 END;
```

## 19. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L37)

```sql
Select an active catalog item.
```

## 20. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L38)

```sql
Select an active warehouse.
```

## 21. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L43)

```sql
SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK, HOLDLOCK)
WHERE item_id = @item AND location_id = @location;
```

## 22. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L55)

```sql
UPDATE dbo.CURRENT_INVENTORY SET quantity_on_hand = @next, last_updated_at = SYSUTCDATETIME()
WHERE item_id = @item AND location_id = @location AND quantity_on_hand = @current;
SELECT @@ROWCOUNT;
```

## 23. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L59)

```sql
INSERT INTO dbo.CURRENT_INVENTORY (item_id, location_id, quantity_on_hand, last_updated_at)
VALUES (@item, @location, @next, SYSUTCDATETIME());
SELECT @@ROWCOUNT;
```

## 24. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L69)

```sql
INSERT INTO dbo.TRANSACTION_LOG
    (employee_id, item_id, location_id, transaction_type, quantity_moved, transaction_datetime, notes)
VALUES (@employee, @item, @location, @direction, @quantity, SYSUTCDATETIME(), @notes);
```
