-- REFERENCE SNAPSHOT: creates tables in an EMPTY database only.
-- Do not execute against the existing teacher database.
-- Supplied table definition (sanitized reference).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
) ON [PRIMARY]
GO
-- Supplied table definition (sanitized reference).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
) ON [PRIMARY]
GO
-- Supplied table definition (sanitized reference).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
) ON [PRIMARY]
GO
-- Supplied table definition (sanitized reference).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LOCATION](
[location_id] [int] IDENTITY(1,1) NOT NULL,
[location_name] [varchar](100) NOT NULL,
[address] [varchar](255) NOT NULL,
[location_status] [varchar](10) NOT NULL,
CONSTRAINT [PK_LOCATION] PRIMARY KEY CLUSTERED
(
[location_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
-- Supplied table definition (sanitized reference).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] ADD CONSTRAINT [DF_CURRENT_INVENTORY_quantity] DEFAULT ((0)) FOR [quantity_on_hand]
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] ADD CONSTRAINT [DF_CURRENT_INVENTORY_last_updated] DEFAULT (sysutcdatetime()) FOR [last_updated_at]
GO
ALTER TABLE [dbo].[ITEM] ADD CONSTRAINT [DF_ITEM_status] DEFAULT ('ACTIVE') FOR [item_status]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] ADD CONSTRAINT [DF_TRANSACTION_LOG_datetime] DEFAULT (sysutcdatetime()) FOR [transaction_datetime]
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [FK_CURRENT_INVENTORY_ITEM] FOREIGN KEY([item_id])
REFERENCES [dbo].[ITEM] ([item_id])
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [FK_CURRENT_INVENTORY_ITEM]
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [FK_CURRENT_INVENTORY_LOCATION] FOREIGN KEY([location_id])
REFERENCES [dbo].[LOCATION] ([location_id])
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [FK_CURRENT_INVENTORY_LOCATION]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_correction] FOREIGN KEY([corrects_transaction_id])
REFERENCES [dbo].[TRANSACTION_LOG] ([transaction_id])
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_correction]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_EMPLOYEE] FOREIGN KEY([employee_id])
REFERENCES [dbo].[EMPLOYEE] ([employee_id])
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_EMPLOYEE]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_ITEM] FOREIGN KEY([item_id])
REFERENCES [dbo].[ITEM] ([item_id])
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_ITEM]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [FK_TRANSACTION_LOG_LOCATION] FOREIGN KEY([location_id])
REFERENCES [dbo].[LOCATION] ([location_id])
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [FK_TRANSACTION_LOG_LOCATION]
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] WITH CHECK ADD CONSTRAINT [CK_CURRENT_INVENTORY_quantity] CHECK (([quantity_on_hand]>=(0)))
GO
ALTER TABLE [dbo].[CURRENT_INVENTORY] CHECK CONSTRAINT [CK_CURRENT_INVENTORY_quantity]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_access_role] CHECK (((([access_role]) collate Latin1_General_100_BIN2='MANAGER' OR ([access_role]) collate Latin1_General_100_BIN2='STAFF') AND datalength([access_role])=len([access_role])))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_access_role]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_employment_status] CHECK (((([employment_status]) collate Latin1_General_100_BIN2='TERMINATED' OR ([employment_status]) collate Latin1_General_100_BIN2='INACTIVE' OR ([employment_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([employment_status])=len([employment_status])))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_employment_status]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_first_name] CHECK ((([first_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_first_name]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_last_name] CHECK ((([last_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_last_name]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_positive_id] CHECK (([employee_id]>(0)))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_positive_id]
GO
ALTER TABLE [dbo].[EMPLOYEE] WITH CHECK ADD CONSTRAINT [CK_EMPLOYEE_username] CHECK ((([username]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[EMPLOYEE] CHECK CONSTRAINT [CK_EMPLOYEE_username]
GO
ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_item_name] CHECK ((([item_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_item_name]
GO
ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_item_type] CHECK (((([item_type]) collate Latin1_General_100_BIN2='GAME' OR ([item_type]) collate Latin1_General_100_BIN2='MOVIE') AND datalength([item_type])=len([item_type])))
GO
ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_item_type]
GO
ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_media_format] CHECK ((([media_format]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_media_format]
GO
ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_positive_id] CHECK (([item_id]>(0)))
GO
ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_positive_id]
GO
ALTER TABLE [dbo].[ITEM] WITH CHECK ADD CONSTRAINT [CK_ITEM_status] CHECK (((([item_status]) collate Latin1_General_100_BIN2='ARCHIVED' OR ([item_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([item_status])=len([item_status])))
GO
ALTER TABLE [dbo].[ITEM] CHECK CONSTRAINT [CK_ITEM_status]
GO
ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_address] CHECK ((([address]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_address]
GO
ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_location_name] CHECK ((([location_name]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_location_name]
GO
ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_positive_id] CHECK (([location_id]>(0)))
GO
ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_positive_id]
GO
ALTER TABLE [dbo].[LOCATION] WITH CHECK ADD CONSTRAINT [CK_LOCATION_status] CHECK (((([location_status]) collate Latin1_General_100_BIN2='INACTIVE' OR ([location_status]) collate Latin1_General_100_BIN2='ACTIVE') AND datalength([location_status])=len([location_status])))
GO
ALTER TABLE [dbo].[LOCATION] CHECK CONSTRAINT [CK_LOCATION_status]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_correction_details] CHECK (([corrects_transaction_id] IS NULL OR [corrects_transaction_id]<>[transaction_id] AND [notes] IS NOT NULL AND ([notes]) collate Latin1_General_100_BIN2 like ((((('%[^ '+char((9)))+char((10)))+char((11)))+char((12)))+char((13)))+']%'))
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_correction_details]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_positive_id] CHECK (([transaction_id]>(0)))
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_positive_id]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_quantity] CHECK (([quantity_moved]>(0)))
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_quantity]
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] WITH CHECK ADD CONSTRAINT [CK_TRANSACTION_LOG_type] CHECK (((([transaction_type]) collate Latin1_General_100_BIN2='CHECK OUT' OR ([transaction_type]) collate Latin1_General_100_BIN2='CHECK IN') AND datalength([transaction_type])=len([transaction_type])))
GO
ALTER TABLE [dbo].[TRANSACTION_LOG] CHECK CONSTRAINT [CK_TRANSACTION_LOG_type]
GO
