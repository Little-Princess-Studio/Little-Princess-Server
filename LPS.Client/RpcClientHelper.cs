using System.Reflection;
using LPS.Client.Core.Entity;
using LPS.Client.Core.Rpc.RpcProperty;
using LPS.Common.Core.Entity;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.RpcProperty;

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
                    if (genType != typeof(RpcShadowComplexProperty<>)
                        && genType != typeof(RpcShadowPlaintProperty<>))
                    {
                        return false;
                    }

                    return true;
                }).ToDictionary(
                    field => (field.GetValue(entity) as RpcProperty)!.Name,
                    field => (field.GetValue(entity) as RpcProperty)!);

            foreach (var (_, prop) in tree)
            {
                prop.Owner = entity;
            }

            entity.SetPropertyTree(tree);
        }
    }
}