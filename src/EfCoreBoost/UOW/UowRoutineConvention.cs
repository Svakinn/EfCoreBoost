using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace EfCore.Boost.UOW
{
    internal enum RoutineKind
    {
        Scalar,    // returns a single value
        Query,     // returns rows
        NonQuery   // side effects only
    }

    internal enum RoutineCallMode { ProcedureName, SqlText }
    internal readonly record struct RoutineCall(string Text, RoutineCallMode Mode);

    /// generally more capable than MySQL in this area
    internal sealed class RoutineConvention
    {
        readonly DatabaseType _dbType;
        public RoutineConvention(DatabaseType dbType) => _dbType = dbType;

        internal RoutineCall Build(string schema, string routine, RoutineKind kind, IReadOnlyList<DbParmInfo>? parms)
        {
            parms ??= [];
            return _dbType switch
            {
                DatabaseType.SqlServer => new($"{schema}.{routine}", RoutineCallMode.ProcedureName),
                DatabaseType.MySql => new($"{schema}_{routine}", RoutineCallMode.ProcedureName),
                DatabaseType.PostgreSql => BuildPostgres(schema, routine, kind, parms),
                _ => throw new NotSupportedException($"Unsupported db type: {_dbType}")
            };
        }

        internal static RoutineCall BuildPostgres(string schema, string routine, RoutineKind kind, IReadOnlyList<DbParmInfo> parms)
        {
            var name = $"{schema}.\"{routine}\"";
            var args = string.Join(", ", parms.Select(p => NormalizeParamName(p.Name)));

            return kind switch
            {
                RoutineKind.Scalar => new($"SELECT {name}({args})", RoutineCallMode.SqlText),
                RoutineKind.Query => new($"SELECT * FROM {name}({args})", RoutineCallMode.SqlText),
                RoutineKind.NonQuery => new($"CALL {name}({args})", RoutineCallMode.SqlText),
                _ => throw new NotSupportedException()
            };
        }

        internal static string NormalizeParamName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Parameter name cannot be empty.");
            return name[0] == '@' ? name : "@" + name;
        }
    }
}
