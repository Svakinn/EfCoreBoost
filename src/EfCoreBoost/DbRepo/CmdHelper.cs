using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;

namespace EfCore.Boost.DbRepo
{
    internal static class CmdHelper
    {
        /// <summary>
        /// Carries connection and command
        /// The important part is that when disposed, the connection is not destroyed (being the DbContext connection)
        /// However the Command is destroyed.
        /// I.e. using var oc = CmdHelper.OpenCmdSynchronized(Ctx)  is quite safe to use "using"
        /// </summary>
        internal readonly struct Opened(DbConnection conn, DbCommand cmd) : IDisposable, IAsyncDisposable
        {
            public DbConnection Conn { get; } = conn;
            public DbCommand Cmd { get; } = cmd;

            public void Dispose() => Cmd.Dispose();
            public ValueTask DisposeAsync()
            {
                Cmd.Dispose();
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>
        /// Returns the DbContexts connection, properly opened when needed
        /// This is important since we want to make sure on-connected triggers from EF fire appropriate
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>
        /// Dispose-save structure with the command and connection, meaning that the command gets destroyed,
        /// but not the connection that belongs to the DbContext
        /// </returns>
        public static Opened OpenCmdSynchronized(DbContext ctx)
        {
            ctx.Database.OpenConnection(); // no-op if already open; fires interceptor if it opens
            var conn = ctx.Database.GetDbConnection();
            var cmd = conn.CreateCommand();
            var tx = ctx.Database.CurrentTransaction;
            if (tx != null)
                cmd.Transaction = tx.GetDbTransaction();
            return new Opened(conn, cmd);
        }

        /// <summary>
        /// Ensures the connection is properly opened through EF
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public static DbConnection OpenConnectionSynchronized(DbContext ctx)
        {
            ctx.Database.OpenConnection(); // no-op if already open; fires interceptor if it opens
            return ctx.Database.GetDbConnection();
        }

        /// <summary>
        /// Gives us proper command on properly open connection on our DbContext
        /// If transaction is active on the connection, that one gets hoked to our command
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>
        /// Dispose-save structure with the command and connection, meaning that the command gets destroyed,
        /// but not the connection that belongs to the DbContext
        /// </returns>
        public static async Task<Opened> OpenCmdAsync(DbContext ctx, CancellationToken ct)
        {
            await ctx.Database.OpenConnectionAsync(ct); // goes through EF => interceptor fires
            var conn = ctx.Database.GetDbConnection();
            var cmd = conn.CreateCommand();
            var tx = ctx.Database.CurrentTransaction;
            if (tx != null)
                cmd.Transaction = tx.GetDbTransaction();
            return new Opened(conn, cmd);
        }

        /// <summary>
        /// Returns the DbContexts connection, properly opened when needed
        /// This is important since we want to make sure on-connected triggers from EF fire appropriately
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns></returns>
        public static async Task<DbConnection> OpenConnectionAsync(DbContext ctx, CancellationToken ct)
        {
            await ctx.Database.OpenConnectionAsync(ct); // goes through EF => interceptor fires when needed
            return ctx.Database.GetDbConnection();
        }
    }
}
