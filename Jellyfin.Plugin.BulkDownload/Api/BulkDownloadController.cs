using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BulkDownload.Api;

[ApiController]
[Route("BulkDownload")]
[Authorize]
public class BulkDownloadController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<BulkDownloadController> _logger;

    public BulkDownloadController(ILibraryManager libraryManager, ILogger<BulkDownloadController> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>Download arbitrary items by ID as a ZIP.</summary>
    [HttpGet("zip")]
    public Task GetZipByIds([FromQuery] string ids, CancellationToken cancellationToken)
    {
        var guids = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => Guid.Parse(s.Trim()));

        var items = guids
            .Select(id => _libraryManager.GetItemById(id))
            .Where(item => item is not null && !string.IsNullOrEmpty(item!.Path))
            .Select(item => item!)
            .ToList();

        return StreamZipAsync("bulk-download.zip", items, flatNames: true, cancellationToken);
    }

    /// <summary>Download a full playlist as a ZIP.</summary>
    [HttpGet("playlist/{id}/zip")]
    public Task GetPlaylistZip(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var playlist = _libraryManager.GetItemById(id);
            if (playlist is null)
            {
                _logger.LogWarning("BulkDownload: playlist {Id} not found", id);
                return WriteNotFound();
            }

            var items = GetChildren(id)
                .Where(i => !string.IsNullOrEmpty(i.Path))
                .ToList();

            _logger.LogInformation("BulkDownload: playlist {Name} — {Count} items", playlist.Name, items.Count);
            return StreamZipAsync($"{Sanitize(playlist.Name)}.zip", items, flatNames: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BulkDownload: playlist {Id} failed", id);
            throw;
        }
    }

    /// <summary>Download a full TV series as a ZIP.</summary>
    [HttpGet("series/{id}/zip")]
    public Task GetSeriesZip(Guid id, CancellationToken cancellationToken)
    {
        var series = _libraryManager.GetItemById(id) as Series;
        if (series is null)
            return WriteNotFound();

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            AncestorIds = new[] { id },
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true,
        })
        .Where(i => !string.IsNullOrEmpty(i.Path))
        .ToList();

        return StreamZipAsync(
            $"{Sanitize(series.Name)}.zip", items,
            flatNames: false, cancellationToken, rootName: series.Name);
    }

    /// <summary>Download a single TV season as a ZIP.</summary>
    [HttpGet("season/{id}/zip")]
    public Task GetSeasonZip(Guid id, CancellationToken cancellationToken)
    {
        var season = _libraryManager.GetItemById(id) as Season;
        if (season is null)
            return WriteNotFound();

        var seriesName = season.SeriesName ?? "Unknown Series";

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = id,
            IncludeItemTypes = new[] { BaseItemKind.Episode },
        })
        .Where(i => !string.IsNullOrEmpty(i.Path))
        .ToList();

        return StreamZipAsync(
            $"{Sanitize(seriesName)} - {Sanitize(season.Name ?? "Season")}.zip",
            items, flatNames: false, cancellationToken, rootName: seriesName);
    }

    /// <summary>Download a music album or audiobook as a ZIP.</summary>
    [HttpGet("album/{id}/zip")]
    public Task GetAlbumZip(Guid id, CancellationToken cancellationToken)
    {
        var album = _libraryManager.GetItemById(id);
        if (album is null)
            return WriteNotFound();

        var items = GetChildren(id)
            .Where(i => !string.IsNullOrEmpty(i.Path))
            .ToList();

        return StreamZipAsync($"{Sanitize(album.Name)}.zip", items, flatNames: true, cancellationToken);
    }

    private async Task StreamZipAsync(
        string zipFileName,
        List<BaseItem> items,
        bool flatNames,
        CancellationToken cancellationToken,
        string? rootName = null)
    {
        if (items.Count == 0)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var config = Plugin.Instance!.Configuration;
        if (config.MaxZipBytes > 0)
        {
            long totalBytes = items.Sum(i => i.Size ?? 0);
            if (totalBytes > config.MaxZipBytes)
            {
                Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/zip";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{zipFileName}\"");

        // Disable buffering so bytes flow to the client incrementally.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var compressionLevel = config.CompressionLevel switch
        {
            0 => CompressionLevel.NoCompression,
            1 => CompressionLevel.Fastest,
            _ => CompressionLevel.Optimal,
        };

        using (var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var entryPath = flatNames
                    ? Path.GetFileName(item.Path)
                    : BuildHierarchyPath(item, rootName);

                _logger.LogDebug("BulkDownload: adding {Path} as {Entry}", item.Path, entryPath);

                try
                {
                    var entry = archive.CreateEntry(entryPath!, compressionLevel);
                    if (item.DateCreated != default)
                        entry.LastWriteTime = new DateTimeOffset(item.DateCreated);

                    await using var fileStream = new FileStream(
                        item.Path!, FileMode.Open, FileAccess.Read,
                        FileShare.Read, bufferSize: 81920, useAsync: true);
                    await using var entryStream = entry.Open();
                    await fileStream.CopyToAsync(entryStream, 81920, cancellationToken);
                }
                catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "BulkDownload: skipping inaccessible file {Path}", item.Path);
                }
            }
        }

        await Response.Body.FlushAsync(cancellationToken);
    }

    private static string BuildHierarchyPath(BaseItem item, string? rootName)
    {
        if (item is Episode ep)
        {
            var show   = Sanitize(ep.SeriesName ?? rootName ?? "Series");
            var season = Sanitize(ep.Season?.Name ?? "Season");
            var file   = Path.GetFileName(ep.Path);
            return $"{show}/{season}/{file}";
        }

        if (item is Audio audio)
        {
            var album = Sanitize(audio.Album ?? rootName ?? "Album");
            return $"{album}/{Path.GetFileName(audio.Path)}";
        }

        return Path.GetFileName(item.Path) ?? item.Name;
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars()));

    private IEnumerable<BaseItem> GetChildren(Guid parentId) =>
        _libraryManager.GetItemList(new InternalItemsQuery { ParentId = parentId });

    private Task WriteNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
