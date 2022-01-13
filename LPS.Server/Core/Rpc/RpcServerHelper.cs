using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Core.Entity;

namespace LPS.Core.Rpc
{
    public static class RpcServerHelper
    {
        private static Dictionary<string, Type> EntityClassMap => RpcHelper.EntityClassMap;

        public static DistributeEntity CreateEntityLocally(string entityClassName, string desc)
        {
            if (EntityClassMap.ContainsKey(entityClassName))
            {
                var entityClass = EntityClassMap[entityClassName];
                if (entityClass.IsSubclassOf(typeof(DistributeEntity)))
                {
                    var obj = (Activator.CreateInstance(entityClass, desc) as DistributeEntity)!;
                    return obj;
                }
                throw new Exception($"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
            }
            throw new Exception($"Invalid entity class name {entityClassName}");
        }

        public static async Task<DistributeEntity> CreateEntityAnywhere()
        {
            return null;
        }
    }    
}
