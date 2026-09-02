namespace Chorectl.Cli.Tests.Commands;

internal sealed class FakeBinaryHttpMessageHandler(byte[] content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content),
        });
}
