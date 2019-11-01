using System;
using asyncperf;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace asyncperf
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<asyncperf.Benchmarks>();
        }
    }
}
