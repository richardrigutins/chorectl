using Chorectl.Core.Audit;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakeAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
