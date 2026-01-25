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

