using System.Net;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

/// <summary>
/// Builds the minimal Octokit <see cref="IResponse"/> the exception types thrown by
/// <c>Octokit*</c> adapters need to construct, so adapter tests can simulate a given HTTP status
/// without a real network round-trip.
/// </summary>
internal static class OctokitTestDoubles
{
    public static IResponse FakeResponse(HttpStatusCode statusCode)
    {
        // RateLimitExceededException's constructor reads ApiInfo.RateLimit to populate its own
        // Limit/Remaining/Reset properties, so it needs a populated ApiInfo even when the status
        // being simulated isn't actually a rate-limit response.
        var rateLimit = new RateLimit(limit: 5000, remaining: 0, resetAsUtcEpochSeconds: DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
        var apiInfo = new ApiInfo(
            new Dictionary<string, Uri>(),
            [],
            [],
            etag: string.Empty,
            rateLimit,
            serverTimeDifference: TimeSpan.Zero);

        var response = Substitute.For<IResponse>();
        response.StatusCode.Returns(statusCode);
        response.Headers.Returns(new Dictionary<string, string>());
        response.Body.Returns(string.Empty);
        response.ApiInfo.Returns(apiInfo);
        return response;
    }
}
