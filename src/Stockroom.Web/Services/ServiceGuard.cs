using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;

namespace Stockroom.Web.Services;

// Role checks and lock handling shared by the services that write data.
public static class ServiceGuard
{
    public const string ForbiddenMessage = "Your role does not allow this action. Nothing was changed.";
    public const string BusyMessage = "The database is busy. Nothing was changed. Try again.";

    // A failure while committing can leave the outcome unknown, so this message does not claim that nothing changed.
    public const string DatabaseErrorMessage =
        "The change could not be completed because of a database error. Open the record again to check its current state before retrying.";

    // Roles come from the database, not from the caller, so a direct service call cannot claim a role.
    // Queries can embed this as a subquery to decide visibility in the same SQL statement.
    public static IQueryable<IdentityUserRole<string>> RoleMemberships(this AppDbContext db, string userId, string role) =>
        db.UserRoles.Where(ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role));

    public static Task<bool> HasRoleAsync(this AppDbContext db, string userId, string role) =>
        db.RoleMemberships(userId, role).AnyAsync();

    // Records a role refusal as a security event (release security review R-02). The caller still receives only
    // the generic message.
    public static OperationResult Refuse(ILogger securityLog, string actorId, string action, string requirement, int? requestId = null)
    {
        securityLog.PermissionRefused(actorId, action, requirement);
        return new(false, ForbiddenMessage, requestId);
    }

    // Microsoft.Data.Sqlite starts transactions with BEGIN IMMEDIATE and retries a locked database until the
    // command timeout. If the retries run out, the transaction inside the operation is disposed, and therefore
    // rolled back, before the catch runs; the caller receives BusyMessage. Other database failures are logged with
    // their details, and the caller receives DatabaseErrorMessage without them.
    public static async Task<OperationResult> GuardAsync(this AppDbContext db, ILogger logger, Func<Task<OperationResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsBusy(ex))
        {
            db.ChangeTracker.Clear();
            return new(false, BusyMessage);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "A database operation failed.");
            return new(false, DatabaseErrorMessage);
        }
    }

    // SQLITE_BUSY (5) and SQLITE_LOCKED (6) remain after the provider's retries run out.
    private static bool IsBusy(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqliteException { SqliteErrorCode: 5 or 6 })
            {
                return true;
            }
        }
        return false;
    }
}
