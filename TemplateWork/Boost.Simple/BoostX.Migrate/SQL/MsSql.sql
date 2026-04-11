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
    update [BoostScemaX].[IpInfo] set Processed = 0 where Id = @FoundId;
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

