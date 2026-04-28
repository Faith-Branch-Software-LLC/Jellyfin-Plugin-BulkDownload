using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.BulkDownload.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Max total uncompressed bytes per request. 0 = unlimited.</summary>
    public long MaxZipBytes { get; set; } = 50L * 1024 * 1024 * 1024;

    /// <summary>0 = NoCompression (recommended for video/audio), 1 = Fastest, 2 = Optimal.</summary>
    public int CompressionLevel { get; set; } = 0;

    /// <summary>Allow non-admin users to use bulk download.</summary>
    public bool AllowAllUsers { get; set; } = true;
}
