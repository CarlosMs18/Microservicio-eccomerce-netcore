// En Catalog.Infrastructure/Helpers/DbConnectionHelper.cs
public static class DbConnectionHelper
{
    public static string GetServerName(string connectionString)
    {
        try
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            return builder.TryGetValue("Server", out var value) ?
                   value.ToString()! : "Desconocido";
        }
        catch
        {
            return "Indeterminado";
        }
    }

    public static string GetDatabaseName(string connectionString)
    {
        try
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
            return builder.TryGetValue("Database", out var value) ? value.ToString()! : "Desconocido";
        }
            catch
        {
            return "Indeterminado";
        }
    }
}