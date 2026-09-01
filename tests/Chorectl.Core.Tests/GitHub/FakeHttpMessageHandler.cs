using System.Net;

namespace Chorectl.Core.Tests.GitHub;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Json)> responses;

    public FakeHttpMessageHandler(params string[] jsonResponses) =>
        responses = new(jsonResponses.Select(json => (HttpStatusCode.OK, json)));

    private FakeHttpMessageHandler(IEnumerable<(HttpStatusCode Status, string Json)> responses) =>
        this.responses = new(responses);

    public List<string> RequestBodies { get; } = [];

    /// <summary>Queues responses with an explicit status per response, e.g. a 429 followed by a 200.</summary>
    public static FakeHttpMessageHandler WithStatuses(params (HttpStatusCode Status, string Json)[] responses) => new(responses);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        var (status, json) = responses.Count > 0 ? responses.Dequeue() : (HttpStatusCode.OK, "{}");
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
