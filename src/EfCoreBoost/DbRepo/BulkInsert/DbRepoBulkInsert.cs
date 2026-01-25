
// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Provider-specific BulkInsertAsync implementation (SqlServer, Postgres, MySql) + helpers.
using EfCore.Boost.DbRepo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EfCore.Boost;

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
    protected async Task BulkInsert_SqlServerAsync(List<T> items, CancellationToken ct)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema(); 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var full = schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
        var inc = ProjectScalarNonIdentityProps(); 
        var dt = ToDataTable(items, inc);
        await Ctx.Database.OpenConnectionAsync(ct);
        var conn = Ctx.Database.GetDbConnection();
        if (conn is not SqlConnection sc) throw new("SqlServer connection required");
        using var b = new SqlBulkCopy(sc) { DestinationTableName = full }; 
        foreach (DataColumn c in dt.Columns) 
            b.ColumnMappings.Add(c.ColumnName, c.ColumnName); 
        await b.WriteToServerAsync(dt);
    }

    protected void BulkInsert_SqlServerSynchronized(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema();
        var table = et.GetTableName() ?? throw new("No table name");
        var full = schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
        var inc = ProjectScalarNonIdentityProps();
        var dt = ToDataTable(items, inc);
        Ctx.Database.OpenConnection();
        var conn = Ctx.Database.GetDbConnection();
        if (conn is not SqlConnection sc) throw new("SqlServer connection required");
        using var b = new SqlBulkCopy(sc) { DestinationTableName = full };
        foreach (DataColumn c in dt.Columns)
            b.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        b.WriteToServer(dt);
    }

    // ---- PostgreSQL ----
    protected async Task BulkInsert_PostgresAsync(List<T> items, CancellationToken ct)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema() ?? "public"; 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var cols = ProjectScalarNonIdentityProps().Select(p => $"\"{p.Name}\"").ToArray(); 
        var inc = ProjectScalarNonIdentityProps();
        using var oc = await CmdHelper.OpenAsync(Ctx, ct);
        if (oc.Conn is not Npgsql.NpgsqlConnection pg) throw new("PostgreSQL connection required");
        using var w = await pg.BeginBinaryImportAsync($"COPY \"{schema}\".\"{table}\" ({string.Join(", ", cols)}) FROM STDIN (FORMAT BINARY)");
        foreach (var it in items) {
            await w.StartRowAsync(); 
            foreach (var p in inc) { 
                var raw = p.GetValue(it); 
                var val = PrepareForPostgres(raw); 
                var t = InferNpgsqlDbType(p.PropertyType); 
                if (val == null) 
                    await w.WriteNullAsync(); 
                else 
                    await w.WriteAsync(val, t);
            } 
        }
        await w.CompleteAsync();
    }
    protected void BulkInsert_PostgresSynchronized(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema() ?? "public";
        var table = et.GetTableName() ?? throw new("No table name");
        var cols = ProjectScalarNonIdentityProps().Select(p => $"\"{p.Name}\"").ToArray();
        var inc = ProjectScalarNonIdentityProps();
        Ctx.Database.OpenConnection();
        var conn = Ctx.Database.GetDbConnection();
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
    protected async Task BulkInsert_MySqlAsync(List<T> items, CancellationToken ct)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema(); 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var inc = ProjectScalarNonIdentityProps();
        await Ctx.Database.OpenConnectionAsync(ct);
        var conn = Ctx.Database.GetDbConnection();
        // Fast path if MySqlConnector is available
        if (Type.GetType("MySqlConnector.MySqlBulkCopy, MySqlConnector") is not null && conn is MySqlConnector.MySqlConnection my) { 
            var dt = ToDataTable(items, inc); 
            var dest = schema == null ? $"`{table}`" : $"`{schema}`.`{table}`"; 
            var b = new MySqlConnector.MySqlBulkCopy(my) { DestinationTableName = dest };
            for (int i = 0; i < dt.Columns.Count; i++)
                b.ColumnMappings.Add(new MySqlConnector.MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
            await b.WriteToServerAsync(dt); 
            return; 
        }
        // Fallback: chunked multi-row INSERT (parameterized)
        const int chunk = 1000; var props = inc.ToArray();
        for (int i = 0; i < items.Count; i += chunk) { 
            var batch = items.Skip(i).Take(chunk).ToList(); 
            var cols = string.Join(", ", props.Select(p => $"`{p.Name}`")); 
            var values = new List<string>(); 
            using var cmd = conn.CreateCommand(); 
            for (int r = 0; r < batch.Count; r++) { 
                var parts = new List<string>(); 
                for (int c = 0; c < props.Length; c++) { 
                    var p = props[c]; 
                    var name = $"@p_{i + r}_{c}"; 
                    parts.Add(name); 
                    var val = p.GetValue(batch[r]) ?? DBNull.Value; 
                    var prm = cmd.CreateParameter(); 
                    prm.ParameterName = name;
                    prm.Value = val; 
                    cmd.Parameters.Add(prm); 
                } 
                values.Add($"({string.Join(", ", parts)})"); 
            }
            cmd.CommandText = $"INSERT INTO {(schema == null ? $"`{table}`" : $"`{schema}`.`{table}`")} ({cols}) VALUES {string.Join(", ", values)}";
            await cmd.ExecuteNonQueryAsync(); 
        }
    }
    protected void BulkInsert_MySqlSynchronized(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found");
        var schema = et.GetSchema();
        var table = et.GetTableName() ?? throw new("No table name");
        var inc = ProjectScalarNonIdentityProps();
        Ctx.Database.OpenConnection();
        var conn = Ctx.Database.GetDbConnection();
        // Fast path if MySqlConnector is available
        if (Type.GetType("MySqlConnector.MySqlBulkCopy, MySqlConnector") is not null && conn is MySqlConnector.MySqlConnection my)
        {
            var dt = ToDataTable(items, inc);
            var dest = schema == null ? $"`{table}`" : $"`{schema}`.`{table}`";
            var b = new MySqlConnector.MySqlBulkCopy(my) { DestinationTableName = dest };
            for (int i = 0; i < dt.Columns.Count; i++)
                b.ColumnMappings.Add(new MySqlConnector.MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
            b.WriteToServer(dt);
            return;
        }
        // Fallback: chunked multi-row INSERT (parameterized)
        const int chunk = 1000; var props = inc.ToArray();
        for (int i = 0; i < items.Count; i += chunk)
        {
            var batch = items.Skip(i).Take(chunk).ToList();
            var cols = string.Join(", ", props.Select(p => $"`{p.Name}`"));
            var values = new List<string>();
            using var cmd = conn.CreateCommand();
            for (int r = 0; r < batch.Count; r++)
            {
                var parts = new List<string>();
                for (int c = 0; c < props.Length; c++)
                {
                    var p = props[c];
                    var name = $"@p_{i + r}_{c}";
                    parts.Add(name);
                    var val = p.GetValue(batch[r]) ?? DBNull.Value;
                    var prm = cmd.CreateParameter();
                    prm.ParameterName = name;
                    prm.Value = val;
                    cmd.Parameters.Add(prm);
                }
                values.Add($"({string.Join(", ", parts)})");
            }
            cmd.CommandText = $"INSERT INTO {(schema == null ? $"`{table}`" : $"`{schema}`.`{table}`")} ({cols}) VALUES {string.Join(", ", values)}";
            cmd.ExecuteNonQuery();
        }
    }

    // ---- Helpers ----
    protected static DataTable ToDataTable(List<T> items, List<PropertyInfo> inc) { 
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

    protected static bool IsScalar(Type t) { 
        t = Nullable.GetUnderlyingType(t) ?? t; 
        return t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(byte[]) || t == typeof(TimeSpan); 
    }

    protected static bool IsDbGenerated(PropertyInfo p) { 
        var a = p.GetCustomAttribute<DatabaseGeneratedAttribute>(); 
        return a?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity; 
    }

    protected static List<PropertyInfo> ProjectScalarNonIdentityProps() { 
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
