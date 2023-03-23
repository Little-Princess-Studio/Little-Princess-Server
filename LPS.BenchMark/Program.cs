// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.BenchMark;

using BenchmarkDotNet.Running;

/// <summary>
/// Entry class.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry method.
    /// </summary>
    /// <param name="args">Args.</param>
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<NativePropertyVsLpsRpcProperty>();
    }
}