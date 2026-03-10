/* This is an example on how you could run script to create the test db on azure named TestDb with master user named svasure-tester */
/* It is NOT run by the test, you need to have this DB up and running before running the tests  */
DECLARE @AzureUserName sysname = N'svasure-tester';  -- AAD app or user display name
DECLARE @DbName        sysname = N'TestDb';

DECLARE @PreferredCollation           sysname = N'Latin1_General_100_CI_AS_SC_UTF8'; -- UTF-8, good across Europe
DECLARE @FallBackCollation            sysname = N'Icelandic_100_CI_AS';
DECLARE @EmergencyFallBackCollation   sysname = N'Latin1_General_100_CI_AS';
DECLARE @Collation                    sysname;
declare @dbNamePre                    sysname = '[' + @DbName + '].';

BEGIN TRY
/* creata our desired db-user if missing */
IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals WHERE name = @AzureUserName
)
BEGIN
    PRINT 'Creating master user for [' + @AzureUserName + ']...';
    EXEC ('CREATE USER [' + @AzureUserName + '] FROM EXTERNAL PROVIDER;');
END
ELSE
BEGIN
    PRINT 'Master user [' + @AzureUserName + '] already exists.';
END;
END TRY
BEGIN CATCH
    PRINT N'Provision: CREATE Master user '+@AzureUserName+N' skipped/failed: ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = N'dbmanager'
      AND m.name = @AzureUserName
)
BEGIN
    PRINT 'Adding [' + @AzureUserName + '] to dbmanager in master...';
    EXEC('ALTER ROLE dbmanager ADD MEMBER ['+@AzureUserName+'];')
END
ELSE
BEGIN
    PRINT 'User [' + @AzureUserName + '] already in dbmanager.';
END;
END TRY
BEGIN CATCH
    PRINT N'Provision: master AAD user/role skipped/failed: ' + ERROR_MESSAGE();
END CATCH;

/* Pick best available collation */
IF EXISTS (SELECT 1 FROM sys.fn_helpcollations() WHERE name = @PreferredCollation)
    SET @Collation = @PreferredCollation;
ELSE IF EXISTS (SELECT 1 FROM sys.fn_helpcollations() WHERE name = @FallBackCollation)
    SET @Collation = @FallBackCollation;
ELSE
    SET @Collation = @EmergencyFallBackCollation;
print 'Collation '+@Collation+' selected.'

BEGIN TRY
/* Create DB if missing */
IF Not EXISTS (SELECT 1 FROM sys.databases WHERE name = @DbName)
BEGIN
    PRINT 'Creating database ' + @DbName + ' with collation ' + @Collation + '...';

    DECLARE @CreateSql nvarchar(max) = N'
        CREATE DATABASE [' + @DbName + N']
        COLLATE ' + @Collation + N';
    ';
    EXEC (@CreateSql);
END
ELSE
BEGIN
    PRINT 'Database ' + @DbName + ' already exists.';
END;
END TRY
BEGIN CATCH
    PRINT N'Provision: CREATE DATABASE skipped/failed: ' + ERROR_MESSAGE();
END CATCH;
