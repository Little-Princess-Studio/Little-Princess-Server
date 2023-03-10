// -----------------------------------------------------------------------
// <copyright file="UniqueEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Entity
{
    using LPS.Common.Core.Entity;
    using LPS.Common.Core.Rpc;

    /// <summary>
    /// Unique entity should have only 1 instance host-wide.
    /// </summary>
    [EntityClass]
    public class UniqueEntity : BaseEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueEntity"/> class.
        /// </summary>
        protected UniqueEntity()
        {
        }
    }
}