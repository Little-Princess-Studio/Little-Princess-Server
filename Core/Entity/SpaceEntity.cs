using System;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Entity
{
    public class SpaceEntity : DistributeEntity
    {
        public SpaceEntity(Action<EntityRpc> send): base(send)
        {
        }
    }
}
