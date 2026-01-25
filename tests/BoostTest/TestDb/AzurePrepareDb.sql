
/*  Since each test run expexts same seed data each time, we are better off dropping and */
/* recreating objecs each time the test is run.                                                 */ 
/* This bit is responsible from dropping the objects.                                           */
/* It must be performed under connection to testb, not as master, thus being seporated script.  */
DECLARE @AzureUserName sysname = N'svasure-tester';
DECLARE @DbName        sysname = N'TestDb';

-- Task 1 make sure user exists and is linked to the db:
BEGIN TRY
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @AzureUserName)    
BEGIN
    PRINT N'Creating user [' + @AzureUserName + N'] from EXTERNAL PROVIDER...';
    EXEC(N'CREATE USER [' + @AzureUserName + N'] FROM EXTERNAL PROVIDER;');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = N'db_owner'
      AND m.name = @AzureUserName
)
BEGIN
    PRINT N'Adding [' + @AzureUserName + N'] to db_owner...';
    EXEC(N'ALTER ROLE db_owner ADD MEMBER [' + @AzureUserName + N'];');
END;
END TRY
BEGIN CATCH
  PRINT N'User [' + @AzureUserName + N'] is already db_owner.';
END CATCH;

-- Task 2 cleanup previous tests :
BEGIN TRY
    -- Views first (they depend on tables)
    DROP VIEW IF EXISTS [my].[MyTableRefView];
    DROP PROCEDURE IF EXISTS [my].[ReserveMyIds];
    DROP PROCEDURE IF EXISTS [my].[GetMyTableRefViewByMyId];
    DROP PROCEDURE IF EXISTS [my].[GetMaxIdByChanger];
    DROP TABLE IF EXISTS [my].[MyTableRef];
    DROP TABLE IF EXISTS [my].[MyTable];
    DROP SEQUENCE IF EXISTS [my].[MySeq];

    -- EF migrations history
    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
        DELETE FROM [dbo].[__EFMigrationsHistory];
    -- (If yours is not dbo, adjust schema; EF defaults to dbo.)
END TRY
BEGIN CATCH
    PRINT N'Provision cleanup failed: ' + ERROR_MESSAGE();
END CATCH;


BEGIN TRY
    -- Also drop the custom objects if the DB exists
    IF EXISTS (SELECT 1 FROM sys.databases WHERE name = @DbName)
    BEGIN
        PRINT 'Database ' + @DbName + ' exists, dropping...';
        /* Cannot really drop it on AZURE LIKE THAT, drop the objects within it instead */
        IF OBJECT_ID(N'[my].[MyTableRefView]', N'V') IS NOT NULL
            DROP VIEW [my].[MyTableRefView];
        IF OBJECT_ID(N'[my].[MyTable]', N'U') IS NOT NULL
            DROP TABLE [my].[MyTable];
        IF OBJECT_ID(N'[my].[ReserveMyIds]', N'P') IS NOT NULL
            DROP PROCEDURE [my].[ReserveMyIds];
        IF OBJECT_ID(N'[my].[GetMyTableRefViewByMyId]', N'P') IS NOT NULL
            DROP PROCEDURE [my].GetMyTableRefViewByMyId;
        IF OBJECT_ID(N'[my].[GetMaxIdByChanger]', N'P') IS NOT NULL
            DROP PROCEDURE [my].GetMaxIdByChanger;
        IF OBJECT_ID(N'[my].[MyTableRef]', N'U') IS NOT NULL
            DROP TABLE [my].[MyTableRef];
        IF OBJECT_ID(N'[my].[MySeq]', N'SO') IS NOT NULL
            DROP SEQUENCE [my].[MySeq];
        IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL
            delete from [__EFMigrationsHistory]; --avoid duplicate key issues
    END
END TRY
BEGIN CATCH
    PRINT N'Provision: dropping previous objectes failed: ' + ERROR_MESSAGE();
END CATCH;