// -----------------------------------------------------------------------
// <copyright file="ConsoleCommandAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Client.Console;

/// <summary>
/// Tag a static method as console command.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ConsoleCommandAttribute : Attribute
{
    /// <summary>
    /// Name of the console command.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleCommandAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the console command..</param>
    public ConsoleCommandAttribute(string name)
    {
        this.Name = name;
    }
}