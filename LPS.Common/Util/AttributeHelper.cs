// -----------------------------------------------------------------------
// <copyright file="AttributeHelper.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Util;

using System.Reflection;

/// <summary>
/// Provides helper methods for working with attributes.
/// </summary>
public static class AttributeHelper
{
    /// <summary>
    /// Scans for all types in the specified namespace that are decorated with the specified attribute type.
    /// </summary>
    /// <param name="namespace">The namespace to scan for types.</param>
    /// <param name="attributeType">The attribute type to search for.</param>
    /// <param name="inherit">Specifies whether to search this member's inheritance chain to find the attributes.</param>
    /// <param name="prediction">A function that returns true for types that should be included in the result.</param>
    /// <returns>An enumerable collection of types that match the specified criteria.</returns>
    public static IEnumerable<Type> ScanTypeWithNamespaceAndAttribute(string @namespace, Type attributeType, bool inherit, Func<Type, bool> prediction)
        => ScanTypeWithNamespace(@namespace, prediction: (type) => type.IsDefined(attributeType, inherit) && prediction.Invoke(type));

    /// <summary>
    /// Scans for all types in the specified namespace that satisfy the specified prediction function.
    /// </summary>
    /// <param name="namespace">The namespace to scan for types.</param>
    /// <param name="prediction">A function that returns true for types that should be included in the result.</param>
    /// <returns>An enumerable collection of types that match the specified criteria.</returns>
    public static IEnumerable<Type> ScanTypeWithNamespace(string @namespace, Func<Type, bool> prediction)
    {
        var callingAssemblyTypes = Assembly.GetCallingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == @namespace && prediction.Invoke(type));
        var entryAssemblyTypes = Assembly.GetEntryAssembly()?
            .GetTypes()
            .Where(type => type.Namespace == @namespace && prediction.Invoke(type));
        IEnumerable<Type>? allInterfaces;
        if (entryAssemblyTypes is not null)
        {
            allInterfaces = callingAssemblyTypes.Concat(entryAssemblyTypes).Distinct();
        }
        else
        {
            allInterfaces = callingAssemblyTypes.Distinct();
        }

        return allInterfaces;
    }
}