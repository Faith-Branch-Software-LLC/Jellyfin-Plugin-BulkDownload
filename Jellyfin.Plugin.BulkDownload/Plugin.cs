using System;
using System.Collections.Generic;
using Jellyfin.Plugin.BulkDownload.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.BulkDownload;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid StaticId = new Guid("6c023404-f107-4b93-9253-148b088d4c9b");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Bulk Download";
    public override Guid Id => StaticId;
    public override string Description => "Download playlists, series, seasons, and audiobooks as a single ZIP file.";

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "BulkDownload",
            EmbeddedResourcePath = $"{GetType().Namespace}.Web.configPage.html"
        };
    }
}
