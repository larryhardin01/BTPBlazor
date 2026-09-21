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
}
