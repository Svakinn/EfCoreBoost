
// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Provider-specific BulkInsertAsync implementation (SqlServer, Postgres, MySql) + helpers.
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;


// ReSharper disable once CheckNamespace
namespace EfCore.Boost.DbRepo;

public partial class EfRepo<T> where T : class
{
    protected async Task BulkInsertPartialAsync(List<T> items, CancellationToken ct)
    {
        switch (DbType) {
            case DatabaseType.SqlServer:
                await BulkInsert_SqlServerAsync(items, ct); break;
            case DatabaseType.PostgreSql:
                await BulkInsert_PostgresAsync(items, ct); break;
            case DatabaseType.MySql:
                await BulkInsert_MySqlAsync(items, ct); break;
            default:
                throw new NotSupportedException($"BulkInsertAsync not supported for {DbType}");
        }
    }

    protected void BulkInsertPartialSynchronized(List<T> items)
    {
        switch (DbType)
        {
            case DatabaseType.SqlServer:
                BulkInsert_SqlServerSynchronized(items); break;
            case DatabaseType.PostgreSql:
                BulkInsert_PostgresSynchronized(items); break;
            case DatabaseType.MySql:
                BulkInsert_MySqlSynchronized(items); break;
            default:
                throw new NotSupportedException($"BulkInsertAsync not supported for {DbType}");
        }
    }

    // ---- SQL Server ----
    private async Task BulkInsert_SqlServerAsync(List<T> items, CancellationToken ct)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema();
        var table = et.GetTableName() ?? throw new("No table name");
        var full = schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
        var inc = ProjectScalarNonIdentityProps();
        var dt = ToDataTable(items, inc);
        var conn = await CmdHelper.OpenConnectionAsync(Ctx, ct); //The DbContexts connection, correctly opened
        if (conn is not SqlConnection sc) throw new("SqlServer connection required");
        using var b = new SqlBulkCopy(sc);
        b.DestinationTableName = full;
        foreach (DataColumn c in dt.Columns)
            b.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await b.WriteToServerAsync(dt,ct);
    }

    private void BulkInsert_SqlServerSynchronized(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema();
        var table = et.GetTableName() ?? throw new("No table name");
        var full = schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
        var inc = ProjectScalarNonIdentityProps();
        var dt = ToDataTable(items, inc);
        var conn = CmdHelper.OpenConnectionSynchronized(Ctx);
        if (conn is not SqlConnection sc) throw new("SqlServer connection required");
        using var b = new SqlBulkCopy(sc);
        b.DestinationTableName = full;
        foreach (DataColumn c in dt.Columns)
            b.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        b.WriteToServer(dt);
    }

    // ---- PostgreSQL ----
    private async Task BulkInsert_PostgresAsync(List<T> items, CancellationToken ct)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema() ?? "public";
        var table = et.GetTableName() ?? throw new("No table name");
        var cols = ProjectScalarNonIdentityProps().Select(p => $"\"{p.Name}\"").ToArray();
        var inc = ProjectScalarNonIdentityProps();
        var conn = await CmdHelper.OpenConnectionAsync(Ctx, ct);
        if (conn is not Npgsql.NpgsqlConnection pg) throw new("PostgreSQL connection required");
        await using var w = await pg.BeginBinaryImportAsync($"COPY \"{schema}\".\"{table}\" ({string.Join(", ", cols)}) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var it in items) {
            await w.StartRowAsync(ct);
            foreach (var p in inc) {
                var raw = p.GetValue(it);
                var val = PrepareForPostgres(raw);
                var t = InferNpgsqlDbType(p.PropertyType);
                if (val == null)
                    await w.WriteNullAsync(ct);
                else
                    await w.WriteAsync(val, t, ct);
            }
        }
        await w.CompleteAsync(ct);
    }

    private void BulkInsert_PostgresSynchronized(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema() ?? "public";
        var table = et.GetTableName() ?? throw new("No table name");
        var cols = ProjectScalarNonIdentityProps().Select(p => $"\"{p.Name}\"").ToArray();
        var inc = ProjectScalarNonIdentityProps();
        var conn = CmdHelper.OpenConnectionSynchronized(Ctx);
        if (conn is not Npgsql.NpgsqlConnection pg) throw new("PostgreSQL connection required");
        using var w = pg.BeginBinaryImport($"COPY \"{schema}\".\"{table}\" ({string.Join(", ", cols)}) FROM STDIN (FORMAT BINARY)");
        foreach (var it in items)
        {
            w.StartRow();
            foreach (var p in inc)
            {
                var raw = p.GetValue(it);
                var val = PrepareForPostgres(raw);
                var t = InferNpgsqlDbType(p.PropertyType);
                if (val == null)
                    w.WriteNull();
                else
                    w.Write(val, t);
            }
        }
        w.Complete();
    }

    // ---- MySQL ----
    private async Task BulkInsert_MySqlAsync(List<T> items, CancellationToken ct)
    {
        throw new NotImplementedException("MySQL not yet supported for .NET10 EfCore drivers !");
    }

    private void BulkInsert_MySqlSynchronized(List<T> items)
    {
        throw new NotImplementedException("MySQL not yet supported for .NET10 EfCore drivers !");
    }

    // ---- Helpers ----
    private static DataTable ToDataTable(List<T> items, List<PropertyInfo> inc) {
        var dt = new DataTable();
        foreach (var p in inc)
            dt.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);
        foreach (var it in items) {
            var vals = new object[inc.Count];
            for (int i = 0; i < inc.Count; i++) vals[i] = inc[i].GetValue(it) ?? DBNull.Value;
            dt.Rows.Add(vals);
        }
        return dt;
    }

    private static bool IsScalar(Type t) {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(byte[]) || t == typeof(TimeSpan);
    }

    private static bool IsDbGenerated(PropertyInfo p) {
        var a = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
        return a?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
    }

    private static List<PropertyInfo> ProjectScalarNonIdentityProps() {
        var list = new List<PropertyInfo>();
        foreach (var p in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            if (IsDbGenerated(p))
                continue;
            if (!IsScalar(p.PropertyType))
                continue;
            list.Add(p);
        } return list;
    }
}
