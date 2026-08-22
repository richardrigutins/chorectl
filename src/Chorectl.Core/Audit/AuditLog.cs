using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chorectl.Core.Audit;

/// <summary>
/// Appends one JSON line per Dependabot action to the audit log file (normally
/// <see cref="DefaultPath"/>, the XDG-style <c>~/.local/share/chorectl/audit.log</c>). Append-only -
/// entries are never rewritten or pruned.
/// </summary>
public sealed class AuditLog(string path) : IAuditLog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The XDG-style default audit log path: <c>$XDG_DATA_HOME/chorectl/audit.log</c>, falling back
    /// to <c>~/.local/share/chorectl/audit.log</c> when that variable isn't set.
    /// </summary>
    public static string DefaultPath { get; } = ResolveDefaultPath();

    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(entry, SerializerOptions);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
    }

    private static string ResolveDefaultPath()
    {
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = string.IsNullOrEmpty(xdgDataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdgDataHome;

        return Path.Combine(dataHome, "chorectl", "audit.log");
    }
}
