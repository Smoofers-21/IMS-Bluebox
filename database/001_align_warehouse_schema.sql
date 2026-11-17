/*
    BlueBox: update the five existing warehouse tables to the agreed plan.

    Existing database: run this file ONLY, not 000_create_baseline.sql.
    Set @ExpectedDatabase below, select that database in SSMS, then execute
    this entire file in a fresh query window (no GO separators required).

    This is a one-time, additive migration. It does not drop tables, delete
    rows, adjust stock, or rewrite movement history. Existing items become
    ACTIVE. Existing correction references and optional metadata stay NULL.

    Existing data must satisfy the new constraints. If it does not, all
    changes in this migration roll back; resolve the reported data issue
    deliberately rather than disabling constraints.
*/
DECLARE @ExpectedDatabase sysname = N'YourBlueBoxDatabaseName';

IF @@TRANCOUNT <> 0 OR (@@OPTIONS & 2) = 2
BEGIN
    RAISERROR (
        'Use a fresh query window with no open transaction and IMPLICIT_TRANSACTIONS OFF.',
        16, 1
    );
    RETURN;
END;

IF DB_ID() <= 4
   OR ISNULL(DB_ID(@ExpectedDatabase), -1) <> DB_ID()
BEGIN
    THROW 50101,
        'Select your BlueBox database and set @ExpectedDatabase to its exact name.',
        1;
END;

IF OBJECT_ID(N'dbo.EMPLOYEE', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ITEM', N'U') IS NULL
   OR OBJECT_ID(N'dbo.LOCATION', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CURRENT_INVENTORY', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TRANSACTION_LOG', N'U') IS NULL
BEGIN
    THROW 50102,
        'The five baseline dbo warehouse tables must already exist.',
        1;
END;

-- Refuse a rerun or partially changed schema instead of replacing anything.
IF COL_LENGTH(N'dbo.ITEM', N'poster_url') IS NOT NULL
   OR COL_LENGTH(N'dbo.ITEM', N'age_rating') IS NOT NULL
   OR COL_LENGTH(N'dbo.ITEM', N'item_status') IS NOT NULL
   OR COL_LENGTH(N'dbo.TRANSACTION_LOG', N'corrects_transaction_id') IS NOT NULL
BEGIN
    THROW 50103,
        'A planned column already exists. This migration is one-time; inspect the schema before continuing.',
        1;
END;

BEGIN TRY
    -- A nested batch makes DDL compilation failures catchable below.
    -- XACT_ABORT is scoped to this nested batch.
    EXEC sys.sp_executesql N'
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        ALTER TABLE dbo.ITEM ADD
            poster_url VARCHAR(2048) NULL,
            age_rating VARCHAR(20) NULL,
            item_status VARCHAR(10) NOT NULL
                CONSTRAINT DF_ITEM_status DEFAULT (''ACTIVE'');

        ALTER TABLE dbo.TRANSACTION_LOG ADD
            corrects_transaction_id BIGINT NULL;

        ALTER TABLE dbo.EMPLOYEE WITH CHECK ADD
            CONSTRAINT CK_EMPLOYEE_access_role CHECK
            (
                access_role COLLATE Latin1_General_100_BIN2
                    IN (''STAFF'', ''MANAGER'')
                AND DATALENGTH(access_role) = LEN(access_role)
            );

        ALTER TABLE dbo.EMPLOYEE WITH CHECK ADD
            CONSTRAINT CK_EMPLOYEE_employment_status CHECK
            (
                employment_status COLLATE Latin1_General_100_BIN2
                    IN (''ACTIVE'', ''INACTIVE'', ''TERMINATED'')
                AND DATALENGTH(employment_status) = LEN(employment_status)
            );

        ALTER TABLE dbo.LOCATION WITH CHECK ADD
            CONSTRAINT CK_LOCATION_status CHECK
            (
                location_status COLLATE Latin1_General_100_BIN2
                    IN (''ACTIVE'', ''INACTIVE'')
                AND DATALENGTH(location_status) = LEN(location_status)
            );

        ALTER TABLE dbo.ITEM WITH CHECK ADD
            CONSTRAINT CK_ITEM_media_format CHECK
            (
                media_format COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
            );

        -- Compile checks referencing new columns only after those columns exist.
        EXEC sys.sp_executesql N''
            ALTER TABLE dbo.ITEM WITH CHECK ADD
                CONSTRAINT CK_ITEM_status CHECK
                (
                    item_status COLLATE Latin1_General_100_BIN2
                        IN (''''ACTIVE'''', ''''ARCHIVED'''')
                    AND DATALENGTH(item_status) = LEN(item_status)
                );

            ALTER TABLE dbo.TRANSACTION_LOG WITH CHECK ADD
                CONSTRAINT FK_TRANSACTION_LOG_correction
                    FOREIGN KEY (corrects_transaction_id)
                    REFERENCES dbo.TRANSACTION_LOG (transaction_id)
                    ON DELETE NO ACTION;

            ALTER TABLE dbo.TRANSACTION_LOG WITH CHECK ADD
                CONSTRAINT CK_TRANSACTION_LOG_correction_details CHECK
                (
                    corrects_transaction_id IS NULL
                    OR
                    (
                        corrects_transaction_id <> transaction_id
                        AND notes IS NOT NULL
                        AND notes COLLATE Latin1_General_100_BIN2 LIKE
                            ''''%[^ '''' + CHAR(9) + CHAR(10) + CHAR(11)
                            + CHAR(12) + CHAR(13) + '''']%''''
                    )
                );

            CREATE INDEX IX_TRANSACTION_LOG_corrects_transaction_id
                ON dbo.TRANSACTION_LOG (corrects_transaction_id)
                WHERE corrects_transaction_id IS NOT NULL;
        '';

        COMMIT TRANSACTION;
    ';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

PRINT 'BlueBox schema update completed. No inventory movements or balances were changed.';

-- Result sets to review after a successful update.
SELECT
    OBJECT_NAME(c.object_id) AS table_name,
    c.name AS column_name,
    TYPE_NAME(c.user_type_id) AS data_type,
    c.max_length,
    c.is_nullable
FROM sys.columns AS c
WHERE (c.object_id = OBJECT_ID(N'dbo.ITEM')
       AND c.name IN (N'poster_url', N'age_rating', N'item_status'))
   OR (c.object_id = OBJECT_ID(N'dbo.TRANSACTION_LOG')
       AND c.name = N'corrects_transaction_id')
ORDER BY table_name, column_name;

SELECT name, is_disabled, is_not_trusted
FROM sys.check_constraints
WHERE name IN
(
    N'CK_EMPLOYEE_access_role', N'CK_EMPLOYEE_employment_status',
    N'CK_LOCATION_status', N'CK_ITEM_media_format', N'CK_ITEM_status',
    N'CK_TRANSACTION_LOG_correction_details'
)
UNION ALL
SELECT name, is_disabled, is_not_trusted
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'dbo.TRANSACTION_LOG')
  AND name = N'FK_TRANSACTION_LOG_correction';
