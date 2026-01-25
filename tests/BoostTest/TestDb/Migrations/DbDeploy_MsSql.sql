/* 
    Database deploy script (MsSQL)
    Generated: 2026-01-13 16:38:18
*/

/*** BEGIN InitDbTest.sql ***/

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF SCHEMA_ID(N'my') IS NULL EXEC(N'CREATE SCHEMA [my];');
GO

CREATE TABLE [my].[MyTable] (
    [Id] bigint NOT NULL IDENTITY,
    [RowID] uniqueidentifier NOT NULL,
    [LastChanged] datetimeoffset NOT NULL,
    [LastChangedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_MyTable] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [my].[MyTableRef] (
    [Id] bigint NOT NULL IDENTITY,
    [ParentId] bigint NOT NULL,
    [MyInfo] nvarchar(256) NOT NULL,
    [LastChanged] datetimeoffset NOT NULL,
    [LastChangedBy] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_MyTableRef] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MyTableRef_MyTable_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [my].[MyTable] ([Id]) ON DELETE NO ACTION
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'LastChanged', N'LastChangedBy', N'RowID') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] ON;
INSERT INTO [my].[MyTable] ([Id], [LastChanged], [LastChangedBy], [RowID])
VALUES (CAST(-2 AS bigint), '1970-01-01T00:00:00.0000000+00:00', N'Stefan', '76768a9c-2682-4ebf-ab4e-3c19ba985b4d'),
(CAST(-1 AS bigint), '1970-01-01T00:00:00.0000000+00:00', N'Baldr', 'f8c846fb-fa55-481c-bf42-9d67a1c334cb');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'LastChanged', N'LastChangedBy', N'RowID') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
    SET IDENTITY_INSERT [my].[MyTableRef] ON;
INSERT INTO [my].[MyTableRef] ([Id], [LastChanged], [LastChangedBy], [MyInfo], [ParentId])
VALUES (CAST(-3 AS bigint), '1970-01-01T00:00:00.0000000+00:00', N'Stefan', N'OtherData', CAST(-2 AS bigint)),
(CAST(-2 AS bigint), '1970-01-01T00:00:00.0000000+00:00', N'Baldr', N'BiggerData', CAST(-1 AS bigint)),
(CAST(-1 AS bigint), '1970-01-01T00:00:00.0000000+00:00', N'Baldr', N'BigData', CAST(-1 AS bigint));
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
    SET IDENTITY_INSERT [my].[MyTableRef] OFF;
GO

CREATE INDEX [IX_MyTable_LastChanged] ON [my].[MyTable] ([LastChanged]);
GO

CREATE UNIQUE INDEX [IX_MyTable_RowID] ON [my].[MyTable] ([RowID]);
GO

CREATE INDEX [IX_MyTableRef_MyInfo] ON [my].[MyTableRef] ([MyInfo]);
GO

CREATE INDEX [IX_MyTableRef_ParentId] ON [my].[MyTableRef] ([ParentId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260113163814_InitDbTest', N'8.0.21');
GO

COMMIT;
GO


GO
/*** END InitDbTest.sql ***/


/*** BEGIN MsSQL.sql ***/

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF OBJECT_ID(N'[my].[MySeq]', N'SO') IS NULL
BEGIN
CREATE SEQUENCE [my].[MySeq] AS [bigint]
 START WITH 1
 INCREMENT BY 1
 CACHE 
END
go
--Using the sequence example via sp, 
--Returning list of reserved iems from the sequence
--We will mapp this one by hand into the UOW
CREATE or alter PROCEDURE [my].[ReserveMyIds](@IdCount INT) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @StartVariant sql_variant;
    DECLARE @StartBase BIGINT;
    -- Reserve from seq by @IdCount
    EXEC sys.sp_sequence_get_range
        @sequence_name = N'[my].[MySeq]',
        @range_size = @IdCount,
        @range_first_value = @StartVariant OUTPUT;
    SET @StartBase = CONVERT(BIGINT, @StartVariant);
    -- Return all the reserved ids
    WITH nums AS (
        SELECT 0 AS i
        UNION ALL
        SELECT i + 1 FROM nums WHERE i + 1 < @IdCount
    )
    SELECT @StartBase + i AS ReservedId FROM nums
    OPTION (MAXRECURSION 0);
END
go
--Stored procedure test scalar result
Create PROCEDURE [my].[GetMaxIdByChanger](@Changer nvarchar(50)) AS
BEGIN
	SET NOCOUNT ON;
    SELECT max(Id) from my.MyTable where LastChangedBy = @Changer;
END
go
--View example
--Part of the entityset, defined by the ViewKey attribute: [ViewKey(nameof(RefId), nameof(MyId))]
CREATE OR ALTER VIEW my.MyTableRefView AS
SELECT
    r.Id AS RefId,
    t.Id AS MyId,
    t.RowID,
    t.LastChanged,
    t.LastChangedBy,
    r.MyInfo,
    r.LastChanged AS RefLastChanged,
    r.LastChangedBy AS RefLastChangedBy
FROM my.MyTableRef r
INNER JOIN my.MyTable t ON t.Id = r.ParentId;
GO
--Stored procedure to get data from the view by MyId
CREATE OR ALTER PROCEDURE [my].[GetMyTableRefViewByMyId] @MyId BIGINT AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        v.RefId,
        v.MyId,
        v.RowID,
        v.LastChanged,
        v.LastChangedBy,
        v.MyInfo,
        v.RefLastChanged,
        v.RefLastChangedBy
    FROM my.MyTableRefView AS v
    WHERE v.MyId = @MyId;
END
GO

GO
/*** END MsSQL.sql ***/

