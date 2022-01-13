using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Rpc;

namespace LPS.Logic.Entity
{
    [EntityClass]
    public class Untrusted : DistributeEntity
    {
        public Untrusted(string desc) : base(desc)
        {
            Logger.Debug($"Untrusted created, desc : {desc}");
        }
    }
}
