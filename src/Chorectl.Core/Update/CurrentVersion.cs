using System.Reflection;

namespace Chorectl.Core.Update;

/// <summary>
/// The running build's version, as set by <c>-p:Version=$VERSION</c> at publish time. Falls back
/// to the SDK's own default ("1.0.0") for a local dev build that didn't pass that property.
/// </summary>
public static class CurrentVersion
{
    public static string Value { get; } =
        typeof(CurrentVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "1.0.0";
}
