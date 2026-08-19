using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

internal sealed class FakeRepositorySource : IRepositorySource
{
    private readonly IReadOnlyList<RepositoryInfo> repositories;

    public FakeRepositorySource(params RepositoryInfo[] repositories) => this.repositories = repositories;

    public bool GetOwnedRepositoriesAsyncWasCalled { get; private set; }

    public Task<IReadOnlyList<RepositoryInfo>> GetOwnedRepositoriesAsync()
    {
        GetOwnedRepositoriesAsyncWasCalled = true;
        return Task.FromResult(repositories);
    }

    public Task<RepositoryInfo?> GetOwnedRepositoryAsync(string name) =>
        Task.FromResult(repositories.FirstOrDefault(r => r.Name == name));
}
