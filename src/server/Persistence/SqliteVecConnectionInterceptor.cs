using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookMyDoctor.Persistence;

// Loads the sqlite-vec extension into every SQLite connection EF Core opens.
public sealed class SqliteVecConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        LoadExtension(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        LoadExtension(connection);
        return Task.CompletedTask;
    }

    private static void LoadExtension(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        sqlite.EnableExtensions();
        sqlite.LoadVector();
        sqlite.EnableExtensions(false);
    }
}
