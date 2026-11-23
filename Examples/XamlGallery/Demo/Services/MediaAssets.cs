using System.IO;
namespace Demo;

/// <summary>Extracts the embedded demo media into a per-user temp folder once, then returns its path.</summary>
internal static class MediaAssets
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "EaselGalleryMedia");

    /// <summary>Returns a local file for an embedded media resource such as <c>aurora-drift.mp3</c>.</summary>
    /// <param name="fileName">The file name under <c>Assets/Media</c>.</param>
    public static string Resolve(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        lock (Gate)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return path;
            }

            Directory.CreateDirectory(Folder);
            var resourceName = $"Demo.Assets.Media.{fileName}";
            using var source = typeof(MediaAssets).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded media '{resourceName}' was not found.");
            using var target = File.Create(path);
            source.CopyTo(target);
        }

        return path;
    }
}
