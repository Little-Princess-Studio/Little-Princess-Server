﻿// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;

namespace LPS.BenchMark;

public static class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<NativePropertyVsLpsRpcProperty>();
    }
}