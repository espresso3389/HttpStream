using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HttpStreamTests;

public class WebAppFixture : IAsyncLifetime
{
    private readonly WebApplication _webApp;
    private Uri? _uri;

    public WebAppFixture()
    {
        var webAppBuilder = WebApplication.CreateBuilder();
        webAppBuilder.Services.AddResponseCompression();
        webAppBuilder.WebHost.UseUrls("http://127.0.0.1:0");

        _webApp = webAppBuilder.Build();
        _webApp.UseResponseCompression();
        _webApp.MapMethods("/bytes/{size:int}", [HttpMethods.Get, HttpMethods.Head], (int size, bool enableRangeProcessing = true) => TypedResults.File(Enumerable.Repeat(Convert.ToByte('A'), size).ToArray(), "text/plain; charset=utf-8", enableRangeProcessing: enableRangeProcessing));
    }

    /// <summary>
    /// Returns a URI that sends a <c>text/plain</c> response of the character <c>A</c> repeated <paramref name="size"/> times.
    /// </summary>
    /// <param name="size">The size of the response content in bytes.</param>
    /// <param name="enableRangeProcessing">Whether range processing is enabled or not.</param>
    public Uri GetBytesUri(int size, bool enableRangeProcessing = true)
    {
        var baseUri = _uri ?? throw new InvalidOperationException($"The URI is only available after {nameof(IAsyncLifetime.InitializeAsync)} has been called");
        return new Uri(baseUri, $"/bytes/{size}?enableRangeProcessing={enableRangeProcessing}");
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _webApp.StartAsync();

        var server = _webApp.Services.GetRequiredService<IServer>();
        var serverAddress = server.Features.Get<IServerAddressesFeature>() ?? throw new InvalidOperationException($"Could not get the server addresses feature from {server}");
        _uri = new Uri(serverAddress.Addresses.Single());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _webApp.StopAsync();
    }
}
