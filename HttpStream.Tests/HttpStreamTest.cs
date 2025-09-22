using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Espresso3389.HttpStream;
using Xunit;

namespace HttpStreamTests;

public class HttpStreamTest(WebAppFixture webApp) : IClassFixture<WebAppFixture>
{
    [Theory]
    [InlineData(32 * 1024 - 1)]
    [InlineData(32 * 1024)] // DefaultCachePageSize
    [InlineData(32 * 1024 + 1)]
    public async Task TestSize(int size)
    {
        var uri = webApp.GetBytesUri(size);
        await using var httpStream = await HttpStream.CreateAsync(uri);

        httpStream.IsStreamLengthAvailable.Should().BeTrue();
        httpStream.Length.Should().Be(size);

        using var memoryStream = new MemoryStream(size);
        await httpStream.CopyToAsync(memoryStream);
        memoryStream.Length.Should().Be(size);
        memoryStream.ToArray().Should().OnlyContain(b => b == 'A');
    }

    [Theory]
    [InlineData(DecompressionMethods.None)]
    [InlineData(DecompressionMethods.All)]
    public async Task TestDecompressionMethod(DecompressionMethods automaticDecompression)
    {
        const int size = 100;

        var uri = webApp.GetBytesUri(size);
        using var httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = automaticDecompression });
        await using var httpStream = await HttpStream.CreateAsync(uri, cache: new MemoryStream(), ownStream: true, cachePageSize: 32 * 1024, cached: null, httpClient);

        httpStream.IsStreamLengthAvailable.Should().Be(automaticDecompression == DecompressionMethods.None);

        using var memoryStream = new MemoryStream(size);
        await httpStream.CopyToAsync(memoryStream);
        memoryStream.Length.Should().Be(size);
        memoryStream.ToArray().Should().OnlyContain(b => b == 'A');
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestSeek(bool enableRangeProcessing)
    {
        var uri = webApp.GetBytesUri(100, enableRangeProcessing);
        await using var httpStream = await HttpStream.CreateAsync(uri, cache: new MemoryStream(), ownStream: true, cachePageSize: 32, cached: null);

        httpStream.Seek(40, SeekOrigin.Begin);

        using var memoryStream = new MemoryStream();
        await httpStream.CopyToAsync(memoryStream);
        memoryStream.Length.Should().Be(60);
        memoryStream.ToArray().Should().OnlyContain(b => b == 'A');
    }

    [Fact]
    public async Task TestNotFound()
    {
        var uri = new Uri(webApp.GetBytesUri(100).AbsoluteUri.Replace("/bytes/", "/not_found/"));
        await using var httpStream = await HttpStream.CreateAsync(uri);

        var action = () => httpStream.CopyToAsync(Stream.Null);

        (await action.Should().ThrowExactlyAsync<HttpRequestException>()).WithMessage("Response status code does not indicate success for bytes=0-32767: 404 (Not Found).");
    }
}
