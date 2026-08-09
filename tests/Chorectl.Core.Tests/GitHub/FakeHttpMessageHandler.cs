using System.Net;

namespace Chorectl.Core.Tests.GitHub;

internal sealed class FakeHttpMessageHandler(params string[] jsonResponses) : HttpMessageHandler
{
    private readonly Queue<string> responses = new(jsonResponses);

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        var json = responses.Count > 0 ? responses.Dequeue() : "{}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
