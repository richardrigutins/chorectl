using Chorectl.Core.Audit;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakeAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    /// <summary>When set, every <see cref="RecordAsync"/> call throws this instead of recording.</summary>
    public Exception? FailWith { get; set; }

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
