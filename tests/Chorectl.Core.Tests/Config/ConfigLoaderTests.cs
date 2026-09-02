using Chorectl.Core.Config;

namespace Chorectl.Core.Tests.Config;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string ConfigPath => Path.Combine(_tempDir, "config.yml");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var loader = new ConfigLoader(ConfigPath);

        var config = loader.Load();

        Assert.Empty(config.ExcludeRepos);
        Assert.False(config.IncludeForks);
        Assert.Equal("squash", config.MergeMethod);
        Assert.Equal(15, config.MergePollIntervalSeconds);
        Assert.Equal(120, config.MergePollTimeoutSeconds);
        Assert.Equal(300, config.MaxBackoffSeconds);
        Assert.False(config.SkipUpdateCheck);
        Assert.True(config.DefaultSelect.Patch);
        Assert.True(config.DefaultSelect.Minor);
        Assert.False(config.DefaultSelect.Major);
        Assert.False(config.DefaultSelect.Grouped);
    }

    [Fact]
    public void Load_WithPartialYaml_MergesRemainingValuesOntoDefaults()
    {
        File.WriteAllText(ConfigPath, "exclude_repos: [foo, bar]\n");

        var config = new ConfigLoader(ConfigPath).Load();

        Assert.Equal(["foo", "bar"], config.ExcludeRepos);
        Assert.Equal("squash", config.MergeMethod);
        Assert.True(config.DefaultSelect.Patch);
    }

    [Fact]
    public void Load_WithPartialNestedYaml_MergesRemainingDefaultSelectValuesOntoDefaults()
    {
        File.WriteAllText(ConfigPath, "default_select:\n  major: true\n");

        var config = new ConfigLoader(ConfigPath).Load();

        Assert.True(config.DefaultSelect.Major);
        Assert.True(config.DefaultSelect.Patch);
        Assert.True(config.DefaultSelect.Minor);
        Assert.False(config.DefaultSelect.Grouped);
    }

    [Fact]
    public void Load_WithExplicitNullExcludeRepos_NormalizesToEmptyList()
    {
        File.WriteAllText(ConfigPath, "exclude_repos:\n");

        var config = new ConfigLoader(ConfigPath).Load();

        Assert.Empty(config.ExcludeRepos);
    }

    [Fact]
    public void Load_WithExplicitNullDefaultSelect_NormalizesToDefaults()
    {
        File.WriteAllText(ConfigPath, "default_select:\n");

        var config = new ConfigLoader(ConfigPath).Load();

        Assert.True(config.DefaultSelect.Patch);
        Assert.True(config.DefaultSelect.Minor);
        Assert.False(config.DefaultSelect.Major);
        Assert.False(config.DefaultSelect.Grouped);
    }

    [Theory]
    [InlineData("merge_method: bogus")]
    [InlineData("merge_poll_interval_seconds: 0")]
    [InlineData("merge_poll_interval_seconds: -5")]
    [InlineData("merge_poll_timeout_seconds: -1")]
    [InlineData("max_backoff_seconds: 0")]
    public void Load_WithAHandEditedInvalidValue_ThrowsArgumentException(string yaml)
    {
        File.WriteAllText(ConfigPath, yaml + "\n");
        var loader = new ConfigLoader(ConfigPath);

        Assert.Throws<ArgumentException>(() => loader.Load());
    }

    [Fact]
    public void SetValue_WhenTheFileHasAnUnrelatedInvalidValue_StillFixesTheGivenKey()
    {
        // config set must stay usable to repair a hand-broken file, one key at a time - it can't
        // require every other key to already be valid first.
        File.WriteAllText(ConfigPath, "merge_method: bogus\nmerge_poll_interval_seconds: 45\n");
        var loader = new ConfigLoader(ConfigPath);

        loader.SetValue("merge_poll_interval_seconds", "60");

        Assert.Equal(60, loader.SetValue("include_forks", "true").MergePollIntervalSeconds);
    }

    [Fact]
    public void SetValue_CanFixTheInvalidKeyItself()
    {
        File.WriteAllText(ConfigPath, "merge_method: bogus\n");
        var loader = new ConfigLoader(ConfigPath);

        var config = loader.SetValue("merge_method", "rebase");

        Assert.Equal("rebase", config.MergeMethod);
        Assert.Equal("rebase", loader.Load().MergeMethod);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var loader = new ConfigLoader(ConfigPath);
        var config = new ChorectlConfig
        {
            ExcludeRepos = ["some-repo"],
            IncludeForks = true,
            MergeMethod = "rebase",
            MergePollIntervalSeconds = 30,
            MergePollTimeoutSeconds = 240,
            MaxBackoffSeconds = 600,
            SkipUpdateCheck = true,
            DefaultSelect = new DefaultSelectConfig { Major = true, Grouped = true },
        };

        loader.Save(config);
        var reloaded = loader.Load();

        Assert.Equal(config.ExcludeRepos, reloaded.ExcludeRepos);
        Assert.Equal(config.IncludeForks, reloaded.IncludeForks);
        Assert.Equal(config.MergeMethod, reloaded.MergeMethod);
        Assert.Equal(config.MergePollIntervalSeconds, reloaded.MergePollIntervalSeconds);
        Assert.Equal(config.MergePollTimeoutSeconds, reloaded.MergePollTimeoutSeconds);
        Assert.Equal(config.MaxBackoffSeconds, reloaded.MaxBackoffSeconds);
        Assert.Equal(config.SkipUpdateCheck, reloaded.SkipUpdateCheck);
        Assert.Equal(config.DefaultSelect, reloaded.DefaultSelect);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "config.yml");
        var loader = new ConfigLoader(nestedPath);

        loader.Save(new ChorectlConfig());

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void SetValue_WithValidKeyAndValue_PersistsEachSupportedKey()
    {
        var loader = new ConfigLoader(ConfigPath);

        loader.SetValue("exclude_repos", "foo,bar");
        loader.SetValue("include_forks", "true");
        loader.SetValue("merge_method", "rebase");
        loader.SetValue("merge_poll_interval_seconds", "30");
        loader.SetValue("merge_poll_timeout_seconds", "240");
        loader.SetValue("max_backoff_seconds", "600");
        loader.SetValue("skip_update_check", "true");
        loader.SetValue("default_select.patch", "false");
        loader.SetValue("default_select.minor", "false");
        loader.SetValue("default_select.major", "true");
        loader.SetValue("default_select.grouped", "true");
        var config = loader.Load();

        Assert.Equal(["foo", "bar"], config.ExcludeRepos);
        Assert.True(config.IncludeForks);
        Assert.Equal("rebase", config.MergeMethod);
        Assert.Equal(30, config.MergePollIntervalSeconds);
        Assert.Equal(240, config.MergePollTimeoutSeconds);
        Assert.Equal(600, config.MaxBackoffSeconds);
        Assert.True(config.SkipUpdateCheck);
        Assert.False(config.DefaultSelect.Patch);
        Assert.False(config.DefaultSelect.Minor);
        Assert.True(config.DefaultSelect.Major);
        Assert.True(config.DefaultSelect.Grouped);
    }

    [Fact]
    public void SetValue_WithUnknownKey_ThrowsArgumentException()
    {
        var loader = new ConfigLoader(ConfigPath);

        var ex = Assert.Throws<ArgumentException>(() => loader.SetValue("not_a_real_key", "value"));
        Assert.Contains("not_a_real_key", ex.Message);
    }

    [Theory]
    [InlineData("merge_method", "bogus")]
    [InlineData("include_forks", "maybe")]
    [InlineData("merge_poll_interval_seconds", "not-a-number")]
    [InlineData("merge_poll_interval_seconds", "0")]
    [InlineData("merge_poll_timeout_seconds", "-1")]
    [InlineData("max_backoff_seconds", "0")]
    [InlineData("skip_update_check", "maybe")]
    [InlineData("default_select.patch", "yes")]
    public void SetValue_WithInvalidValue_ThrowsArgumentException(string key, string value)
    {
        var loader = new ConfigLoader(ConfigPath);

        Assert.Throws<ArgumentException>(() => loader.SetValue(key, value));
    }

    [Fact]
    public void SetValue_WithEmptyExcludeRepos_ClearsTheList()
    {
        var loader = new ConfigLoader(ConfigPath);
        loader.SetValue("exclude_repos", "foo,bar");

        loader.SetValue("exclude_repos", "");
        var config = loader.Load();

        Assert.Empty(config.ExcludeRepos);
    }

    [Fact]
    public void SetValue_OnlyChangesTheGivenKey_LeavesEverythingElseAsIs()
    {
        var loader = new ConfigLoader(ConfigPath);
        loader.SetValue("merge_method", "rebase");

        loader.SetValue("include_forks", "true");
        var config = loader.Load();

        Assert.Equal("rebase", config.MergeMethod);
        Assert.True(config.IncludeForks);
    }
}
