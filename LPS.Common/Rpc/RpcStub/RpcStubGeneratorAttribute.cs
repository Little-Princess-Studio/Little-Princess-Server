// -----------------------------------------------------------------------
// <copyright file="RpcStubGeneratorAttribute.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcStub;

/// <summary>
/// Attribute used to mark interface that generate RPC stub interface.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
public class RpcStubGeneratorAttribute : System.Attribute
{
    /// <summary>
    /// Gets the type of the generator.
    /// </summary>
    public readonly Type GeneratorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcStubGeneratorAttribute"/> class.
    /// </summary>
    /// <param name="generatorType">The type of the generator.</param>
    public RpcStubGeneratorAttribute(Type generatorType)
    {
        this.GeneratorType = generatorType;
    }
}