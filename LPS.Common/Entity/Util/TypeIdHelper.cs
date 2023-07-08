// -----------------------------------------------------------------------
// <copyright file="TypeIdHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Util;

/// <summary>
/// Provides helper methods for generating unique identifiers for types.
/// </summary>
public static class TypeIdHelper
{
    /// <summary>
    /// Gets the unique identifier for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to get the identifier for.</typeparam>
    /// <returns>The unique identifier for the specified type.</returns>
    public static uint GetId<T>() => TypeId<T>.Id;

    /// <summary>
    /// Gets the unique identifier for the specified type.
    /// </summary>
    /// <param name="type">The type to get the identifier for.</param>
    /// <returns>The unique identifier for the specified type.</returns>
    public static uint GetId(Type type)
    {
        var genType = typeof(TypeId<>);
        var typeIdType = genType.MakeGenericType(type);
        return typeIdType.GetField("Id")!.GetValue(null) as uint?
            ?? throw new Exception($"Failed to get type ID for {type}.");
    }
}

#pragma warning disable SA1402
#pragma warning disable SA1600

/// <summary>
/// Represents a unique identifier for a specific type.
/// </summary>
/// <typeparam name="T">The type to generate an identifier for.</typeparam>
internal static class TypeId<T>
{
    /// <summary>
    /// The generated type ID.
    /// </summary>
    public static readonly uint Id = TypeIdGenerator.GenerateNewId();
}

internal class TypeIdGenerator
{
    private static uint typeId = 0;

    /// <summary>
    /// Generates a new unique ID for a type.
    /// </summary>
    /// <returns>The new unique ID.</returns>
    public static uint GenerateNewId() => typeId++;
}
#pragma warning restore SA1600
#pragma warning restore SA1402