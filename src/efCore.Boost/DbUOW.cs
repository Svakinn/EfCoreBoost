using DbRepo.CFG;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace DbRepo
{
    /// <summary>
    /// Base unit of work class to be extended by inherited classess, doing the acutal db work
    /// </summary>
    public class DbUOW : IDisposable
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="cfgName"></param>
        public DbUOW(IConfiguration cfg, string cfgName)
        {
            this.Cfg = cfg;
            this.DbCfgName = cfgName;
            this.CreateDbContext();            // Must be overridden in subclass
            this.DbType = this.DetectDbType();   // Now that Ctx is assigned
        }

        /// <summary>
        /// Created DB-context to be used
        /// </summary>
        internal protected DbContext? Ctx { get; set; }
        public string DbCfgName { get; set; }
        public DatabaseType DbType { get; protected set; }

        protected IConfiguration Cfg;
        private bool _disposed;

        /// <summary>
        /// Virtual method to create the DB-context
        /// Must be overwritten by inherited classes
        /// </summary>
        protected virtual void CreateDbContext()
        {
            //Define your DBContext like:
            //this.Ctx = new Entities();
        }

        public DatabaseType DetectDbType()
        {
            if (this.Ctx == null)
                return DatabaseType.Unknown;
            var provider = this.Ctx?.Database.ProviderName?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(provider))
                return DatabaseType.Unknown;
            else if (provider.Contains("sqlserver"))
                return DatabaseType.SqlServer;
            else if (provider.Contains("postgres"))
                return DatabaseType.PostgreSql;
            else if (provider.Contains("mysql"))
                return DatabaseType.MySql;
            else if (provider.Contains("sqlite"))
                return DatabaseType.Sqlite;
            else if (provider.Contains("oracle"))
                return DatabaseType.Oracle;
            else if (provider.Contains("inmemory"))
                return DatabaseType.InMemory;
            else
                return DatabaseType.Unknown;
        }

        /// <summary>
        /// Override default command timeout (in seconds)
        /// </summary>
        /// <param name="seconds">New command time limit in seconds</param>
        public void SetCommandTimeout(int seconds)
        {
            this.Ctx?.Database.SetCommandTimeout(seconds);
        }

        public string Metadata()
        {
            return EdmContextBuilder.BuildXMLModelFromContext(this.Ctx!);
        }

        public static Microsoft.OData.Edm.IEdmModel GetModel<T>() where T : DbContext, new()
        {
            return EdmBuilder.BuildEdmModelFromContext<T>();
        }

        /// <summary>
        /// SaveChangesAsync the saved changes, but in background
        /// Use this only when you do not rely on data to be saved before doing additional changes
        /// </summary>
        /// <returns></returns>
        public virtual async Task SaveChangesAsync()
        {
            NormalizeDateTimeValues();
            await this.Ctx!.SaveChangesAsync();
        }

        /// <summary>
        /// When dealing with large inserts/updates performance is increased by recreating the context after commit
        /// However we need to be carefull if any links are present to any of the previous context data
        /// </summary>
        public async Task SaveChangesAndNewAsync()
        {
            try
            {
                NormalizeDateTimeValues();
                await this.Ctx!.SaveChangesAsync();
                this.Ctx.Dispose();
                this.CreateDbContext();
            }
            catch (Exception)
            {
                this.Ctx?.Dispose();
                this.CreateDbContext();
                throw;
            }
        }

        #region ChangeTracker tuning

        /// <summary>
        /// Enables or disables automatic change detection in the underlying DbContext.
        /// Set to <c>false</c> when performing bulk inserts (e.g., via <c>AddRange</c>) to improve performance.
        /// Remember to restore the original setting after saving changes.
        /// </summary>
        /// <param name="enable">True to enable automatic change detection; false to disable.</param>
        public void SetAutoDetectChanges(bool enable) => Ctx!.ChangeTracker.AutoDetectChangesEnabled = enable;

        /// <summary>
        /// Returns the current state of automatic change detection in the DbContext.
        /// </summary>
        public bool IsAutoDetectChangesEnabled() => Ctx!.ChangeTracker.AutoDetectChangesEnabled;

        /// <summary>
        /// Triggers manual change detection. Useful when <c>AutoDetectChanges</c> is disabled
        /// and you need EF to process changes before calling <c>SaveChanges</c>.
        /// </summary>
        public void DetectChanges() => Ctx!.ChangeTracker.DetectChanges();

        /// <summary>
        /// Accepts all changes tracked by the DbContext.
        /// Use this if you disabled automatic acceptance via <c>SaveChanges(acceptAllChangesOnSuccess: false)</c>
        /// and want to manually finalize the changes.
        /// </summary>
        public void AcceptAllChanges() => Ctx!.ChangeTracker.AcceptAllChanges();

        /// <summary>
        /// Internal disposable helper for restoring a setting when disposed.
        /// Used by <c>WithAutoDetectChangesDisabled()</c> to reset AutoDetectChanges state.
        /// </summary>
        private class DelegateDisposable : IDisposable
        {
            private readonly Action _onDispose;

            /// <summary>
            /// Initializes a new instance of the <see cref="DelegateDisposable"/> class with a callback to execute on disposal.
            /// </summary>
            /// <param name="onDispose">The action to execute when disposed.</param>
            public DelegateDisposable(Action onDispose) => _onDispose = onDispose;

            /// <summary>
            /// Invokes the stored disposal action.
            /// </summary>
            public void Dispose() => _onDispose();
        }

        /// <summary>
        /// Temporarily disables automatic change detection for the current DbContext.
        /// Automatically restores the original state when the returned IDisposable is disposed.
        /// Use with a <c>using</c> block to safely wrap bulk insert operations.
        /// </summary>
        /// <returns>An IDisposable that restores the original AutoDetectChanges setting on disposal.</returns>
        public IDisposable WithAutoDetectChangesDisabled()
        {
            var original = Ctx!.ChangeTracker.AutoDetectChangesEnabled;
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            return new DelegateDisposable(() => Ctx.ChangeTracker.AutoDetectChangesEnabled = original);
        }

        #endregion


        #region Transactions
        private IDbContextTransaction? _currentTransaction;

        /// <summary>
        /// Begins a new database transaction for the current context.
        /// Throws if a transaction is already active.
        /// </summary>
        /// <param name="isolationLevel">
        /// Default is ReadCommited, you can override wih any you want.
        /// Note: some are not supported by all poviders.
        /// ReadUncommitted maps to ReadCommitted for postgres and mySql
        /// ReadCommitted - default supported by all
        /// RepeatableRead - supported by all
        /// Serializable - supported by all
        /// Snapshot - only for SQL Server and then only if specifficly enabled on server (avoid if you can)
        /// Chaos - not supported, legacy, NEVER USE THIS OPTION
        /// if you try Chaos or Snapshot, you may/willl get error thrown (dont do it)
        /// </param>
        public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
              if (_currentTransaction != null)
                throw new InvalidOperationException("A transaction is already active on this context.");
            _currentTransaction = await Ctx!.Database.BeginTransactionAsync(isolationLevel);
        }

        /// <summary>
        /// Commits the current active transaction.
        /// Throws if no transaction is active.
        /// </summary>
        public async Task CommitTransactionAsync()
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No active transaction to commit.");
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        /// <summary>
        /// Rolls back the current active transaction.
        /// Throws if no transaction is active.
        /// </summary>
        public async Task RollbackTransactionAsync()
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No active transaction to roll back.");
            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        /// <summary>
        /// Executes a unit of work inside a transaction, including saving changes.
        /// Rolls back the transaction if any exception is thrown during the operation or save.
        /// Example usage:
        /// await uow.ExecuteInTransactionAsync(async () => {
        ///   uow.MyRepo.DoSomething();
        /// });
        /// </summary>
        /// <param name="action">The async action to execute within the transaction scope (e.g., adding or modifying entities).</param>
        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await BeginTransactionAsync();
            try
            {
                await action();                  // user-defined logic
                await SaveChangesAsync();        // ensure changes are persisted
                await CommitTransactionAsync();  // commit only if all succeeded
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
        }
        #endregion

        /// <summary>
        /// Used for generating exception message including all inner exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string SqlExceptionMessages(SqlException ex)
        {
            var ret = "";
            if (ex != null)
            {
                if (ex.Errors != null && ex.Errors.Count > 0)
                {
                    foreach (var err in ex.Errors)
                    {
                        ret += "\r\n" + err.ToString();
                    }
                    ret += "\r\n";
                }
            }
            return ret;
        }

        #region raw stuff

        /// <summary>
        /// Executes a single SQL command with parameters.
        /// Use <see cref="ExecSqlScriptAsync"/> if your script contains multiple statements (e.g., separated by ';' or 'GO').
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async Task ExecSqlCmdAsync(string cmd)
        {
            await this.Ctx!.Database.ExecuteSqlRawAsync(cmd);
        }

        /// <summary>
        /// Intended to run single sql command.
        /// If you indend to run more than one at a time (script with commands seporarted by ; or go), use the ExecSqlScriptAsync
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public async Task ExecSqlCmdAsync(string cmd, params object[] parameters)
        {
            await this.Ctx!.Database.ExecuteSqlRawAsync(cmd, parameters);
        }

        /// <summary>
        /// Executes a SQL script containing multiple statements.
        /// Handles all supported database types.
        /// For MySQL, this uses MySql.Data to allow execution of complex scripts with delimiters.
        /// </summary>
        /// <param name="scriptContent"></param>
        /// <returns></returns>
        public async Task ExecSqlScriptAsync(string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
                return;
            if (this.DbType == DatabaseType.MySql)
                await MySqlScriptRunner.ExecScriptAsync(Ctx!.Database.GetDbConnection(), scriptContent);
            else
                await this.ExecSqlCmdAsync(scriptContent);
        }

        /// <summary>
        /// MySQL (via Pomelo / MySqlConnector) does not support executing multiple statements in a single command.
        /// This runner uses MySql.Data.MySqlClient.MySqlScript to execute full SQL scripts (e.g., with DELIMITER blocks).
        /// </summary>
        internal static class MySqlScriptRunner
        {
            public static async Task ExecScriptAsync(DbConnection conn, string scriptContent)
            {
                if (conn is not MySqlConnection myConn)
                    throw new InvalidCastException("Connection must be a MySql.Data.MySqlClient.MySqlConnection");
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();
                await new MySqlScript(myConn, scriptContent).ExecuteAsync();
            }
        }

        public class DbParmInfo
        {
            public DbParmInfo(string name, object objValue)
            {
                Name = name;
                ObjValue = objValue;
                OutValue = DBNull.Value;
                IsOut = false;
            }
            public string Name { get; set; } = default!;
            public object ObjValue { get; set; } = default!;
            public bool IsOut { get; set; } = false;
            public object OutValue { get; set; } = default!;
            public DbType? DbType { get; set; }
            protected DbParameter? ParmStore { get; set; }
            public Object? GetOutValue()
            {
                if (IsOut && ParmStore != null)
                    return ParmStore.Value;
                return null;
            }
            public void HookUpOutValue(DbParameter parm)
            {
                this.ParmStore = parm;
            }
        }

        private void ToDbParms(List<DbParmInfo>? parms, DbCommand cmd)
        {
            if (parms != null && parms.Count > 0)
            {
                foreach (var param in parms)
                {
                    var pa = cmd.CreateParameter();
                    pa.ParameterName = param.Name;
                    if (param.IsOut)
                    {
                        pa.Direction = ParameterDirection.Output;
                        pa.Value = param.OutValue;
                        pa.DbType = (DbType)param.DbType!;
                        param.HookUpOutValue(pa);  //so we can retreive it after the command has run
                    }
                    else
                        pa.Value = param.ObjValue;
                    cmd.Parameters.Add(pa);
                }
            }
        }

        public async Task<long?> GetLongScalarAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return null;
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            this.ToDbParms(parameters, cmd);
            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result) : null;
        }

        public async Task<int?> GetIntScalarAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return null;
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            this.ToDbParms(parameters, cmd);
            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
        }

        public async Task<string?> GetStringScalarAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return null;
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            this.ToDbParms(parameters, cmd);
            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? result.ToString() : null;
        }

        public async Task<List<long>> GetLongListAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            var list = new List<long>();

            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return [];
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            this.ToDbParms(parameters, cmd);
            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt64(0));
            }

            return list;
        }

        public async Task<List<int>> GetIntListAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            var list = new List<int>();

            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return [];
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var param in parameters)
                {
                    var pa = cmd.CreateParameter();
                    pa.ParameterName = param.Name;
                    pa.Value = param.ObjValue;
                    cmd.Parameters.Add(pa);
                }
            }

            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt32(0));
            }

            return list;
        }

        public async Task<List<string>> GetStringListAsync(string sql, List<DbParmInfo>? parameters, bool storedProc = false)
        {
            var list = new List<string>();

            using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
            if (cmd == null || cmd.Connection == null) return [];
            cmd.CommandText = sql;
            cmd.CommandType = storedProc ? CommandType.StoredProcedure : CommandType.Text;
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var param in parameters)
                {
                    var pa = cmd.CreateParameter();
                    pa.ParameterName = param.Name;
                    pa.Value = param.ObjValue;
                    cmd.Parameters.Add(pa);
                }
            }

            if (cmd.Connection.State != ConnectionState.Open)
                await cmd.Connection.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetString(0));
            }

            return list;
        }

        #endregion

        #region trunc helpers
        public string? sTrunc(string? str, int maxLen = 0)
        {
            if (str == null) return null;
            if (maxLen == 0 || maxLen <= str.Length) return str;
            return str[..maxLen];
        }
        public string sTruncS(string? str, int maxLen = 0)
        {
            if (str == null) return "";
            if (maxLen == 0 || maxLen <= str.Length) return str;
            return str[..maxLen];
        }

        /// <summary>
        /// So datetime functionality is a pain in the Ar* for Postgres since it has to be defined if datetime is stored with or without timezone, when c# datetime does not care
        /// Still it looks like lyou neeed to specify in-code the datetime c# object if it is with or withot timezone before being saved
        /// Our solution is to specificallly have the datatype in pg as timestamp without timezone and filter it before being saved to the db by specifying kind
        /// </summary>
        public virtual void NormalizeDateTimeValues()
        {
            if (this.DbType != DatabaseType.PostgreSql)
                return;

            var entries = this.Ctx!.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.ClrType == typeof(DateTime) && prop.CurrentValue is DateTime dt)
                    {
                        if (dt.Kind == DateTimeKind.Utc)
                            prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                    }
                    else 
                    if (prop.Metadata.ClrType == typeof(DateTime?) && prop.CurrentValue is DateTime dtN)
                    {
                        if (dtN.Kind == DateTimeKind.Utc)
                            prop.CurrentValue = (DateTime?)DateTime.SpecifyKind(dtN, DateTimeKind.Unspecified);
                    }
                }
            }
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Ctx?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
