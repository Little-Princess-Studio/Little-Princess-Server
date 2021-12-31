using System;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Entity
{
    public class DistributeEntity : BaseEntity
    {
        public DistributeEntity(Action<EntityRpc> send) : base(send)
        {
            
        }
    }
}