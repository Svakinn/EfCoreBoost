// Copyright © 2026 Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using EfCore.Boost.CFG;
using EfCore.Boost.DbRepo;
using EfCore.Boost.EDM;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.SqlServer.Server;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.UOW
{

    /// <summary>
    /// Unit-of-work over a single DbContext.
    /// Not concurrency-safe: do not use the same UOW instance from multiple concurrent operations.
    /// Create a new UOW per scope (request/job) and dispose it when done.
    /// </summary>
    public interface IDbReadUow : IDisposable
    {
        /// <summary>
        /// Rerturns the EDMX metadata XML representation of the model. Used by OData endpoints.
        /// </summary>
        /// <returns></returns>
        string Metadata();

        /// <summary>
        /// Override default command timeout (in seconds)
        /// </summary>
        /// <param name="seconds">New command time limit in seconds</param>
        void SetCommandTimeout(int seconds);

        /// <summary>
        /// Gets the Entity Data Model (EDM) associated with the dataset. Also used by OData endpoints.
        /// </summary>
        /// <returns>An <see cref="IEdmModel"/> instance representing the EDM for the current context.</returns>
        IEdmModel GetModel();

        /// <summary>
        /// Gets the type of database associated with the current context.
        /// This is currently any of MySql, PostgreSql, SqlServer
        /// </summary>
        DatabaseType DbType { get; }

#pragma warning disable CS1570 // XML comment has badly formed XML
        /// <summary>
        /// Builds an IQueryable<T> from a provider-correct routine (function or query-shaped procedure).
        /// The routine must return rows matching entity T, otherwise materialization will fail.
        /// The query is not executed here; it runs when enumerated and may be further composed.
        /// Composability (Where/OrderBy) depends on provider and SQL form. PstgreSQL and SQL Server are
        /// generally more capable than MySQL in this area
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="schema">Database schema</param>
        /// <param name="routineName">Routine name (sp or func)</param>
        /// <param name="parameters">Optional list of parameters to pass to the routine</param>
        /// <returns></returns>
        IQueryable<T> SetUpRoutineQuery<T>(string schema, string routineName, List<DbParmInfo>? parameters = null) where T : class;
#pragma warning restore CS1570 // XML comment has badly formed XML
        /// <summary>
        /// Calls a non-query routine (stored procedure) with the given parameters.
        /// </summary>
        /// <param name="schema">Database schema</param>
        /// <param name="routineName">Routine name (sp or func)</param>
        /// <param name="parameters">Optional list of parameters to pass to the routine</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        Task<int> RunRoutineNonQueryAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineNonQueryAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        int RunRoutineNonQuerySynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Executes a SQL script that may contain multiple statements.
        /// Splits the input into executable batches based on the active database provider.
        /// </summary>
        /// <param name="scriptContent">A single statement or a multi-statement script.</param>
        /// <param name="useTransaction">
        /// If true, executes all batches inside a single transaction.
        /// Use only for data-only scripts. Many DDL statements are non-transactional or forbidden in a transaction (provider-dependent).
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        Task ExecSqlScriptAsync(string scriptContent, bool useTransaction = false, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of ExecSqlScriptAsync
        /// </summary>
        /// <param name="scriptContent"></param>
        /// <param name="useTransaction"></param>
        void ExecSqlScriptSynchronized(string scriptContent, bool useTransaction = false);

        /// <summary>
        /// Executes a single SQL statement as a non-query (CommandType.Text).
        /// This is intended for one statement only (INSERT, UPDATE, DELETE, DDL, etc.).
        /// Do not use for multi-statement scripts; use ExecSqlScriptAsync for such instead.
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string sql, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of ExecuteNonQueryAsync
        /// </summary>
        int ExecuteNonQuerySynchronized(string sql, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Runs <paramref name="work"/> inside a resilient EF Core transaction (ExecutionStrategy-aware).
        /// Use this when the operations must be atomic (commit/rollback handled by the UOW).
        /// The transaction is created on the current DbContext connection, so any commands executed on the same
        /// connection (including bulk operations that accept a DbTransaction) can participate.
        /// Notes:
        /// - If the provider enables transient retries (e.g. Azure SQL), the work delegate may be retried.
        /// - Keep <paramref name="work"/> deterministic and avoid non-idempotent external side-effects.
        /// </summary>
        /// <param name="work">Async delegate to execute within the transaction.</param>
        /// <param name="iso">Isolation level for the transaction.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RunInTransactionAsync(Func<CancellationToken, Task> work, System.Data.IsolationLevel iso = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunInTransactionAsync
        /// </summary>
        /// <param name="work"></param>
        /// <param name="iso"></param>
        void RunInTransactionSynchronized(Action work, IsolationLevel iso = IsolationLevel.ReadCommitted);

        /// <summary>
        /// Executes a scalar database routine and returns the result as a nullable 64-bit integer.
        /// </summary>
        /// <param name="schema">The name of the database schema that contains the routine to execute.</param>
        /// <param name="routineName">The name of the routine to execute. (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The scalar value from the routine</returns>
        Task<long?> RunRoutineLongAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineLongAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        long? RunRoutineLongSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Executes a scalar database routine and returns the result as a nullable 32-bit integer.
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <param name="routineName">Routine name (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<int?> RunRoutineIntAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineStringAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        string? RunRoutineStringSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Executes a scalar database routine and returns the result as a nullable string
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <param name="routineName">Routine name (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<string?> RunRoutineStringAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Executes a scalar database routine pre-made for returning a list of 64-bit integers.
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <param name="routineName">Routine name (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<List<long>> RunRoutineLongListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineLongListAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        List<long> RunRoutineLongListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Executes a scalar database routine pre-made for returning a list of 32-bit integers.
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <param name="routineName">Routine name (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<List<int>> RunRoutineIntListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineIntListAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        List<int> RunRoutineIntListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Executes a scalar database routine pre-made for returning a list of strings.
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <param name="routineName">Routine name (Case sensitive)</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<List<string>> RunRoutineStringListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of RunRoutineStringListAsync
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="routineName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        List<string> RunRoutineStringListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null);

        /// <summary>
        /// Raw sql runner, returning scalar long value
        /// </summary>
        /// <remarks>
        /// Raw sql is not cross-db compatible and should be avoided.
        /// Proper way would be to set up routine and call that one instead.
        /// </remarks>
        /// <param name="sql">Raw SQL to execute</param>
        /// <param name="parameters">An optional list of parameters to pass to the routine. If null, no parameters are used.</param>
        /// <param name="ct">Cancelation token</param>
        /// <returns></returns>
        Task<long?> GetLongScalarAsync(string sql, List<DbParmInfo>? parameters = null, CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of GetLongScalarAsync
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        long? GetLongScalarSynchronized(string sql, List<DbParmInfo>? parameters = null);
    }

    public interface IDbUow : IDbReadUow {

        /// <summary>
        /// Saves pending changes to the database. Applies internal normalization/truncation rules before saving.
        /// </summary>
        /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns></returns>
        Task SaveChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of SaveChangesAsync
        /// </summary>
        void SaveChangesSynchronized();

        /// <summary>
        /// Saves all pending changes to the data store and creates a new entity instance for further
        /// editing. This is useful when performing large inserts/updates in batches to reduce change-tracker overhead.
        /// </summary>
        /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns></returns>
        Task SaveChangesAndNewAsync(CancellationToken ct = default);

        /// <summary>
        /// Syncronized version of SaveChangesAndNewAsync
        /// </summary>
        void SaveChangesAndNewSynchronized();


#pragma warning disable CS1587 // XML comment is not placed on a valid language element
        /// <summary>
        /// In case you would ever want to give access to the underlying DbContext for some reason, you can get it via this method.
        /// Use with care, as direct access to the DbContext can lead to unintended side effects if not handled properly.
        /// Returns the underlying EF DbContext instance for advanced scenarios where you need direct access.
        /// </summary>
        /// <returns>The EF´s DBContext</returns>
        //DbContext GetDbContext();
#pragma warning restore CS1587 // XML comment is not placed on a valid language element

        /// <summary>
        /// Enables or disables automatic change detection in the underlying DbContext.
        /// Set to <c>false</c> when performing bulk inserts (e.g., via <c>AddRange</c>) to improve performance.
        /// Remember to restore the original setting after doing your things.
        /// </summary>
        /// <param name="enable">True to enable automatic change detection; false to disable.</param>
        void SetAutoDetectChanges(bool enable);


        /// <summary>
        /// Returns the current state of automatic change detection in the DbContext.
        /// </summary>
        bool IsAutoDetectChangesEnabled();

        /// <summary>
        /// Triggers manual change detection. Useful when <c>AutoDetectChanges</c> is disabled
        /// and you need EF to process changes before calling <c>SaveChanges</c>.
        /// </summary>
        void DetectChanges();

        /// <summary>
        /// Accepts all changes tracked by the DbContext.
        /// Use this if you disabled automatic acceptance via <c>SaveChanges(acceptAllChangesOnSuccess: false)</c>
        /// and want to manually finalize the changes.
        /// </summary>
        void AcceptAllChanges();

        /// <summary>
        /// Temporarily disables automatic change detection for the current DbContext.
        /// Automatically restores the original state when the returned IDisposable is disposed.
        /// Use with a <c>using</c> block to safely wrap bulk insert operations.
        /// </summary>
        /// <returns>An IDisposable that restores the original AutoDetectChanges setting on disposal.</returns>
        IDisposable WithAutoDetectChangesDisabled();
    }

    public abstract class DbUow<TCtx> : DbReadUow<TCtx> where TCtx : DbContext
    {
        protected DbUow(Func<TCtx> ctxFactory) : base(ctxFactory) { }

        #region save changes
        //See interface for documentation
        public virtual async Task SaveChangesAsync(CancellationToken ct = default)
        {
            NormalizeDateTimeValues();
            TruncateStringValues();
            await Ctx.SaveChangesAsync(ct);
        }

        //See interface for documentation
        public virtual void SaveChangesSynchronized()
        {
            NormalizeDateTimeValues();
            TruncateStringValues();
            Ctx.SaveChanges();
        }

        //See interface for documentation
        // Remarks:
        // Not valid while a transaction is active on this UOW instance.
        public async Task SaveChangesAndNewAsync(CancellationToken ct = default)
        {
            if (_currentTx != null)
                throw new InvalidOperationException("SaveChangesAndNewAsync() cannot be used while a transaction is active. Don´t use this method within transaction block !.");
            try
            {
                this.NormalizeDateTimeValues();
                this.TruncateStringValues();
                await this.Ctx.SaveChangesAsync(ct);
                this.RecreateContext();
            }
            catch (Exception)
            {
                this.RecreateContext();
                throw;
            }
        }

        //See interface for documentation
        public void SaveChangesAndNewSynchronized()
        {
            if (_currentTx != null)
                throw new InvalidOperationException("SaveChangesAndNewSynchronized() cannot be used while a transaction is active. Don´t use this method within transaction block !.");
            try
            {
                this.NormalizeDateTimeValues();
                this.TruncateStringValues();
                this.Ctx.SaveChanges();
                this.RecreateContext();
            }
            catch (Exception)
            {
                this.RecreateContext();
                throw;
            }
        }
        #endregion

        #region ChangeTracker tuning

        //See interface for documentation
        public void SetAutoDetectChanges(bool enable) => Ctx.ChangeTracker.AutoDetectChangesEnabled = enable;

        //See interface for documentation
        public bool IsAutoDetectChangesEnabled() => Ctx.ChangeTracker.AutoDetectChangesEnabled;

        //See interface for documentation
        public void DetectChanges() => Ctx.ChangeTracker.DetectChanges();

        //See interface for documentation
        public void AcceptAllChanges() => Ctx.ChangeTracker.AcceptAllChanges();

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

        //See interface for documentation
        public IDisposable WithAutoDetectChangesDisabled()
        {
            var original = Ctx.ChangeTracker.AutoDetectChangesEnabled;
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            return new DelegateDisposable(() => Ctx.ChangeTracker.AutoDetectChangesEnabled = original);
        }
        #endregion
    }

    /// <summary>
    /// Base unit of work class to be extended by inherited classes, doing the actual db work
    /// </summary>
    public abstract class DbReadUow<TCtx> : IDbReadUow where TCtx : DbContext
    {
        protected readonly Func<TCtx> CtxFactory;
        protected TCtx Ctx;
        public DatabaseType DbType { get; protected set; }
        protected bool _disposed;
        //EDM model caching
        protected static IEdmModel? _cachedEdmModel;
        protected static readonly object _edmLock = new();
        //Transaction support
        protected IDbContextTransaction? _currentTx;
        protected readonly SemaphoreSlim _sync = new(1, 1);

        protected DbReadUow(Func<TCtx> ctxFactory)
        {
            CtxFactory = ctxFactory;
            Ctx = CtxFactory();
            DbType = DetectDbType(Ctx);
        }

        ///See interface for documentation
        public DbContext GetDbContext() => Ctx;

        protected void RecreateContext()
        {
            Ctx.Dispose();
            Ctx = CtxFactory();
            DbType = DetectDbType(Ctx);
        }

        /// <summary>
        /// Detect db type by provider
        /// </summary>
        /// <returns></returns>
        static DatabaseType DetectDbType(DbContext ctx)
        {
            var p = ctx.Database.ProviderName?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(p)) return DatabaseType.Unknown;
            if (p.Contains("sqlserver")) return DatabaseType.SqlServer;
            if (p.Contains("postgres")) return DatabaseType.PostgreSql;
            if (p.Contains("mysql")) return DatabaseType.MySql;
            if (p.Contains("sqlite")) return DatabaseType.Sqlite;
            if (p.Contains("oracle")) return DatabaseType.Oracle;
            if (p.Contains("inmemory")) return DatabaseType.InMemory;
            return DatabaseType.Unknown;
        }

        ///See interface for documentation
        public void SetCommandTimeout(int seconds)
        {
            this.Ctx.Database.SetCommandTimeout(seconds);
        }

        //See interface for documentation
        public string Metadata()
        {
            return EdmContextBuilder.BuildXMLModelFromContext(this.Ctx);
        }

        ///See interface for documentation
        public IEdmModel GetModel()
        {
            if (_cachedEdmModel != null) return _cachedEdmModel;
            lock (_edmLock)
            {
                _cachedEdmModel ??= EdmContextBuilder.BuildEdmModelFromContext(Ctx);
            }
            return _cachedEdmModel;
        }

        /// <summary>
        /// Used for generating exception message including all inner exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <returns>Encaptulated full error text</returns>
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

        #region command/script execution

        //See interface for documentation
        public async Task<int> RunRoutineNonQueryAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.NonQuery, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            return await oc.Cmd.ExecuteNonQueryAsync(ct);
        }

        //See interface for documentation
        public async Task<int> ExecuteNonQueryAsync(string sql, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = sql;
            oc.Cmd.CommandType = CommandType.Text;
            ToDbParms(parameters, oc.Cmd);
            return await oc.Cmd.ExecuteNonQueryAsync(ct);
        }

        //See interface for documentation
        public int RunRoutineNonQuerySynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.NonQuery, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            return oc.Cmd.ExecuteNonQuery();
        }

        //See interface for documentation
        public int ExecuteNonQuerySynchronized(string sql, List<DbParmInfo>? parameters = null)
        {
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = sql;
            oc.Cmd.CommandType = CommandType.Text;
            ToDbParms(parameters, oc.Cmd);
            return oc.Cmd.ExecuteNonQuery();
        }

        //See interface for documentation
        public async Task<long?> RunRoutineLongAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = await oc.Cmd.ExecuteScalarAsync(ct);
            return result != null && result != DBNull.Value ? Convert.ToInt64(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public long? RunRoutineLongSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = oc.Cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public async Task<int?> RunRoutineIntAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = await oc.Cmd.ExecuteScalarAsync(ct);
            return result != null && result != DBNull.Value ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public int? RunRoutineIntSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = oc.Cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public async Task<string?> RunRoutineStringAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = await oc.Cmd.ExecuteScalarAsync(ct);
            return result != null && result != DBNull.Value ? Convert.ToString(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public string? RunRoutineStringSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Scalar, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var result = oc.Cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToString(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public async Task<List<long>> RunRoutineLongListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<long>();
            using var reader = await oc.Cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt64(0));
            }
            return list;
        }

        //See interface for documentation
        public List<long> RunRoutineLongListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<long>();
            using var reader = oc.Cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt64(0));
            }
            return list;
        }

        //See interface for documentation
        public async Task<List<int>> RunRoutineIntListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<int>();
            using var reader = await oc.Cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt32(0));
            }
            return list;
        }

        //See interface for documentation
        public List<int> RunRoutineIntListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<int>();
            using var reader = oc.Cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetInt32(0));
            }
            return list;
        }

        //See interface for documentation
        public async Task<List<string>> RunRoutineStringListAsync(string schema, string routineName, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<string>();
            using var reader = await oc.Cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetString(0));
            }
            return list;
        }

        //See interface for documentation
        public List<string> RunRoutineStringListSynchronized(string schema, string routineName, List<DbParmInfo>? parameters = null)
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = call.Text;
            oc.Cmd.CommandType = CalcCommandType(call.Mode);
            ToDbParms(parameters, oc.Cmd);
            var list = new List<string>();
            using var reader = oc.Cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetString(0));
            }
            return list;
        }

        #endregion

        #region Transactions
        //See interface for documentation
        protected async Task RunInTransactionAsync(Func<DbTransaction, CancellationToken, Task> work, System.Data.IsolationLevel iso = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (_currentTx != null) throw new InvalidOperationException("A transaction is already active on this UOW instance. Use nested logic inside the same RunInTransaction call.");

            var strat = Ctx.Database.CreateExecutionStrategy();
            await strat.ExecuteAsync(async () =>
            {
                await using var efTx = await Ctx.Database.BeginTransactionAsync(iso, ct);
                _currentTx = efTx;
                try
                {
                    var dbTx = efTx.GetDbTransaction();
                    await work(dbTx, ct);
                    await efTx.CommitAsync(ct);
                }
                catch
                {
                    try { await efTx.RollbackAsync(ct); } catch { /* ignore rollback errors */ }
                    throw;
                }
                finally
                {
                    _currentTx = null;
                }
            });
        }

        //See interface for documentation
        public Task RunInTransactionAsync(Func<CancellationToken, Task> work, System.Data.IsolationLevel iso = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default)
                => RunInTransactionAsync(async (tx, c) => await work(c), iso, ct);

        //See interface for documentation
        public void RunInTransactionSynchronized(Action work, IsolationLevel iso = IsolationLevel.ReadCommitted)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (_currentTx != null) throw new InvalidOperationException("A transaction is already active on this UOW instance. Use nested logic inside the same RunInTransaction call.");
            _sync.Wait();
            try
            {
                var strat = Ctx.Database.CreateExecutionStrategy();
                strat.Execute(() =>
                {
                    using var efTx = Ctx.Database.BeginTransaction(iso);
                    _currentTx = efTx;
                    try
                    {
                        work();
                        efTx.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { efTx.Rollback(); } catch { }
                        throw new InvalidOperationException($"Transaction failed (iso={iso}). See inner exception for root cause.", ex);
                    }
                    finally
                    {
                        _currentTx = null;
                    }
                });
            }
            finally
            {
                _sync.Release();
            }
        }

        #endregion

        #region SQL in transactions
        //See interface for documentation
        public async Task ExecSqlScriptAsync(string scriptContent, bool useTransaction = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(scriptContent)) return;
            var scripts = ScriptSplitter(scriptContent)?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (scripts == null || scripts.Count == 0) return;
            if (!useTransaction)
            {
                foreach (var sql in scripts)
                    await ExecuteNonQueryAsync(sql, null, ct);
                return;
            }
            await RunInTransactionAsync(async (tx, c) =>
            {
                foreach (var sql in scripts)
                    await ExecuteNonQueryAsync(sql, null, c);
            }, IsolationLevel.ReadCommitted, ct);
        }

        //See interface for documentation
        public void ExecSqlScriptSynchronized(string scriptContent, bool useTransaction = false)
        {
            if (string.IsNullOrWhiteSpace(scriptContent)) return;
            var scripts = ScriptSplitter(scriptContent)?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (scripts == null || scripts.Count == 0) return;
            if (!useTransaction)
            {
                foreach (var sql in scripts)
                    _ = ExecuteNonQuerySynchronized(sql, null);
                return;
            }
            RunInTransactionSynchronized(() =>
            {
                foreach (var sql in scripts)
                    ExecuteNonQuerySynchronized(sql, null);
            }, IsolationLevel.ReadCommitted);
        }

        private IEnumerable<string>? ScriptSplitter(string scriptContent)
        {
            if (DbType == DatabaseType.MySql)
                return SqlScriptSplitters.SplitMySql(scriptContent);
            else if (DbType == DatabaseType.PostgreSql)
                return SqlScriptSplitters.SplitPostgres(scriptContent);
            else if (DbType == DatabaseType.SqlServer)
                return SqlScriptSplitters.SplitMsSql(scriptContent);
            throw new NotImplementedException($"Script splitting not implemented for database type {DbType}.");
        }
        #endregion

        #region command/script execution + routine wrappers

        private void ToDbParms(List<DbParmInfo>? parms, DbCommand cmd)
        {
            if (parms == null || parms.Count == 0) return;
            foreach (var param in parms)
            {
                var pa = cmd.CreateParameter();
                pa.ParameterName = param.Name;
                if (param.IsOut)
                {
                    pa.Direction = ParameterDirection.Output;
                    pa.Value = param.OutValue ?? DBNull.Value;
                    if (param.DbType == null)
                        throw new InvalidOperationException($"Output parameter '{param.Name}' requires DbType.");
                    pa.DbType = param.DbType.Value;
                    param.HookUpOutValue(pa);
                }
                else pa.Value = param.ObjValue ?? DBNull.Value;
                cmd.Parameters.Add(pa);
            }
        }

        private static CommandType CalcCommandType(RoutineCallMode mode) => mode == RoutineCallMode.ProcedureName ? CommandType.StoredProcedure : CommandType.Text;

        //See interface for documentation
        public async Task<long?> GetLongScalarAsync(string sql, List<DbParmInfo>? parameters = null, CancellationToken ct = default)
        {
            using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
            oc.Cmd.CommandText = sql;
            oc.Cmd.CommandType = CommandType.Text;
            ToDbParms(parameters, oc.Cmd);
            var result = await oc.Cmd.ExecuteScalarAsync(ct);
            return result != null && result != DBNull.Value ? Convert.ToInt64(result, CultureInfo.InvariantCulture) : null;
        }

        //See interface for documentation
        public long? GetLongScalarSynchronized(string sql, List<DbParmInfo>? parameters = null)
        {
            using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
            oc.Cmd.CommandText = sql;
            oc.Cmd.CommandType = CommandType.Text;
            ToDbParms(parameters, oc.Cmd);
            var result = oc.Cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt64(result, CultureInfo.InvariantCulture) : null;
        }
        #endregion

        #region trunc helpers
        protected string? sTrunc(string? str, int maxLen = 0)
        {
            if (str == null) return null;
            if (maxLen == 0 || maxLen >= str.Length) return str;
            return str[..maxLen];
        }
        protected string sTruncS(string? str, int maxLen = 0)
        {
            if (str == null) return "";
            if (maxLen == 0 || maxLen >= str.Length) return str;
            return str[..maxLen];
        }

        /// <summary>
        /// PostgreSQL timestamp columns may be configured as 'without time zone'. When saving UTC DateTime values into such columns,
        /// we normalize DateTimeKind to Unspecified to avoid provider conversion issues.
        /// </summary>
        protected virtual void NormalizeDateTimeValues()
        {
            if (this.DbType != DatabaseType.PostgreSql)
                return;

            var entries = this.Ctx.ChangeTracker.Entries()
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

        /// <summary>
        /// In case string values exceed max length defined in model, truncate them before saving so we don´t get exceptions from the database for this issue
        /// You can argue that its better to know about it, but in most cases its better to just truncate and move on
        /// </summary>
        protected virtual void TruncateStringValues()
        {
            var entries = this.Ctx.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
            foreach (var entry in entries)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.ClrType == typeof(string) && prop.CurrentValue is string strVal)
                    {
                        var maxLength = prop.Metadata.GetMaxLength();
                        if (maxLength.HasValue && strVal.Length > maxLength.Value)
                        {
                            prop.CurrentValue = sTrunc(strVal, maxLength.Value);
                        }
                    }
                }
            }
        }

        //See interface for documentation
        public IQueryable<T> SetUpRoutineQuery<T>(string schema, string routineName, List<DbParmInfo>? parameters = null) where T : class
        {
            parameters ??= [];
            var conv = new RoutineConvention(DbType);
            var call = conv.Build(schema, routineName, RoutineKind.Query, parameters);
            var cmd = Ctx.Database.GetDbConnection().CreateCommand(); //NoTE: only used for parameter handling so transaction binding the command or open connection is not needed
            var dbParams = parameters
                .Select(p =>
                {
                    var pa = cmd.CreateParameter();
                    pa.ParameterName = p.Name;
                    pa.Value = p.ObjValue ?? DBNull.Value;
                    return pa;
                })
                .ToArray();
            return Ctx.Set<T>().FromSqlRaw(call.Text, dbParams).AsNoTracking();
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing) Ctx.Dispose();
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Class to define database parameter information for routine calls.
    /// </summary>
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

    // Extension method to allow treating a regular DbUow as a read-only unit of work when needed
    // Example: await Helper(uow.AsReadOnly(), ct); --Helper only needs read access, so we can pass a read-only
    public static class DbUowExtensions
    {
        public static IDbReadUow AsReadOnly(this IDbUow uow) => uow;
    }
}
