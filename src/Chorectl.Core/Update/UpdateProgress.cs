namespace Chorectl.Core.Update;

/// <summary>
/// A progress update reported during <see cref="Updater.UpdateAsync"/>. <paramref name="BytesDownloaded"/>
/// and <paramref name="TotalBytes"/> are only populated for <see cref="UpdateStage.Downloading"/>;
/// <paramref name="TotalBytes"/> is null when the response has no Content-Length header.
/// </summary>
public readonly record struct UpdateProgress(UpdateStage Stage, long? BytesDownloaded = null, long? TotalBytes = null);
