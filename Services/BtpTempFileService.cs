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
}
