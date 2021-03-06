HttpStream
==========
This C# project implements randomly accessible stream on HTTP 1.1 transport.
Typically, HttpClient provides almost all HTTP features but it just provides us with a way to download a file completely.
The implementation take advantage of HTTP 1.1 range access or on fallback case, it uses HTTP/1.0 sequential download anyway.

## Source Code

The source code is available at [GitHub](https://github.com/espresso3389/HttpStream).

## NuGet Package
A prebuilt NuGet package is available: [HttpStream](https://www.nuget.org/packages/HttpStream/).

To install HttpStream, run the following command in the Package Manager Console:
```
PM> Install-Package HttpStream
```

## Supported Platforms
This module is built against .NET Platform Standard 1.6 (`netstandard1.6`) and it's compatible with the following platforms:

- .NET Core 1.0
- .NET Framework 4.6.1
- Universal Windows Platform 10.0.16299
- Mono 4.6
- Xamarin.iOS 10.0
- Xamarin.Mac 3.0
- Xamarin.Android 7.0

For more information, see the illustration on [Mapping the .NET Platform Standard to platforms - .NET Platform Standard](https://github.com/dotnet/corefx/blob/master/Documentation/architecture/net-platform-standard.md#mapping-the-net-platform-standard-to-platforms).

### Simple Usage
```cs
using Espresso3389.HttpStream;

// cache stream
var fs = File.Create("cache.jpg");

// The third parameter, true indicates that the httpStream will close the cache stream.
var uri = new Uri(@"https://dl.dropboxusercontent.com/u/150906/2007-01-28%2006.04.05.JPG");
var cacheSize = 1024 * 64;
var httpStream = new HttpStream.HttpStream(uri, fs, true, cacheSize, null);

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
