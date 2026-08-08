using System.ComponentModel;
using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _responses = [];

    public bool ThrowGhNotFound { get; set; }

    public void SetResponse(string arguments, ProcessResult result) => _responses[arguments] = result;

    public ProcessResult Run(string fileName, string arguments)
    {
        if (ThrowGhNotFound)
        {
            throw new Win32Exception(2, "The system cannot find the file specified");
        }

        return _responses.TryGetValue(arguments, out var result)
            ? result
            : throw new InvalidOperationException($"FakeProcessRunner has no response set up for '{fileName} {arguments}'.");
    }
}
