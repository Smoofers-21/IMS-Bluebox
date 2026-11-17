# Runtime SQL query catalog

SQL literals used by the application, following the instructor ADO.NET template. Values use typed parameters; credentials, schema scripts, and test-only statements are excluded. Transaction batches can contain several statements.

No runtime DELETE is used. Retirement updates status; corrections append linked movements. See [Milestone 2](milestone-2.md) for safeguards. Recheck this catalog when runtime queries change.

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

## 4. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L16)

```sql
SELECT employee_id, first_name, last_name, username, access_role, employment_status FROM dbo.EMPLOYEE ORDER BY last_name, first_name, employee_id;
```

## 5. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L23)

```sql
SELECT COUNT(*) FROM dbo.EMPLOYEE WITH (HOLDLOCK) WHERE employee_id = @actor AND employment_status = 'ACTIVE' AND access_role = 'MANAGER';
```

## 6. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L36)

```sql
SELECT item_id FROM dbo.ITEM WITH (UPDLOCK, HOLDLOCK) WHERE item_id = @id;
```

## 7. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L41)

```sql
SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY WITH (HOLDLOCK) WHERE item_id = @id AND quantity_on_hand <> 0;
```

## 8. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L47)

```sql
UPDATE dbo.ITEM SET item_name=@name, item_type=@type, media_format=@format, poster_url=@poster, age_rating=@rating, item_status=@status WHERE item_id=@id;
```

## 9. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L49)

```sql
INSERT INTO dbo.ITEM (item_name, item_type, media_format, poster_url, age_rating, item_status) VALUES (@name, @type, @format, @poster, @rating, @status);
```

## 10. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L69)

```sql
SELECT location_id FROM dbo.LOCATION WITH (UPDLOCK, HOLDLOCK) WHERE location_id=@id;
```

## 11. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L74)

```sql
SELECT COUNT(*) FROM dbo.CURRENT_INVENTORY WITH (HOLDLOCK) WHERE location_id=@id AND quantity_on_hand <> 0;
```

## 12. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L80)

```sql
UPDATE dbo.LOCATION SET location_name=@name, address=@address, location_status=@status WHERE location_id=@id;
```

## 13. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L82)

```sql
INSERT INTO dbo.LOCATION (location_name, address, location_status) VALUES (@name, @address, @status);
```

## 14. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L102)

```sql
SELECT COUNT(*) FROM dbo.EMPLOYEE WITH (UPDLOCK, HOLDLOCK);
```

## 15. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L107)

```sql
SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE employee_id=@id;
```

## 16. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L110)

```sql
SELECT COUNT(*) FROM dbo.EMPLOYEE WHERE employment_status='ACTIVE' AND access_role='MANAGER' AND employee_id<>@id;
```

## 17. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L114)

```sql
UPDATE dbo.EMPLOYEE SET first_name=@first, last_name=@last, username=@username, access_role=@role,
    employment_status=@status, password_hash=COALESCE(@hash, password_hash) WHERE employee_id=@id;
```

## 18. AdministrationService

