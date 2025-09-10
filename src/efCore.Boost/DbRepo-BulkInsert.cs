// Provider-specific BulkInsert implementation (SqlServer, Postgres, MySql) + helpers.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NpgsqlTypes;

namespace DbRepo;

public partial class EfRepo<T> where T : class
{
    protected async Task BulkInsertPartialAsync(List<T> items)
    { 
        switch (DbType) { 
            case DatabaseType.SqlServer: 
                await BulkInsert_SqlServer(items); break; 
            case DatabaseType.PostgreSql: 
                await BulkInsert_Postgres(items); break; 
            case DatabaseType.MySql: 
                await BulkInsert_MySql(items); break; 
            default: 
                throw new NotSupportedException($"BulkInsert not supported for {DbType}"); 
        } 
    }

    // ---- SQL Server ----
    async Task BulkInsert_SqlServer(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema(); 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var full = schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
        var inc = ProjectScalarNonIdentityProps(); 
        var dt = ToDataTable(items, inc);
        var conn = Ctx.Database.GetDbConnection(); 
        if (conn.State != ConnectionState.Open) 
            await conn.OpenAsync(); 
        if (conn is not SqlConnection sc) 
            throw new("SqlServer connection required");
        using var b = new SqlBulkCopy(sc) { DestinationTableName = full }; 
        foreach (DataColumn c in dt.Columns) 
            b.ColumnMappings.Add(c.ColumnName, c.ColumnName); 
        await b.WriteToServerAsync(dt);
    }

    // ---- PostgreSQL ----
    async Task BulkInsert_Postgres(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema() ?? "public"; 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var cols = ProjectScalarNonIdentityProps().Select(p => $"\"{p.Name}\"").ToArray(); 
        var inc = ProjectScalarNonIdentityProps();
        var conn = Ctx.Database.GetDbConnection(); 
        if (conn.State != ConnectionState.Open) 
            await conn.OpenAsync(); 
        if (conn is not Npgsql.NpgsqlConnection pg) 
            throw new("PostgreSQL connection required");
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

    // ---- MySQL ----
    async Task BulkInsert_MySql(List<T> items)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new("Entity metadata not found"); 
        var schema = et.GetSchema(); 
        var table = et.GetTableName() ?? throw new("No table name"); 
        var inc = ProjectScalarNonIdentityProps();
        var conn = Ctx.Database.GetDbConnection(); 
        if (conn.State != ConnectionState.Open) 
            await conn.OpenAsync();
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
            var cmd = conn.CreateCommand(); 
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

    // ---- Helpers ----
    static DataTable ToDataTable(List<T> items, List<PropertyInfo> inc) { 
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
    static bool IsScalar(Type t) { 
        t = Nullable.GetUnderlyingType(t) ?? t; 
        return t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(byte[]) || t == typeof(TimeSpan); 
    }
    static bool IsDbGenerated(PropertyInfo p) { 
        var a = p.GetCustomAttribute<DatabaseGeneratedAttribute>(); 
        return a?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity; 
    }
    static List<PropertyInfo> ProjectScalarNonIdentityProps() { 
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
