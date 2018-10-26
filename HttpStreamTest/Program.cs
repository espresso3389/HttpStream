using Espresso3389.HttpStream;
using System;

namespace HttpStreamTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var httpStream = new HttpStream(new Uri(args[0]));
            var size = (int)httpStream.Length;
            var first = Math.Min(1024, size);
            var second = size - first;

            var mem = new byte[size];
            var firstRead = httpStream.Read(mem, 0, first);
            var secondRead = httpStream.Read(mem, first, second);
            var pos = httpStream.Position;

            Console.WriteLine($"Pos={pos}, Length={size}");
        }
    }
}
