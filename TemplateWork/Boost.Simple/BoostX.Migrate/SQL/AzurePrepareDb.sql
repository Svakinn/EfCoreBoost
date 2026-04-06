
/*  Since each test run expexts same seed data each time, we are better off dropping and */
/* recreating objecs each time the test is run.                                                 */ 
/* This bit is responsible from dropping the objects.                                           */
/* It must be performed under connection to testb, not as master, thus being seporated script.  */
DECLARE @AzureUserName sysname = N'Core';
DECLARE @DbName        sysname = N'BoostXDb';

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