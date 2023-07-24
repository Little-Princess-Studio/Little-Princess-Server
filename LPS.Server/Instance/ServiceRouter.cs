// -----------------------------------------------------------------------
// <copyright file="ServiceRouter.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Service.Instance;

using LPS.Server;

/// <summary>
/// Represents a service instance, which contains multiple LPS service instance.
/// </summary>
public class ServiceRouter : IInstance
{
    /// <inheritdoc/>
    public InstanceType InstanceType => InstanceType.ServiceRouter;

    /// <inheritdoc/>
    public string Ip => throw new System.NotImplementedException();

    /// <inheritdoc/>
    public int Port => throw new System.NotImplementedException();

    /// <inheritdoc/>
    public int HostNum => throw new System.NotImplementedException();

    /// <inheritdoc/>
    public void Loop()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        throw new System.NotImplementedException();
    }
}