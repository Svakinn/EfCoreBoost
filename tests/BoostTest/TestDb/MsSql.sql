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
