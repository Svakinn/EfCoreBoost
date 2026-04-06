SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
Create OR ALTER PROCEDURE [core].[GetIpId](@IpNo nvarchar(50)) AS
BEGIN
  SET NOCOUNT ON;
  declare @FoundId BIGINT, @processed bit, @lCh DateTime;
  set @FoundId = null;
  SELECT @FoundId = i.Id, @processed = Processed, @lCh = LastChangedUtc from core.IpInfo i where i.IpNo = @IpNo;
  if (@FoundId is null) begin
	    insert into core.IpInfo (IpNo,LastChangedUtc,Processed) values (@IpNo,SYSUTCDATETIME(),0);
		set @FoundId = SCOPE_IDENTITY();
  end
	-- Recheck hostname after 6 months
  else if (@processed = 1 and @lCh + 180 > SYSUTCDATETIME()) begin
    update core.IpInfo set Processed = 0 where Id = @FoundId;
  end
  SELECT @FoundId AS IpId;
END
GO
CREATE OR ALTER VIEW [core].[IpInfoView] AS
SELECT i.Id, i.IpNo, i.HostName
FROM [core].[IpInfo] i
GO
CREATE OR ALTER PROCEDURE [core].[GetIpViewByIpId] @IpId BIGINT AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    v.Id,
    v.IpNo,
    v.HostName
  FROM [core].[IpInfo] AS v
  WHERE v.Id = @IpId;
END
GO

