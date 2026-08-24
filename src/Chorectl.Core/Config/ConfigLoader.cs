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

    /// <exception cref="ArgumentException">
    /// The file deserializes but a value fails the same validation <see cref="SetValue"/> applies
    /// (e.g. a hand-edited <c>merge_method: bogus</c> or a negative <c>merge_poll_interval_seconds</c>).
    /// <see cref="SetValue"/> can never write such a file since it validates before saving - this
    /// only catches a file edited by hand outside chorectl. <see cref="SetValue"/> itself reads the
    /// file via <see cref="LoadRaw"/> instead, deliberately skipping this check - fixing one bad
    /// key by hand can't require every other key to already be valid, or `config set` would stop
    /// being the escape hatch it's meant to be for a broken file.
    /// </exception>
    public ChorectlConfig Load()
    {
        var config = LoadRaw();

        ValidateMergeMethod(config.MergeMethod);
        ValidatePositiveInt(config.MergePollIntervalSeconds, "merge_poll_interval_seconds");
        ValidatePositiveInt(config.MergePollTimeoutSeconds, "merge_poll_timeout_seconds");
        ValidatePositiveInt(config.MaxBackoffSeconds, "max_backoff_seconds");

        return config;
    }

    /// <summary>
    /// Reads the config file without validating it - only normalizing an explicit YAML null back
    /// to each field's default (see <see cref="Load"/> for why). Used by <see cref="SetValue"/>,
    /// which only needs the other, unrelated keys' current values to build the updated record; the
    /// one key actually being set is validated on its own regardless.
    /// </summary>
    private ChorectlConfig LoadRaw()
    {
        if (!File.Exists(path))
        {
            return new ChorectlConfig();
        }

        var yaml = File.ReadAllText(path);
        var config = Deserializer.Deserialize<ChorectlConfig>(yaml) ?? new ChorectlConfig();

        // YamlDotNet assigns an explicit YAML null straight through, overriding the record's own
        // default initializer (which only kicks in for a key that's missing entirely) - normalize
        // back to defaults so "exclude_repos:" or "default_select:" with nothing after the colon
        // still yields the fully-populated config the type's own doc comment promises.
        return config with
        {
            ExcludeRepos = config.ExcludeRepos ?? [],
            MergeMethod = config.MergeMethod ?? "squash",
            DefaultSelect = config.DefaultSelect ?? new DefaultSelectConfig(),
        };
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
        var config = LoadRaw();
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

    private static string ParseMergeMethod(string value)
    {
        var normalized = value.ToLowerInvariant();
        ValidateMergeMethod(normalized);
        return normalized;
    }

    private static void ValidateMergeMethod(string value)
    {
        if (value is not ("squash" or "merge" or "rebase"))
        {
            throw new ArgumentException($"Invalid value '{value}' for 'merge_method' - expected 'squash', 'merge', or 'rebase'.");
        }
    }

    private static int ParsePositiveInt(string key, string value)
    {
        if (!int.TryParse(value, out var result))
        {
            throw new ArgumentException($"Invalid value '{value}' for '{key}' - expected a positive whole number of seconds.");
        }

        ValidatePositiveInt(result, key);
        return result;
    }

    private static void ValidatePositiveInt(int value, string key)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"Invalid value '{value}' for '{key}' - expected a positive whole number of seconds.");
        }
    }

    private static string ResolveDefaultPath()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrEmpty(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        return Path.Combine(configHome, "chorectl", "config.yml");
    }
}
