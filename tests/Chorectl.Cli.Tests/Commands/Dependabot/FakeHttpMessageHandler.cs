using System.Net;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakeHttpMessageHandler(params string[] jsonResponses) : HttpMessageHandler
{
    private readonly Queue<string> responses = new(jsonResponses);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var json = responses.Count > 0 ? responses.Dequeue() : "{}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
