DECLARE @DbName sysname = N'TestDb';
DECLARE @PreferredCollation sysname = N'Latin1_General_100_CI_AS_SC_UTF8'; -- Prefer UTF-8 collation (SQL Server 2019+) excelent for cross language collation
DECLARE @FallBackCollation sysname =  N'Icelandic_100_CI_AS'; --Not excelent chouse for all Europe - replace this with jour own culture ?
DECLARE @EmergencyFallBackCollation sysname =  N'Latin1_General_100_CI_AS'; --Just in case - very poor colation score for icelandic i.e. Æ and Þ are messy
DECLARE @Collation sysname;
IF EXISTS (SELECT 1 FROM sys.fn_helpcollations() WHERE name = @PreferredCollation)
BEGIN
    SET @Collation = @PreferredCollation;
END
ELSE IF EXISTS (SELECT 1 FROM sys.fn_helpcollations() WHERE name =@FallBackCollation)
BEGIN
    -- Fallback: (Unicode, non-UTF8)
    SET @Collation = @FallBackCollation;
END
ELSE
BEGIN
    SET @Collation = @EmergencyFallBackCollation;  --Last resort
END;
IF DB_ID(@DbName) IS NULL
BEGIN
    PRINT 'Creating database ' + @DbName + ' with collation ' + @Collation + '...';
    DECLARE @Sql nvarchar(max) = N'
        CREATE DATABASE [' + @DbName + N']
        COLLATE ' + @Collation + N';
        -- Optional: file layout
        -- ON PRIMARY (
        --     NAME = N''' + @DbName + ''',
        --     FILENAME = ''D:\SqlData\' + @DbName + '.mdf'',
        --     SIZE = 64MB,
        --     FILEGROWTH = 64MB
        -- )
        -- LOG ON (
        --     NAME = N''' + @DbName + '_log'',
        --     FILENAME = ''E:\SqlData\' + @DbName + '_log.ldf'',
        --     SIZE = 64MB,
        --     FILEGROWTH = 64MB
        -- );
    ';
    EXEC (@Sql);
END
ELSE
BEGIN
    PRINT 'Database ' + @DbName + ' already exists.';
END;