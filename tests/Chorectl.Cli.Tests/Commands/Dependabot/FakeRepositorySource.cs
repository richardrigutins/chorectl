using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakeRepositorySource : IRepositorySource
{
    private readonly IReadOnlyList<RepositoryInfo> repositories;

    public FakeRepositorySource(params RepositoryInfo[] repositories) => this.repositories = repositories;

    public Task<IReadOnlyList<RepositoryInfo>> GetOwnedRepositoriesAsync() => Task.FromResult(repositories);
}
