namespace Chorectl.Core.Update;

/// <summary>
/// The sequential steps an in-place update goes through, from checking for a release to replacing the executable.
/// </summary>
public enum UpdateStage
{
    CheckingForRelease,
    Downloading,
    Extracting,
    ReplacingExecutable,
}
