using LPS.Client.Entity;
using LPS.Core.Entity;
using LPS.Core.Rpc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Client
{
    public static class RpcClientHelper
    {
        private static Dictionary<string, Type> EntityClassMap_ = RpcHelper.EntityClassMap;

        public static ShadowClientEntity CreateClientEntity(string entityClassName)
        {
            if (EntityClassMap_.ContainsKey(entityClassName))
            {
                var entityClass = EntityClassMap_[entityClassName];
                if (entityClass.IsSubclassOf(typeof(ShadowClientEntity)))
                {
                    var obj = (Activator.CreateInstance(entityClass) as ShadowClientEntity)!;
                    RpcHelper.BuildPropertyTree(obj);
                    return obj;
                }
                throw new Exception($"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
            }
            throw new Exception($"Invalid entity class name {entityClassName}");
        }
    }
}
