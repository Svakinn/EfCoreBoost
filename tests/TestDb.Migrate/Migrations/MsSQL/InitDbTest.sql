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
IF SCHEMA_ID(N'my') IS NULL EXEC(N'CREATE SCHEMA [my];');

CREATE TABLE [my].[MyTable] (
    [Id] bigint NOT NULL IDENTITY,
    [RowVersion] bigint NOT NULL,
    [RowID] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Code] nvarchar(30) NOT NULL,
    [Heading] nvarchar(256) NULL,
    [Balance] decimal(19,4) NOT NULL,
    [Status] int NOT NULL,
    [Discount] decimal(18,8) NOT NULL,
    [LastChanged] datetime2 NOT NULL,
    [Created] datetime2 NOT NULL,
    [LastChangedBy] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_MyTable] PRIMARY KEY ([Id])
);

CREATE TABLE [my].[MyTableRef] (
    [Id] bigint NOT NULL IDENTITY,
    [RowVersion] bigint NOT NULL,
    [ParentId] bigint NOT NULL,
    [MyInfo] nvarchar(256) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Created] datetime2 NOT NULL,
    [LastChanged] datetime2 NOT NULL,
    [LastChangedBy] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_MyTableRef] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MyTableRef_MyTable_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [my].[MyTable] ([Id]) ON DELETE NO ACTION
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'Code', N'Created', N'Discount', N'Heading', N'LastChanged', N'LastChangedBy', N'RowID', N'RowVersion', N'Status') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] ON;
INSERT INTO [my].[MyTable] ([Id], [Balance], [Code], [Created], [Discount], [Heading], [LastChanged], [LastChangedBy], [RowID], [RowVersion], [Status])
VALUES (CAST(-2 AS bigint), 200.0, N'Mn', '2026-05-18T00:31:42.8482331Z', 0.0, N'Mando', '2026-05-18T00:31:42.8482331Z', N'Stefan', '222a4bbf-55ae-487b-aac3-30826a4ef31e', CAST(0 AS bigint), 2),
(CAST(-1 AS bigint), 350.0, N'BD', '2026-05-18T00:31:42.8482331Z', 5.0, N'Baldo', '2026-05-18T00:31:42.8482331Z', N'Baldr', 'd310637d-1cee-43b9-b452-b1da27f2e431', CAST(0 AS bigint), 1);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Balance', N'Code', N'Created', N'Discount', N'Heading', N'LastChanged', N'LastChangedBy', N'RowID', N'RowVersion', N'Status') AND [object_id] = OBJECT_ID(N'[my].[MyTable]'))
    SET IDENTITY_INSERT [my].[MyTable] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'Created', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId', N'RowVersion') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
    SET IDENTITY_INSERT [my].[MyTableRef] ON;
INSERT INTO [my].[MyTableRef] ([Id], [Amount], [Created], [LastChanged], [LastChangedBy], [MyInfo], [ParentId], [RowVersion])
VALUES (CAST(-3 AS bigint), 200.0, '2026-05-18T00:31:42.8482331Z', '2026-05-18T00:31:42.8482331Z', N'Stefan', N'OtherData', CAST(-2 AS bigint), CAST(0 AS bigint)),
(CAST(-2 AS bigint), 50.0, '2026-05-18T00:31:42.8482331Z', '2026-05-18T00:31:42.8482331Z', N'Baldr', N'BiggerData', CAST(-1 AS bigint), CAST(0 AS bigint)),
(CAST(-1 AS bigint), 300.0, '2026-05-18T00:31:42.8482331Z', '2026-05-18T00:31:42.8482331Z', N'Baldr', N'BigData', CAST(-1 AS bigint), CAST(0 AS bigint));
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'Created', N'LastChanged', N'LastChangedBy', N'MyInfo', N'ParentId', N'RowVersion') AND [object_id] = OBJECT_ID(N'[my].[MyTableRef]'))
    SET IDENTITY_INSERT [my].[MyTableRef] OFF;

CREATE INDEX [IX_MyTable_LastChanged] ON [my].[MyTable] ([LastChanged]);

CREATE UNIQUE INDEX [IX_MyTable_RowID] ON [my].[MyTable] ([RowID]);

CREATE INDEX [IX_MyTableRef_MyInfo] ON [my].[MyTableRef] ([MyInfo]);

CREATE INDEX [IX_MyTableRef_ParentId] ON [my].[MyTableRef] ([ParentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518003143_InitDbTest', N'10.0.8');

COMMIT;
GO

