using System;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Entity
{
    public class UniqueEntity : BaseEntity
    {
        public UniqueEntity(Action<EntityRpc> send) : base(send)
        {
            
        }
    }
}
