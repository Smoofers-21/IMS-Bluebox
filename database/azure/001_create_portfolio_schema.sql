-- Fresh Azure SQL / SQL Server schema installer. Creates no database and seeds no data.
-- Replace YourAzureDatabaseName below with the database selected in Azure Query editor.
-- Preserves reference tables/constraints and adds four portfolio performance indexes.
-- Not an upgrade for existing tables; use 002_add_portfolio_indexes.sql instead.
-- Execute the whole file together; no USE statement or GO separators are required.
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
    THROW 51000, 'Set @ExpectedDatabase to the exact selected Azure database name before running this installer.', 1;
IF @@TRANCOUNT <> 0
    THROW 51001, 'Run this installer in a fresh query session without an existing transaction.', 1;
IF EXISTS (
    SELECT 1 FROM sys.objects AS o
    INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
    WHERE s.name = N'dbo'
      AND o.name IN (N'EMPLOYEE', N'ITEM', N'LOCATION', N'CURRENT_INVENTORY', N'TRANSACTION_LOG')
)
    THROW 51002, 'A BlueBox object already exists. This installer requires a fresh schema; do not drop existing tables to rerun it.', 1;

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

CREATE TABLE [dbo].[CURRENT_INVENTORY](
[item_id] [int] NOT NULL,
[location_id] [int] NOT NULL,
[quantity_on_hand] [int] NOT NULL,
[last_updated_at] [datetime2](7) NOT NULL,
CONSTRAINT [PK_CURRENT_INVENTORY] PRIMARY KEY CLUSTERED
(
[item_id] ASC,
[location_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[EMPLOYEE](
[employee_id] [int] IDENTITY(1,1) NOT NULL,
[first_name] [varchar](50) NOT NULL,
[last_name] [varchar](50) NOT NULL,
[username] [varchar](50) NOT NULL,
[password_hash] [varchar](255) NOT NULL,
[access_role] [varchar](20) NOT NULL,
[employment_status] [varchar](20) NOT NULL,
CONSTRAINT [PK_EMPLOYEE] PRIMARY KEY CLUSTERED
(
[employee_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
CONSTRAINT [UQ_EMPLOYEE_username] UNIQUE NONCLUSTERED
(
[username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[ITEM](
[item_id] [int] IDENTITY(1,1) NOT NULL,
[item_name] [varchar](150) NOT NULL,
[item_type] [varchar](10) NOT NULL,
[media_format] [varchar](30) NOT NULL,
[poster_url] [nvarchar](2048) NULL,
[age_rating] [varchar](20) NULL,
[item_status] [varchar](10) NOT NULL,
CONSTRAINT [PK_ITEM] PRIMARY KEY CLUSTERED
(
[item_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[LOCATION](
[location_id] [int] IDENTITY(1,1) NOT NULL,
[location_name] [varchar](100) NOT NULL,
[address] [varchar](255) NOT NULL,
[location_status] [varchar](10) NOT NULL,
CONSTRAINT [PK_LOCATION] PRIMARY KEY CLUSTERED
(
[location_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[TRANSACTION_LOG](
[transaction_id] [bigint] IDENTITY(1,1) NOT NULL,
[employee_id] [int] NOT NULL,
[item_id] [int] NOT NULL,
[location_id] [int] NOT NULL,
[transaction_type] [varchar](10) NOT NULL,
[quantity_moved] [int] NOT NULL,
[transaction_datetime] [datetime2](7) NOT NULL,
[notes] [varchar](255) NULL,
[corrects_transaction_id] [bigint] NULL,
CONSTRAINT [PK_TRANSACTION_LOG] PRIMARY KEY CLUSTERED
(
[transaction_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

ALTER TABLE [dbo].[CURRENT_INVENTORY] ADD CONSTRAINT [DF_CURRENT_INVENTORY_quantity] DEFAULT ((0)) FOR [quantity_on_hand];

ALTER TABLE [dbo].[CURRENT_INVENTORY] ADD CONSTRAINT [DF_CURRENT_INVENTORY_last_updated] DEFAULT (sysutcdatetime()) FOR [last_updated_at];

ALTER TABLE [dbo].[ITEM] ADD CONSTRAINT [DF_ITEM_status] DEFAULT ('ACTIVE') FOR [item_status];

ALTER TABLE [dbo].[TRANSACTION_LOG] ADD CONSTRAINT [DF_TRANSACTION_LOG_datetime] DEFAULT (sysutcdatetime()) FOR [transaction_datetime];

ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [FK_CURRENT_INVENTORY_ITEM] FOREIGN KEY([item_id])
REFERENCES [dbo].[ITEM] ([item_id]);

ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [FK_CURRENT_INVENTORY_ITEM];

ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [FK_CURRENT_INVENTORY_LOCATION] FOREIGN KEY([location_id])
REFERENCES [dbo].[LOCATION] ([location_id]);

ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [FK_CURRENT_INVENTORY_LOCATION];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_correction] FOREIGN KEY([corrects_transaction_id])
REFERENCES [dbo].[TRANSACTION_LOG] ([transaction_id]);

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_correction];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_EMPLOYEE] FOREIGN KEY([employee_id])
REFERENCES [dbo].[EMPLOYEE] ([employee_id]);

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_EMPLOYEE];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_ITEM] FOREIGN KEY([item_id])
REFERENCES [dbo].[ITEM] ([item_id]);

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_ITEM];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_LOCATION] FOREIGN KEY([location_id])
REFERENCES [dbo].[LOCATION] ([location_id]);

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_LOCATION];

ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [CK_CURRENT_INVENTORY_quantity] CHECK (([quantity_on_hand]>=(0)));

ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [CK_CURRENT_INVENTORY_quantity];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_access_role] CHECK (((([access_role]) collate Latin1_General_100_BIN2='MANAGER' OR ([access_role]) collate Latin1_General_100_BIN2='STAFF') AND datalength([access_role])=len([access_role])));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_access_role];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_employment_status] CHECK (((([employment_status]) collate Latin1_General_100_BIN2='TERMINATED' OR ([employment_status]) collate Latin1_General_100_BIN2='INACTIVE' OR ([employment_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([employment_status])=len([employment_status])));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_employment_status];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_first_name] CHECK ((([first_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_first_name];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_last_name] CHECK ((([last_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_last_name];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_positive_id] CHECK (([employee_id]>(0)));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_positive_id];

ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_username] CHECK ((([username]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_username];

ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_item_name] CHECK ((([item_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_item_name];

ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_item_type] CHECK (((([item_type]) collate Latin1_General_100_BIN2='GAME' OR ([item_type]) collate Latin1_General_100_BIN2='MOVIE') AND datalength([item_type])=len([item_type])));

ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_item_type];

ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_media_format] CHECK ((([media_format]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_media_format];

ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_positive_id] CHECK (([item_id]>(0)));

ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_positive_id];

ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_status] CHECK (((([item_status]) collate Latin1_General_100_BIN2='ARCHIVED' OR ([item_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([item_status])=len([item_status])));

ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_status];

ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_address] CHECK ((([address]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_address];

ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_location_name] CHECK ((([location_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_location_name];

ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_positive_id] CHECK (([location_id]>(0)));

ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_positive_id];

ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_status] CHECK (((([location_status]) collate Latin1_General_100_BIN2='INACTIVE' OR ([location_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([location_status])=len([location_status])));

ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_status];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_correction_details] CHECK (([corrects_transaction_id] IS NULL OR [corrects_transaction_id]<>[transaction_id] AND [notes] IS NOT NULL AND ([notes]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'));

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_correction_details];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_positive_id] CHECK (([transaction_id]>(0)));

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_positive_id];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_quantity] CHECK (([quantity_moved]>(0)));

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_quantity];

ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_type] CHECK (((([transaction_type]) collate Latin1_General_100_BIN2='CHECK OUT' OR ([transaction_type]) collate Latin1_General_100_BIN2='CHECK IN') AND datalength([transaction_type])=len([transaction_type])));

ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_type];

CREATE INDEX IX_TRANSACTION_LOG_corrects_transaction_id
    ON dbo.TRANSACTION_LOG (corrects_transaction_id) WHERE corrects_transaction_id IS NOT NULL;
CREATE INDEX IX_TRANSACTION_LOG_item_id ON dbo.TRANSACTION_LOG (item_id);
CREATE INDEX IX_TRANSACTION_LOG_location_id ON dbo.TRANSACTION_LOG (location_id);
CREATE INDEX IX_CURRENT_INVENTORY_location ON dbo.CURRENT_INVENTORY (location_id) INCLUDE (quantity_on_hand);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT DB_NAME() AS database_name, N'BlueBox schema created; tables are empty.' AS result;
