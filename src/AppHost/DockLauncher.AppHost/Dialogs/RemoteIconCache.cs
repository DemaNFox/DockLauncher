using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;

namespace DockLauncher.AppHost.Dialogs;

public interface IRemoteIconCache
{
    bool IsRemoteIconUrl(string? value);

    Task<string> CacheAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class RemoteIconCache : IRemoteIconCache
{
    private const int IconPixelSize = 50;
    private const int MaxDownloadBytes = 4 * 1024 * 1024;

    private readonly AppDataPaths _paths;
    private readonly HttpClient _httpClient;

    public RemoteIconCache(AppDataPaths paths)
    {
        _paths = paths;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    public bool IsRemoteIconUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    public async Task<string> CacheAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = url.Trim();
        var cacheDirectory = Path.Combine(_paths.IconsDirectory, ".thumbnails");
        Directory.CreateDirectory(cacheDirectory);

        var cachePath = Path.Combine(cacheDirectory, $"{HashUrl(normalizedUrl)}.png");
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var limitedStream = await CopyToLimitedMemoryStreamAsync(responseStream, cancellationToken);
        limitedStream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = IconPixelSize;
        bitmap.DecodePixelHeight = IconPixelSize;
        bitmap.StreamSource = limitedStream;
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        await using var output = File.Create(cachePath);
        encoder.Save(output);
        return cachePath;
    }

    private static async Task<MemoryStream> CopyToLimitedMemoryStreamAsync(Stream source, CancellationToken cancellationToken)
    {
        var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return destination;
            }

            if (destination.Length + read > MaxDownloadBytes)
            {
                throw new InvalidOperationException("Remote icon is too large.");
            }

            destination.Write(buffer, 0, read);
        }
    }

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
