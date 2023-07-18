// -----------------------------------------------------------------------
// <copyright file="TypeExtensions.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Util;

/// <summary>
/// Provides extension methods for the <see cref="Type"/> class.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Returns an <see cref="EqualityComparer{T}"/> instance that can be used to compare <see cref="Type"/> objects for equality.
    /// This comparer is used to optimize the performance when we use Type as the key of Dictionary.
    /// </summary>
    /// <returns>An <see cref="EqualityComparer{T}"/> instance that can be used to compare <see cref="Type"/> objects for equality.</returns>
    public static EqualityComparer<Type> GetTypeEqualityComparer()
    {
        return new TypeEqualityComparer();
    }

    private class TypeEqualityComparer : EqualityComparer<Type>
    {
        public override bool Equals(Type? x, Type? y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x == y;
        }

        public override int GetHashCode(Type obj)
        {
            return obj.GetHashCode();
        }
    }
}