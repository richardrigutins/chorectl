using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Chorectl.Core.Config;

/// <summary>
/// Reads and writes <see cref="ChorectlConfig"/> as YAML at a given path (normally
/// <see cref="DefaultPath"/>, the XDG-style <c>~/.config/chorectl/config.yml</c>). Missing keys -
/// or a missing file entirely - resolve to <see cref="ChorectlConfig"/>'s built-in defaults.
/// </summary>
public sealed class ConfigLoader(string path)
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>
    /// The XDG-style default config path: <c>$XDG_CONFIG_HOME/chorectl/config.yml</c>, falling
    /// back to <c>~/.config/chorectl/config.yml</c> when that variable isn't set.
    /// </summary>
    public static string DefaultPath { get; } = ResolveDefaultPath();

    public ChorectlConfig Load()
    {
        if (!File.Exists(path))
        {
            return new ChorectlConfig();
        }

        var yaml = File.ReadAllText(path);
        return Deserializer.Deserialize<ChorectlConfig>(yaml) ?? new ChorectlConfig();
    }

    public void Save(ChorectlConfig config)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serializer.Serialize(config));
    }

    /// <summary>
    /// Validates <paramref name="key"/>/<paramref name="value"/>, persists the change, and
    /// returns the updated config. Throws <see cref="ArgumentException"/> for an unknown key or a
    /// value that fails validation for that key - never a silent no-op.
    /// </summary>
    public ChorectlConfig SetValue(string key, string value)
    {
        var config = Load();
        var updated = key switch
        {
            "exclude_repos" => config with { ExcludeRepos = ParseRepoList(value) },
            "include_forks" => config with { IncludeForks = ParseBool(key, value) },
            "merge_method" => config with { MergeMethod = ParseMergeMethod(value) },
            "merge_poll_interval_seconds" => config with { MergePollIntervalSeconds = ParsePositiveInt(key, value) },
            "merge_poll_timeout_seconds" => config with { MergePollTimeoutSeconds = ParsePositiveInt(key, value) },
            "max_backoff_seconds" => config with { MaxBackoffSeconds = ParsePositiveInt(key, value) },
            "default_select.patch" => config with { DefaultSelect = config.DefaultSelect with { Patch = ParseBool(key, value) } },
            "default_select.minor" => config with { DefaultSelect = config.DefaultSelect with { Minor = ParseBool(key, value) } },
            "default_select.major" => config with { DefaultSelect = config.DefaultSelect with { Major = ParseBool(key, value) } },
            "default_select.grouped" => config with { DefaultSelect = config.DefaultSelect with { Grouped = ParseBool(key, value) } },
            _ => throw new ArgumentException($"Unknown config key '{key}'."),
        };

        Save(updated);
        return updated;
    }

    private static List<string> ParseRepoList(string value) =>
        [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static bool ParseBool(string key, string value) =>
        bool.TryParse(value, out var result)
            ? result
            : throw new ArgumentException($"Invalid value '{value}' for '{key}' - expected 'true' or 'false'.");

    private static string ParseMergeMethod(string value) =>
        value.ToLowerInvariant() switch
        {
            "squash" or "merge" or "rebase" => value.ToLowerInvariant(),
            _ => throw new ArgumentException($"Invalid value '{value}' for 'merge_method' - expected 'squash', 'merge', or 'rebase'."),
        };

    private static int ParsePositiveInt(string key, string value) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : throw new ArgumentException($"Invalid value '{value}' for '{key}' - expected a positive whole number of seconds.");

    private static string ResolveDefaultPath()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrEmpty(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        return Path.Combine(configHome, "chorectl", "config.yml");
    }
}
