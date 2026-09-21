using BTPBlazor.Services;

namespace BTPBlazor.Tests;

public class TempMessageWriterTests
{
    [Fact]
    public async Task WriteMessageAsync_WritesMessageToRequestedTempDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"btpblazor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BtpTempFileService();

            var filePath = await service.WriteMessageAsync("hello from btp", tempRoot);

            Assert.True(File.Exists(filePath));
            Assert.StartsWith(tempRoot, filePath, StringComparison.Ordinal);
            Assert.Equal("hello from btp", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteMessageAsync_Throws_WhenMessageIsEmpty()
    {
        var service = new BtpTempFileService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.WriteMessageAsync(" ", Path.GetTempPath()));
    }

    [Fact]
    public async Task CopyLargeXmlPayloadAsync_CopiesSourceContentsToRequestedTempDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"btpblazor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var sourcePath = Path.Combine(tempRoot, "source.xml");
        var sourceContents = "<root><value>sample payload</value></root>";
        await File.WriteAllTextAsync(sourcePath, sourceContents);

        try
        {
            var service = new BtpTempFileService();

            var filePath = await service.CopyLargeXmlPayloadAsync(sourcePath, tempRoot);

            Assert.True(File.Exists(filePath));
            Assert.StartsWith(tempRoot, filePath, StringComparison.Ordinal);
            Assert.Equal(sourceContents, await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CopyLargeXmlPayloadAsync_Throws_WhenSourceFileIsMissing()
    {
        var service = new BtpTempFileService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.xml");

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.CopyLargeXmlPayloadAsync(missingPath, Path.GetTempPath()));
    }

    [Fact]
    public async Task CopyLargeXmlPayloadAsync_CopiesContentSpanningMultipleChunks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"btpblazor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var sourcePath = Path.Combine(tempRoot, "source-multi-chunk.xml");
        var sourceBytes = new byte[5000];
        new Random(42).NextBytes(sourceBytes);
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);

        try
        {
            var service = new BtpTempFileService();

            var filePath = await service.CopyLargeXmlPayloadAsync(sourcePath, tempRoot);

            var resultBytes = await File.ReadAllBytesAsync(filePath);
            Assert.Equal(sourceBytes, resultBytes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CopyLargeXmlPayloadAsync_CopiesActual10MbPayloadByteForByte()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "10MB_Payload.xml");

        if (!File.Exists(sourcePath))
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"btpblazor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BtpTempFileService();

            var filePath = await service.CopyLargeXmlPayloadAsync(sourcePath, tempRoot);

            var sourceHash = await ComputeSha256Async(sourcePath);
            var resultHash = await ComputeSha256Async(filePath);
            Assert.Equal(sourceHash, resultHash);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "10MB_Payload.xml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
