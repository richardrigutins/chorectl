namespace Chorectl.Core.Audit;

/// <summary>
/// Records <see cref="AuditEntry"/> entries for Dependabot actions.
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
