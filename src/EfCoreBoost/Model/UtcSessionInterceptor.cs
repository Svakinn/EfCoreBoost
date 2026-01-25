// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Ensures the database session time zone is set to UTC when using PostgreSQL or MySQL.
///
/// Why:
/// - In PostgreSQL, "timestamp with time zone" (timestamptz) values are stored in UTC,
///   but input and output are automatically converted to the session time zone.
///   Without forcing the session to UTC, timestamps inserted as UTC from .NET may
///   be returned shifted to a different zone if the server/session is not UTC.
///
/// - In MySQL, "datetime" values do not store an offset, and their interpretation can
///   vary depending on the session time zone. This can cause data inserted as UTC
///   to be shifted or interpreted incorrectly when read back.
///
/// SQL Server:
/// - datetime2 and datetimeoffset do not apply implicit timezone conversions, so this
///   interceptor performs no changes on SQL Server connections.
///
/// Usage:
/// - Supports a consistent application-wide rule where DateTimeOffset values in .NET
///   represent instants in time and are always stored and retrieved as UTC, regardless
///   of database provider or server configuration.
/// </summary>
public sealed class UtcSessionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {                
        var typeName = connection.GetType().FullName ?? string.Empty;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) 
        {
            // PostgreSQL: force session timezone to UTC
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SET TIME ZONE 'UTC';";
            cmd.ExecuteNonQuery();
        }
        else if (typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase) || typeName.Contains("MariaDb", StringComparison.OrdinalIgnoreCase))
        {
            // MySQL/MariaDB: force session timezone to UTC
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SET time_zone = '+00:00';";
            cmd.ExecuteNonQuery();
        }
    }


    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {        
        var typeName = connection.GetType().FullName ?? string.Empty;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            // PostgreSQL: force session timezone to UTC
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SET TIME ZONE 'UTC';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase) || typeName.Contains("MariaDb", StringComparison.OrdinalIgnoreCase))
        {
            // MySQL/MariaDB: force session timezone to UTC
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SET time_zone = '+00:00';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}