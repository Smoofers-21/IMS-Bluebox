-- Portfolio-only upgrade for the already-installed five-table schema.
-- Replace YourAzureDatabaseName with the exact database selected in Query editor.
-- Adds indexes only; preserves tables, constraints, and all existing records.
-- Run the whole file in a fresh query session. No USE or GO required.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ExpectedDatabase sysname = N'YourAzureDatabaseName';
IF @ExpectedDatabase = N'YourAzureDatabaseName' OR DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Set @ExpectedDatabase to the exact selected portfolio database name before running this upgrade.', 1;
IF @@TRANCOUNT <> 0
    THROW 51001, 'Run this upgrade in a fresh query session without an existing transaction.', 1;
IF OBJECT_ID(N'dbo.EMPLOYEE', N'U') IS NULL OR OBJECT_ID(N'dbo.ITEM', N'U') IS NULL
   OR OBJECT_ID(N'dbo.LOCATION', N'U') IS NULL OR OBJECT_ID(N'dbo.CURRENT_INVENTORY', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TRANSACTION_LOG', N'U') IS NULL
    THROW 51003, 'The portfolio tables must already exist. Use the fresh installer only for an empty schema.', 1;

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TRANSACTION_LOG') AND name=N'IX_TRANSACTION_LOG_corrects_transaction_id')
        CREATE INDEX IX_TRANSACTION_LOG_corrects_transaction_id
            ON dbo.TRANSACTION_LOG (corrects_transaction_id) WHERE corrects_transaction_id IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TRANSACTION_LOG') AND name=N'IX_TRANSACTION_LOG_item_id')
        CREATE INDEX IX_TRANSACTION_LOG_item_id ON dbo.TRANSACTION_LOG (item_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TRANSACTION_LOG') AND name=N'IX_TRANSACTION_LOG_location_id')
        CREATE INDEX IX_TRANSACTION_LOG_location_id ON dbo.TRANSACTION_LOG (location_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CURRENT_INVENTORY') AND name=N'IX_CURRENT_INVENTORY_location')
        CREATE INDEX IX_CURRENT_INVENTORY_location ON dbo.CURRENT_INVENTORY (location_id) INCLUDE (quantity_on_hand);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT DB_NAME() AS database_name, N'Portfolio indexes checked/created; existing data retained.' AS result;
