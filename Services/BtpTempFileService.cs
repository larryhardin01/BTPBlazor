namespace BTPBlazor.Services;

public sealed class BtpTempFileService
{
    public string ResolveTempDirectory(string? configuredDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        var candidateLocations = new[]
        {
            Environment.GetEnvironmentVariable("TMPDIR"),
            Environment.GetEnvironmentVariable("TEMP"),
            Environment.GetEnvironmentVariable("TMP"),
            Path.GetTempPath()
        };

        foreach (var directory in candidateLocations)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return Path.GetTempPath();
    }

    public async Task<string> WriteMessageAsync(string message, string? directory = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        var resolvedDirectory = ResolveTempDirectory(directory);
        Directory.CreateDirectory(resolvedDirectory);

        var fileName = $"btp-message-{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt";
        var filePath = Path.Combine(resolvedDirectory, fileName);

        await File.WriteAllTextAsync(filePath, message.Trim(), cancellationToken);

        return filePath;
    }

    private const int ChunkSizeBytes = 1024;

    public async Task<string> CopyLargeXmlPayloadAsync(string sourcePath, string? directory = null, IProgress<int>? chunkProgress = null, Action<string>? onTargetFileCreated = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Sample XML payload was not found.", sourcePath);
        }

        var resolvedDirectory = ResolveTempDirectory(directory);
        Directory.CreateDirectory(resolvedDirectory);

        var fileName = $"btp-large-xml-{DateTime.UtcNow:yyyyMMddHHmmssfff}.xml";
        var filePath = Path.Combine(resolvedDirectory, fileName);

        // Step 1: create the empty spillover file.
        await using (File.Create(filePath))
        {
        }

        onTargetFileCreated?.Invoke(filePath);

        // Step 2-3: read the source in 1KB chunks, appending each chunk to the spillover file.
        await using var source = File.OpenRead(sourcePath);
        await using var destination = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None);
        var buffer = new byte[ChunkSizeBytes];
        int bytesRead;
        var chunkNumber = 0;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            chunkNumber++;
            chunkProgress?.Report(chunkNumber);
        }

        // Step 4: close the spillover file once all chunks have been written.
        await destination.FlushAsync(cancellationToken);
        return filePath;
    }
}
