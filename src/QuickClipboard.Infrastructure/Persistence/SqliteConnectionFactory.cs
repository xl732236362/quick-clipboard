using System.IO;
using Microsoft.Data.Sqlite;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(string databasePath)
{
    public SqliteConnection CreateConnection()
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SqliteConnection($"Data Source={databasePath}");
    }
}
