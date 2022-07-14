using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LPS.Core.Entity;
using LPS.Core.Rpc.RpcProperty;

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
                    RpcHelper.BuildPropertyTree(obj);
                    return obj;
                }

                throw new Exception(
                    $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
            }

            throw new Exception($"Invalid entity class name {entityClassName}");
        }

        public static DistributeEntity BuildEntityFromSerialContent(
            MailBox entityMailBox, string entityClassName, string serialContent)
        {
            // var entity = Activator.CreateInstance<DistributeEntity>(entityClassName);
            if (EntityClassMap.ContainsKey(entityClassName))
            {
                var entityClass = EntityClassMap[entityClassName];
                if (entityClass.IsSubclassOf(typeof(DistributeEntity)))
                {
                    var obj = (Activator.CreateInstance(entityClass, null) as DistributeEntity)!;
                    obj.MailBox = entityMailBox;
                    obj.Deserialize(serialContent);
                    RpcHelper.BuildPropertyTree(obj);
                    return obj;
                }

                throw new Exception(
                    $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
            }

            throw new Exception($"Invalid entity class name {entityClassName}");
        }

        // public static Task<DistributeEntity> CreateEntityAnywhere()
        // {
        //     return null;
        // }
    }
}