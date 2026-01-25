using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.DbRepo
{
    internal class CmdHelper
    {
        internal readonly struct Opened : IDisposable, IAsyncDisposable
        {
            public DbConnection Conn { get; }
            public DbCommand Cmd { get; }

            public Opened(DbConnection conn, DbCommand cmd)
            {
                Conn = conn;
                Cmd = cmd;
            }

            public void Dispose() => Cmd.Dispose();
            public ValueTask DisposeAsync()
            {
                Cmd.Dispose();
                return ValueTask.CompletedTask;
            }
        }

        public static Opened Open(DbContext ctx)
        {
            ctx.Database.OpenConnection(); // no-op if already open; fires interceptor if it opens
            var conn = ctx.Database.GetDbConnection();
            var cmd = conn.CreateCommand();
            return new Opened(conn, cmd);
        }

        public async static Task<Opened> OpenAsync(DbContext ctx, CancellationToken ct)
        {
            await ctx.Database.OpenConnectionAsync(ct); // goes through EF => interceptor fires
            var conn = ctx.Database.GetDbConnection();
            var cmd = conn.CreateCommand();
            return new Opened(conn, cmd);
        }
    }
}
