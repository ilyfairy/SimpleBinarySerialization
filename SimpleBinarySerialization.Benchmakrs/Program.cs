﻿using BenchmarkDotNet.Running;

namespace SimpleBinarySerialization.Benchmakrs
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
