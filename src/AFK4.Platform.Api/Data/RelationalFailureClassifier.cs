using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AFK4.Platform.Api.Data;

internal static class RelationalFailureClassifier
{
    public static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Same as <see cref="IsUniqueViolation(Exception)"/> but scoped to a specific unique
    /// index/constraint name, so a caller that only knows how to recover from one particular
    /// collision (e.g. retrying a number allocation) does not swallow an unrelated unique-constraint
    /// violation on the same table and misreport it as that recoverable conflict.</summary>
    public static bool IsUniqueViolation(Exception exception, string constraintName)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
                && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task RollbackIfActiveAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Providers such as Npgsql complete the transaction before surfacing a
            // commit-phase serialization failure. There is nothing left to roll back.
        }
    }
}
