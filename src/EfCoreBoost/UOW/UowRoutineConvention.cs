using EfCore.Boost.DbRepo;

namespace EfCore.Boost.UOW
{
    internal enum RoutineKind
    {
        Scalar,    // returns a single value
        Query,     // returns rows
        NonQuery   // side effects only
    }

    internal enum RoutineCallMode { ProcedureName, SqlText, FromSqlRaw }
    internal readonly record struct RoutineCall(string Text, RoutineCallMode Mode);

    /// generally more capable than MySQL in this area
    internal sealed class RoutineConvention(DatabaseType dbType)
    {
        /// <summary>
        /// Responsible for building SQL for routine calls and sanitizing parameter names
        /// There are three modes of operation: ProcedureName, SqlText, and FromSqlRaw:
        /// ProcedureName is used for stored procedures (Ado driver calls),
        /// while SqlText is used for ad-hoc SQL statements.
        /// FromSqlRaw is used for EF Core's FromSqlRaw calls, which require specific SQL formatting.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routine"></param>
        /// <param name="kind"></param>
        /// <param name="parms"></param>
        /// <param name="useFromSqlRaw"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        internal RoutineCall Build(string schema, string routine, RoutineKind kind, IReadOnlyList<DbParmInfo>? parms, bool useFromSqlRaw = false)
        {
            parms ??= [];
            foreach (var t in parms)
                t.Name = NormalizeParamName(t.Name);
            if (useFromSqlRaw)
            {
                return dbType switch
                {
                    //We need to manually place the parameters into the SQL text
                    DatabaseType.SqlServer => BuildMsSqlFromSqlRaw(schema, routine, parms),
                    DatabaseType.PostgreSql => BuildPostgres(schema, routine, kind, parms),
                    DatabaseType.MySql => BuildMySqlFromSqlRaw(schema, routine, parms),
                    _ => throw new NotSupportedException($"Unsupported db type for FromSqlRaw: {dbType}")
                };
            }
            return dbType switch
            {
                DatabaseType.SqlServer => new($"{schema}.{routine}", RoutineCallMode.ProcedureName),
                DatabaseType.MySql => new(string.IsNullOrWhiteSpace(schema) ? routine : $"{schema}_{routine}", RoutineCallMode.ProcedureName),
                DatabaseType.PostgreSql => BuildPostgres(schema, routine, kind, parms),
                _ => throw new NotSupportedException($"Unsupported db type: {dbType}")
            };
        }

        internal static RoutineCall BuildMsSqlFromSqlRaw(string schema, string routine, IReadOnlyList<DbParmInfo> parms)
        {
            var name = $"[{schema}].[{routine}]";
            if (parms.Count == 0)
                return new RoutineCall($"EXEC {name}", RoutineCallMode.FromSqlRaw);
            var args = string.Join(", ", parms.Select(p => $"@{p.Name} = @{p.Name}"));
            return new RoutineCall($"EXEC {name} {args}", RoutineCallMode.FromSqlRaw);
        }

        internal static RoutineCall BuildMySqlFromSqlRaw(string schema, string routine, IReadOnlyList<DbParmInfo> parms)
        {
            var name = string.IsNullOrWhiteSpace(schema) ? $"`{routine}`" : $"`{schema}_{routine}`";
            var args = string.Join(", ", parms.Select(p => $"@{p.Name}"));
            return new RoutineCall($"CALL {name}({args})", RoutineCallMode.FromSqlRaw);
        }

        internal static RoutineCall BuildPostgres(string schema, string routine, RoutineKind kind, IReadOnlyList<DbParmInfo> parms)
        {
            var name = $"\"{schema}\".\"{routine}\"";
            var args = string.Join(", ", parms.Select(p => PrefixPostgresParmStrings(p.Name)));
            return kind switch
            {
                RoutineKind.Scalar => new($"SELECT {name}({args})", RoutineCallMode.SqlText),
                RoutineKind.Query => new($"SELECT * FROM {name}({args})", RoutineCallMode.SqlText),
                RoutineKind.NonQuery => new($"CALL {name}({args})", RoutineCallMode.SqlText),
                _ => throw new NotSupportedException()
            };
        }

        /// <summary>
        /// Postgresql requires parameter names to be prefixed with '@' character (if we use the named parameter syntax)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>

        internal static string PrefixPostgresParmStrings(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Parameter name cannot be empty.");
            return name[0] == '@' ? name : "@" + name;
        }

        /// <summary>
        /// Makes sure parameter names for commands are normalized - do not contain leading '@' or ':'
        /// Some efCore constructs are sensitive to names in parameter (db-sets FromSqlRaw, for example)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static string NormalizeParamName(string name)
        {
            return name.TrimStart('@', ':', '?');
        }

    }
}
