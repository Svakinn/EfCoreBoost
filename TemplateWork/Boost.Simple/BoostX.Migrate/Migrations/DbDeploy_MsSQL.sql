/*
    Database deploy script (MsSQL)
    Generated: 2026-04-06 18:14:58
    ConnName: BoostXMs
*/

/*** BEGIN InitBoostXDbContext.sql ***/

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

IF SCHEMA_ID(N'BoostScemaX') IS NULL EXEC(N'CREATE SCHEMA [BoostScemaX];');
GO

CREATE TABLE [BoostScemaX].[IpInfo] (
    [Id] bigint NOT NULL IDENTITY,
    [IpNo] nvarchar(50) NOT NULL,
    [HostName] nvarchar(512) NULL,
    [Processed] bit NOT NULL,
    [LastChangedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_IpInfo] PRIMARY KEY ([Id])
);
DECLARE @description AS sql_variant;
SET @description = N'Received IP numbers with reverse lookup data';
EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'BoostScemaX', 'TABLE', N'IpInfo';
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'HostName', N'IpNo', N'LastChangedUtc', N'Processed') AND [object_id] = OBJECT_ID(N'[BoostScemaX].[IpInfo]'))
    SET IDENTITY_INSERT [BoostScemaX].[IpInfo] ON;
INSERT INTO [BoostScemaX].[IpInfo] ([Id], [HostName], [IpNo], [LastChangedUtc], [Processed])
VALUES (CAST(-1 AS bigint), N'Localhost', N'127.0.0.1', '1970-01-01T00:00:00.0000000+00:00', CAST(1 AS bit));
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'HostName', N'IpNo', N'LastChangedUtc', N'Processed') AND [object_id] = OBJECT_ID(N'[BoostScemaX].[IpInfo]'))
    SET IDENTITY_INSERT [BoostScemaX].[IpInfo] OFF;
GO

CREATE UNIQUE INDEX [IpNoIdx] ON [BoostScemaX].[IpInfo] ([IpNo]);
GO

CREATE INDEX [IX_IpInfo_LastChangedUtc] ON [BoostScemaX].[IpInfo] ([LastChangedUtc] DESC);
GO

CREATE INDEX [IX_IpInfo_Processed] ON [BoostScemaX].[IpInfo] ([Processed]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260406181455_InitBoostXDbContext', N'8.0.24');
GO

COMMIT;
GO


GO
/*** END InitBoostXDbContext.sql ***/


/*** BEGIN MsSQL.sql ***/

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
Create OR ALTER PROCEDURE [BoostScemaX].[GetIpId](@IpNo nvarchar(50)) AS
BEGIN
  SET NOCOUNT ON;
  declare @FoundId BIGINT, @processed bit, @lCh DateTime;
  set @FoundId = null;
  SELECT @FoundId = i.Id, @processed = Processed, @lCh = LastChangedUtc from [BoostScemaX].[IpInfo] i where i.IpNo = @IpNo;
  if (@FoundId is null) begin
	    insert into [BoostScemaX].[IpInfo] (IpNo,LastChangedUtc,Processed) values (@IpNo,SYSUTCDATETIME(),0);
		set @FoundId = SCOPE_IDENTITY();
  end
	-- Recheck hostname after 6 months
  else if (@processed = 1 and @lCh + 180 > SYSUTCDATETIME()) begin
    update BoostScemaX.IpInfo set Processed = 0 where Id = @FoundId;
  end
  SELECT @FoundId AS IpId;
END
GO
CREATE OR ALTER VIEW [BoostScemaX].[IpInfoView] AS
SELECT i.Id, i.IpNo, i.HostName
FROM [BoostScemaX].[IpInfo] i
GO
CREATE OR ALTER PROCEDURE [BoostScemaX].[GetIpViewByIpId] @IpId BIGINT AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    v.Id,
    v.IpNo,
    v.HostName
  FROM [BoostScemaX].[IpInfo] AS v
  WHERE v.Id = @IpId;
END
GO


GO
/*** END MsSQL.sql ***/

