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

    public async Task<string> CopyLargeXmlPayloadAsync(string sourcePath, string? directory = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Sample XML payload was not found.", sourcePath);
        }

        var resolvedDirectory = ResolveTempDirectory(directory);
        Directory.CreateDirectory(resolvedDirectory);

        var fileName = $"btp-large-xml-{DateTime.UtcNow:yyyyMMddHHmmssfff}.xml";
        var filePath = Path.Combine(resolvedDirectory, fileName);

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(filePath);
        await source.CopyToAsync(destination, cancellationToken);

        return filePath;
    }
}
