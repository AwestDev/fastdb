using FastDbExplorer.Domain;
using Microsoft.Data.SqlClient;

namespace FastDbExplorer.Infrastructure;

public static class SqlConnectionStringFactory
{
    public static string Create(ConnectionSettings settings, string? database = null)
    {
        if (string.IsNullOrWhiteSpace(settings.Server))
            throw new ArgumentException("Server is required.", nameof(settings));

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = string.IsNullOrWhiteSpace(database) ? "master" : database,
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ApplicationName = "FastDbExplorer",
            ConnectTimeout = 10,
            TrustServerCertificate = settings.TrustServerCertificate
        };

        if (settings.Authentication == AuthenticationMode.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = settings.UserName ?? "";
            builder.Password = settings.Password ?? "";
        }

        return builder.ConnectionString;
    }
}