Source: [src/BlueBox.Web/Services/AdministrationService.cs](../src/BlueBox.Web/Services/AdministrationService.cs#L117)

```sql
INSERT INTO dbo.EMPLOYEE (first_name, last_name, username, access_role, employment_status, password_hash)
VALUES (@first, @last, @username, @role, @status, @hash);
```

## 19. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L16)

```sql
SELECT (SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK,HOLDLOCK))
    + (SELECT COUNT_BIG(*) FROM dbo.ITEM WITH (UPDLOCK,HOLDLOCK))
    + (SELECT COUNT_BIG(*) FROM dbo.LOCATION WITH (UPDLOCK,HOLDLOCK))
    + (SELECT COUNT_BIG(*) FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK,HOLDLOCK))
    + (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG WITH (UPDLOCK,HOLDLOCK));
```

## 20. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L33)

```sql
INSERT INTO dbo.EMPLOYEE (first_name,last_name,username,password_hash,access_role,employment_status)
OUTPUT INSERTED.employee_id VALUES (@first,@last,@username,@hash,@role,@status);
```

## 21. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L45)

```sql
INSERT INTO dbo.LOCATION (location_name,address,location_status)
OUTPUT INSERTED.location_id VALUES (@name,@address,'ACTIVE');
```

## 22. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L61)

```sql
INSERT INTO dbo.ITEM (item_name,item_type,media_format,age_rating,item_status)
OUTPUT INSERTED.item_id VALUES (@name,@type,@format,@rating,'ACTIVE');
```

## 23. DemoSeeder

Source: [src/BlueBox.Web/Services/DemoSeeder.cs](../src/BlueBox.Web/Services/DemoSeeder.cs#L71)

```sql
INSERT INTO dbo.CURRENT_INVENTORY (item_id,location_id,quantity_on_hand,last_updated_at)
VALUES (@item,@location,@quantity,SYSUTCDATETIME());
IF @quantity > 0
    INSERT INTO dbo.TRANSACTION_LOG (employee_id,item_id,location_id,transaction_type,quantity_moved,transaction_datetime,notes)
    VALUES (@employee,@item,@location,'CHECK IN',@quantity,SYSUTCDATETIME(),'Opening demonstration stock');
```

## 24. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L52)

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

## 25. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L76)

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

## 26. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L105)

```sql
SELECT t.transaction_id, i.item_name, i.media_format, l.location_name,
    t.transaction_type, t.quantity_moved, e.first_name + ' ' + e.last_name AS employee_name,
    t.transaction_datetime, t.notes, t.corrects_transaction_id,
    (SELECT MIN(c.transaction_id) FROM dbo.TRANSACTION_LOG AS c WHERE c.corrects_transaction_id=t.transaction_id) AS correction_id
FROM dbo.TRANSACTION_LOG AS t
INNER JOIN dbo.ITEM AS i ON i.item_id = t.item_id
INNER JOIN dbo.LOCATION AS l ON l.location_id = t.location_id
INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id = t.employee_id
WHERE (@search = '' OR CHARINDEX(@search, i.item_name) > 0 OR TRY_CONVERT(int, @search) = i.item_id)
    AND (@direction = '' OR t.transaction_type = @direction) AND (@location IS NULL OR l.location_id = @location)
ORDER BY t.transaction_datetime DESC, t.transaction_id DESC
OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
```

## 27. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L143)

```sql
SELECT t.transaction_id, t.transaction_type, t.quantity_moved, t.transaction_datetime, t.notes,
    t.corrects_transaction_id, i.item_name, i.media_format, l.location_name,
    e.first_name + ' ' + e.last_name AS employee_name,
    (SELECT MIN(c.transaction_id) FROM dbo.TRANSACTION_LOG AS c WHERE c.corrects_transaction_id=t.transaction_id) AS correction_id
FROM dbo.TRANSACTION_LOG AS t
INNER JOIN dbo.ITEM AS i ON i.item_id=t.item_id
INNER JOIN dbo.LOCATION AS l ON l.location_id=t.location_id
INNER JOIN dbo.EMPLOYEE AS e ON e.employee_id=t.employee_id
WHERE t.transaction_id=@id;
```

## 28. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L176)

```sql
SELECT location_id, location_name, address, location_status FROM dbo.LOCATION WHERE @active = 0 OR location_status = 'ACTIVE' ORDER BY location_name, location_id;
```

## 29. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L189)

```sql
SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_status = 'ACTIVE' ORDER BY item_name, media_format, item_id;
```

## 30. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L195)

```sql
SELECT item_id, item_name, item_type, media_format, poster_url, age_rating, item_status FROM dbo.ITEM WHERE item_id = @id;
```

## 31. InventoryQueries

Source: [src/BlueBox.Web/Services/InventoryQueries.cs](../src/BlueBox.Web/Services/InventoryQueries.cs#L203)

```sql
SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WHERE item_id = @item AND location_id = @location;
```

## 32. ManagerBootstrap

Source: [src/BlueBox.Web/Services/ManagerBootstrap.cs](../src/BlueBox.Web/Services/ManagerBootstrap.cs#L17)

```sql
SELECT COUNT_BIG(*) FROM dbo.EMPLOYEE WITH (UPDLOCK, HOLDLOCK)
WHERE (access_role = 'MANAGER' AND employment_status = 'ACTIVE') OR username = @username;
```

## 33. ManagerBootstrap

Source: [src/BlueBox.Web/Services/ManagerBootstrap.cs](../src/BlueBox.Web/Services/ManagerBootstrap.cs#L24)

```sql
INSERT INTO dbo.EMPLOYEE (first_name, last_name, username, password_hash, access_role, employment_status)
VALUES ('BlueBox', 'Manager', @username, @hash, 'MANAGER', 'ACTIVE');
```

## 34. OverviewQueries

Source: [src/BlueBox.Web/Services/OverviewQueries.cs](../src/BlueBox.Web/Services/OverviewQueries.cs#L13)

```sql
SELECT COALESCE((SELECT SUM(CAST(quantity_on_hand AS bigint)) FROM dbo.CURRENT_INVENTORY), 0) AS units,
    (SELECT COUNT(*) FROM dbo.ITEM WHERE item_status='ACTIVE') AS items,
    (SELECT COUNT(*) FROM dbo.LOCATION WHERE location_status='ACTIVE') AS locations,
    (SELECT COUNT_BIG(*) FROM dbo.TRANSACTION_LOG) AS movements;
```

## 35. OverviewQueries

Source: [src/BlueBox.Web/Services/OverviewQueries.cs](../src/BlueBox.Web/Services/OverviewQueries.cs#L25)

```sql
SELECT l.location_name, l.location_status, COALESCE(SUM(CAST(b.quantity_on_hand AS bigint)), 0) AS units
FROM dbo.LOCATION AS l LEFT JOIN dbo.CURRENT_INVENTORY AS b ON b.location_id=l.location_id
GROUP BY l.location_id, l.location_name, l.location_status ORDER BY l.location_name, l.location_id;
```

## 36. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L24)

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

## 37. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L47)

```sql
SELECT quantity_on_hand FROM dbo.CURRENT_INVENTORY WITH (UPDLOCK, HOLDLOCK)
WHERE item_id = @item AND location_id = @location;
```

## 38. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L59)

```sql
UPDATE dbo.CURRENT_INVENTORY SET quantity_on_hand = @next, last_updated_at = SYSUTCDATETIME()
WHERE item_id = @item AND location_id = @location AND quantity_on_hand = @current;
SELECT @@ROWCOUNT;
```

## 39. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L63)

```sql
INSERT INTO dbo.CURRENT_INVENTORY (item_id, location_id, quantity_on_hand, last_updated_at)
VALUES (@item, @location, @next, SYSUTCDATETIME());
SELECT @@ROWCOUNT;
```

## 40. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L73)

```sql
INSERT INTO dbo.TRANSACTION_LOG
    (employee_id, item_id, location_id, transaction_type, quantity_moved, transaction_datetime, notes, corrects_transaction_id)
VALUES (@employee, @item, @location, @direction, @quantity, SYSUTCDATETIME(), @notes, @corrects);
```

## 41. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L96)

```sql
SELECT item_id, location_id, transaction_type, quantity_moved
FROM dbo.TRANSACTION_LOG WITH (UPDLOCK, HOLDLOCK) WHERE transaction_id=@id;
```

## 42. StockService

Source: [src/BlueBox.Web/Services/StockService.cs](../src/BlueBox.Web/Services/StockService.cs#L106)

```sql
SELECT COUNT(*) FROM dbo.TRANSACTION_LOG WITH (HOLDLOCK) WHERE corrects_transaction_id=@id;
```
