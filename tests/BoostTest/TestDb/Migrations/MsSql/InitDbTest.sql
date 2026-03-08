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
    [RowVersion] bigint NOT NULL,
    [RowID] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Heading] nvarchar(256) NULL,
    [Balance] decimal(19,4) NOT NULL,
    [Status] int NOT NULL,
    [Discount] decimal(18,8) NOT NULL,
    [LastChanged] datetimeoffset NOT NULL,
    [Created] datetimeoffset NOT NULL,
    [LastChangedBy] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_MyTable] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [my].[MyTableRef] (
    [Id] bigint NOT NULL IDENTITY,
    [RowVersion] bigint NOT NULL,
    [ParentId] bigint NOT NULL,
    [MyInfo] nvarchar(256) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Created] datetimeoffset NOT NULL,
    [LastChanged] datetimeoffset NOT NULL,
    [LastChangedBy] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_MyTableRef] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MyTableRef_MyTable_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [my].[MyTable] ([Id]) ON DELETE NO ACTION
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'Code', N'Created', N'Discount', N'Heading', N'LastChanged', N'LastChangedBy', N'RowID', N'RowVersion', N'Status') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] ON;
INSERT INTO [my].[MyTable] ([Id], [Balance], [Code], [Created], [Discount], [Heading], [LastChanged], [LastChangedBy], [RowID], [RowVersion], [Status])
VALUES (CAST(-2 AS bigint), 200.0, N'Mn', '2026-03-08T13:07:57.5537753+00:00', 0.0, N'Mando', '2026-03-08T13:07:57.5537752+00:00', N'Stefan', '0dbf85d5-7e18-4c47-baf9-793f34448533', CAST(0 AS bigint), 2),
(CAST(-1 AS bigint), 350.0, N'BD', '2026-03-08T13:07:57.5537721+00:00', 5.0, N'Baldo', '2026-03-08T13:07:57.5537719+00:00', N'Baldr', '896c798d-118f-42d5-8e8a-e2af6d4109fb', CAST(0 AS bigint), 1);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'Code', N'Created', N'Discount', N'Heading', N'LastChanged', N'LastChangedBy', N'RowID', N'RowVersion', N'Status') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'Created', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId', N'RowVersion') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
    SET IDENTITY_INSERT [my].[MyTableRef] ON;
INSERT INTO [my].[MyTableRef] ([Id], [Amount], [Created], [LastChanged], [LastChangedBy], [MyInfo], [ParentId], [RowVersion])
VALUES (CAST(-3 AS bigint), 200.0, '2026-03-08T13:07:57.5537850+00:00', '2026-03-08T13:07:57.5537850+00:00', N'Stefan', N'OtherData', CAST(-2 AS bigint), CAST(0 AS bigint)),
(CAST(-2 AS bigint), 50.0, '2026-03-08T13:07:57.5537848+00:00', '2026-03-08T13:07:57.5537849+00:00', N'Baldr', N'BiggerData', CAST(-1 AS bigint), CAST(0 AS bigint)),
(CAST(-1 AS bigint), 300.0, '2026-03-08T13:07:57.5537845+00:00', '2026-03-08T13:07:57.5537846+00:00', N'Baldr', N'BigData', CAST(-1 AS bigint), CAST(0 AS bigint));
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'Created', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId', N'RowVersion') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
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
VALUES (N'20260308130757_InitDbTest', N'8.0.24');
GO

COMMIT;
GO

