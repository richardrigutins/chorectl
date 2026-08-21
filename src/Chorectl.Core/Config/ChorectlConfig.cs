namespace Chorectl.Core.Config;

/// <summary>
/// User-tunable preferences persisted at <see cref="ConfigLoader.DefaultPath"/>. Every property
/// carries its resolved default, so a partially-written (or missing) YAML file still yields a
/// fully-populated config.
/// </summary>
public sealed record ChorectlConfig
{
    public List<string> ExcludeRepos { get; init; } = [];
    public bool IncludeForks { get; init; }
    public string MergeMethod { get; init; } = "squash";
    public int MergePollIntervalSeconds { get; init; } = 15;
    public int MergePollTimeoutSeconds { get; init; } = 120;
    public int MaxBackoffSeconds { get; init; } = 300;
    public DefaultSelectConfig DefaultSelect { get; init; } = new();
}

public sealed record DefaultSelectConfig
{
    public bool Patch { get; init; } = true;
    public bool Minor { get; init; } = true;
    public bool Major { get; init; }
    public bool Grouped { get; init; }
}
