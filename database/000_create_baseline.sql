DECLARE @ExpectedDatabase sysname = N'YourBlueBoxDatabaseName';

-- Require a fresh query window without an open transaction.
IF @@TRANCOUNT <> 0 OR (@@OPTIONS & 2) = 2
BEGIN
    RAISERROR (
        'Use a query window with no open transaction and IMPLICIT_TRANSACTIONS OFF.',
        16, 1
    );
    RETURN;
END;

-- Verify the selected database and block system databases.
IF DB_ID() <= 4
   OR ISNULL(DB_ID(@ExpectedDatabase), -1) <> DB_ID()
BEGIN
    THROW 50001,
        'Select your BlueBox database and set @ExpectedDatabase to its name.',
        1;
END;

-- Stop before creating anything if a target name already exists.
IF EXISTS
(
    SELECT 1
    FROM sys.objects
    WHERE schema_id = SCHEMA_ID(N'dbo')
      AND name IN
      (
          N'EMPLOYEE',
          N'ITEM',
          N'LOCATION',
          N'TRANSACTION_LOG',
          N'CURRENT_INVENTORY'
      )
)
BEGIN
    THROW 50002,
        'A BlueBox object already exists. All five target names must be free.',
        1;
END;

-- A nested batch lets the outer CATCH handle DDL compilation errors.
BEGIN TRY
    EXEC sys.sp_executesql N'
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        CREATE TABLE dbo.[EMPLOYEE]
        (
            employee_id       INT IDENTITY(1, 1) NOT NULL,
            first_name        VARCHAR(50) NOT NULL,
            last_name         VARCHAR(50) NOT NULL,
            username          VARCHAR(50) NOT NULL,
            password_hash     VARCHAR(255) NOT NULL,
            access_role       VARCHAR(20) NOT NULL,
            employment_status VARCHAR(20) NOT NULL,

            CONSTRAINT PK_EMPLOYEE
                PRIMARY KEY (employee_id),

            CONSTRAINT CK_EMPLOYEE_positive_id
                CHECK (employee_id > 0),

            CONSTRAINT UQ_EMPLOYEE_username
                UNIQUE (username),

            CONSTRAINT CK_EMPLOYEE_first_name
                CHECK
                (
                    first_name COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                ),

            CONSTRAINT CK_EMPLOYEE_last_name
                CHECK
                (
                    last_name COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                ),

            CONSTRAINT CK_EMPLOYEE_username
                CHECK
                (
                    username COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                )
        );

        CREATE TABLE dbo.[ITEM]
        (
            item_id      INT IDENTITY(1, 1) NOT NULL,
            item_name    VARCHAR(150) NOT NULL,
            item_type    VARCHAR(10) NOT NULL,
            media_format VARCHAR(30) NOT NULL,

            CONSTRAINT PK_ITEM
                PRIMARY KEY (item_id),

            CONSTRAINT CK_ITEM_positive_id
                CHECK (item_id > 0),

            CONSTRAINT CK_ITEM_item_name
                CHECK
                (
                    item_name COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                ),

            CONSTRAINT CK_ITEM_item_type
                CHECK
                (
                    item_type COLLATE Latin1_General_100_BIN2
                        IN (''MOVIE'', ''GAME'')
                    AND DATALENGTH(item_type) = LEN(item_type)
                )
        );

        CREATE TABLE dbo.[LOCATION]
        (
            location_id     INT IDENTITY(1, 1) NOT NULL,
            location_name   VARCHAR(100) NOT NULL,
            address         VARCHAR(255) NOT NULL,
            location_status VARCHAR(10) NOT NULL,

            CONSTRAINT PK_LOCATION
                PRIMARY KEY (location_id),

            CONSTRAINT CK_LOCATION_positive_id
                CHECK (location_id > 0),

            CONSTRAINT CK_LOCATION_location_name
                CHECK
                (
                    location_name COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                ),

            CONSTRAINT CK_LOCATION_address
                CHECK
                (
                    address COLLATE Latin1_General_100_BIN2 LIKE
                    ''%[^ '' + CHAR(9) + CHAR(10) + CHAR(11)
                    + CHAR(12) + CHAR(13) + '']%''
                )
        );

        CREATE TABLE dbo.[TRANSACTION_LOG]
        (
            transaction_id       BIGINT IDENTITY(1, 1) NOT NULL,
            employee_id          INT NOT NULL,
            item_id              INT NOT NULL,
            location_id          INT NOT NULL,
            transaction_type     VARCHAR(10) NOT NULL,
            quantity_moved       INT NOT NULL,
            transaction_datetime DATETIME2(7) NOT NULL
                CONSTRAINT DF_TRANSACTION_LOG_datetime
                DEFAULT (SYSUTCDATETIME()),
            notes                VARCHAR(255) NULL,

            CONSTRAINT PK_TRANSACTION_LOG
                PRIMARY KEY (transaction_id),

            CONSTRAINT CK_TRANSACTION_LOG_positive_id
                CHECK (transaction_id > 0),

            CONSTRAINT FK_TRANSACTION_LOG_EMPLOYEE
                FOREIGN KEY (employee_id)
                REFERENCES dbo.[EMPLOYEE] (employee_id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_TRANSACTION_LOG_ITEM
                FOREIGN KEY (item_id)
                REFERENCES dbo.[ITEM] (item_id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_TRANSACTION_LOG_LOCATION
                FOREIGN KEY (location_id)
                REFERENCES dbo.[LOCATION] (location_id)
                ON DELETE NO ACTION,

            CONSTRAINT CK_TRANSACTION_LOG_type
                CHECK
                (
                    transaction_type COLLATE Latin1_General_100_BIN2
                        IN (''CHECK IN'', ''CHECK OUT'')
                    AND DATALENGTH(transaction_type) = LEN(transaction_type)
                ),

            CONSTRAINT CK_TRANSACTION_LOG_quantity
                CHECK (quantity_moved > 0)
        );

        CREATE TABLE dbo.[CURRENT_INVENTORY]
        (
            item_id          INT NOT NULL,
            location_id      INT NOT NULL,
            quantity_on_hand INT NOT NULL
                CONSTRAINT DF_CURRENT_INVENTORY_quantity
                DEFAULT (0),
            last_updated_at  DATETIME2(7) NOT NULL
                CONSTRAINT DF_CURRENT_INVENTORY_last_updated
                DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_CURRENT_INVENTORY
                PRIMARY KEY (item_id, location_id),

            CONSTRAINT FK_CURRENT_INVENTORY_ITEM
                FOREIGN KEY (item_id)
                REFERENCES dbo.[ITEM] (item_id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_CURRENT_INVENTORY_LOCATION
                FOREIGN KEY (location_id)
                REFERENCES dbo.[LOCATION] (location_id)
                ON DELETE NO ACTION,

            CONSTRAINT CK_CURRENT_INVENTORY_quantity
                CHECK (quantity_on_hand >= 0)
        );

        COMMIT TRANSACTION;
    ';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;