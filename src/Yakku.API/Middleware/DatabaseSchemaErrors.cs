using Npgsql;

namespace Yakku.API.Middleware
{
    /// <summary>
    /// Recognizes the database being behind the deployed code (a migration not applied yet), so the API can
    /// answer "being updated, try again" instead of a bare 500.
    /// </summary>
    public static class DatabaseSchemaErrors
    {
        /// <summary>True when the exception, or any inner exception, is a missing table or column.</summary>
        public static bool IsSchemaMismatch(Exception? exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
