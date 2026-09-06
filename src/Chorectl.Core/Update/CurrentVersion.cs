using System.Reflection;

namespace Chorectl.Core.Update;

/// <summary>
/// The running build's version, as set by <c>-p:Version=$VERSION</c> at publish time. Falls back
/// to a placeholder ("0.0.0-dev") for a local dev build that didn't pass that property.
/// </summary>
public static class CurrentVersion
{
    public static string Value { get; } =
        typeof(CurrentVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-dev";
}
