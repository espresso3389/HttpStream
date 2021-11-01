HttpStream
==========
This C# project implements randomly accessible stream on HTTP 1.1 transport.
Typically, HttpClient provides almost all HTTP features but it just provides us with a way to download a file completely.
The implementation takes advantage of HTTP 1.1 range access or on fallback case, it uses HTTP/1.0 sequential download anyway.

## Source Code

The source code is available at [GitHub](https://github.com/espresso3389/HttpStream).

## NuGet Package

A prebuilt NuGet package is available: [Espresso3389.HttpStream](https://www.nuget.org/packages/Espresso3389.HttpStream/).

To install HttpStream, run the following command in the Package Manager Console:
```
PM> Install-Package Espresso3389.HttpStream
```

## Supported Platforms

This module is built against .NET Platform Standard 2.0 (`netstandard2.0`).
For compatible .NET implementations, see [.NET Standard - .NET implementation support](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support).

### Simple Usage

```cs
using Espresso3389.HttpStream;

// cache stream
var fs = File.Create("cache.jpg");

// The third parameter, true indicates that the httpStream will close the cache stream.
var uri = new Uri("https://example.com/somewhere/some-large-image.jpg");
var cacheSize = 1024 * 64;
var httpStream = await HttpStream.CreateAsync(uri, fs, true, cacheSize, null);


// RangeDownloaded is called on every incremental download
httpStream.RangeDownloaded += (sender, e) =>
{
  Console.WriteLine("Progress: {0}%", (int)(100 * httpStream.CachedRatio));
};

// The following code actually invokes download whole the file
// You can use BufferedStream to improve I/O performance.
var bmp = await BitmapFactory.DecodeStreamAsync(new BufferedStream(httpStream, cacheSize));
```

## License

The codes/binaries are licensed under [MIT License](https://github.com/espresso3389/HttpStream/blob/master/LICENSE).
