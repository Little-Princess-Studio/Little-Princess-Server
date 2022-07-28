using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Rpc;
using LPS.Server.Core.Entity;
using LPS.Server.Core.Rpc.RpcProperty;

namespace LPS.Server.Core.Rpc
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
                    BuildPropertyTree(obj);
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
                    BuildPropertyTree(obj);
                    return obj;
                }

                throw new Exception(
                    $"Invalid class {entityClassName}, only DistributeEntity and its subclass can be created by CreateEntityLocally.");
            }

            throw new Exception($"Invalid entity class name {entityClassName}");
        }

        public static void BuildPropertyTree(BaseEntity entity)
        {
            var type = entity.GetType();
            var tree = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field =>
                {
                    var fieldType = field.FieldType;

                    if (!fieldType.IsGenericType)
                    {
                        return false;
                    }

                    var genType = fieldType.GetGenericTypeDefinition();
                    if (genType != typeof(RpcPlaintProperty<>)
                        && genType != typeof(RpcComplexProperty<>))
                        // && genType != typeof(RpcShadowComplexProperty<>)
                        // && genType != typeof(RpcShadowPlaintProperty<>))
                    {
                        return false;
                    }

                    return true;
                }).ToDictionary(
                    field => (field.GetValue(entity) as Common.Core.Rpc.RpcProperty.RpcProperty)!.Name,
                    field => (field.GetValue(entity) as Common.Core.Rpc.RpcProperty.RpcProperty)!);

            foreach (var (_, prop) in tree)
            {
                prop.Owner = entity;
            }

            entity.SetPropertyTree(tree);
        }
    }
}