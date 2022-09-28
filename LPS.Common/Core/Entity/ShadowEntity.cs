using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc;
using LPS.Common.Core.Rpc.InnerMessages;
using LPS.Common.Core.Rpc.RpcProperty;
using LPS.Server.Core.Rpc;
using LPS.Server.Core.Rpc.InnerMessages;

namespace LPS.Common.Core.Entity
{
    [EntityClass]
    public class ShadowEntity : BaseEntity
    {
        public void FromSyncContent(Any syncBody)
        {
            if (syncBody.Is(DictWithStringKeyArg.Descriptor))
            {
                var content = syncBody.Unpack<DictWithStringKeyArg>();

                foreach (var (key, value) in content.PayLoad)
                {
                    if (this.PropertyTree!.ContainsKey(key))
                    {
                        var prop = this.PropertyTree[key];
                        prop.FromProtobuf(value);
                    }
                    else
                    {
                        Debug.Logger.Warn($"Missing sync property {key} in {this.GetType()}");
                    }
                }
            }
        }
        
        public void ApplySyncCommandList(PropertySyncCommandList syncCmdList)
        {
            var path = syncCmdList.Path.Split('.');
            var propType = syncCmdList.PropType;
            var entityId = syncCmdList.EntityId!;
            if (entityId != this.MailBox.Id)
            {
                throw new Exception($"Not the same entity id {entityId} of {this.MailBox.Id}");
            }

            var container = this.FindContainerByPath(path);
            foreach (var syncCmd in syncCmdList.SyncArg)
            {
                var op = syncCmd.Operation;
                switch (op)
                {
                    case SyncOperation.SetValue:
                        HandleSetValue(container, syncCmdList.SyncArg);
                        break;
                    case SyncOperation.UpdatePair:
                        HandleUpdateDict(container, syncCmdList.SyncArg);
                        break;
                    case SyncOperation.AddListElem:
                        HandleAddListElem(container, syncCmdList.SyncArg);
                        break;
                    case SyncOperation.RemoveElem:
                        HandleRemoveElem(container, syncCmdList.SyncArg);
                        break;
                    case SyncOperation.Clear:
                        HandleClear(container, syncCmdList.SyncArg);
                        break;
                    case SyncOperation.InsertElem:
                        HandleInsertElem(container, syncCmdList.SyncArg);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void HandleInsertElem(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
        }

        private void HandleClear(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
            throw new NotImplementedException();
        }

        private void HandleRemoveElem(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
            throw new NotImplementedException();
        }

        private void HandleAddListElem(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
            throw new NotImplementedException();
        }

        private void HandleUpdateDict(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
            throw new NotImplementedException();
        }

        private void HandleSetValue(RpcPropertyContainer container, RepeatedField<PropertySyncCommand> syncArg)
        {
            throw new NotImplementedException();
        }

        private RpcPropertyContainer FindContainerByPath(string[] path)
        {
            var rootName = path[0];

            if (!this.PropertyTree!.ContainsKey(rootName))
            {
                throw new Exception($"Invalid root path name {rootName}");
            }

            var container = this.PropertyTree[rootName].Value;

            for (int i = 1; i < path.Length; ++i)
            {
                var nodeName = path[i];
                if (container.Children != null && container.Children.ContainsKey(nodeName))
                {
                    container = container.Children[nodeName];
                }
                else
                {
                    throw new Exception($"Invalid sync path {string.Join('.', path)}, node {nodeName} not found.");
                }
            }

            return container;
        }
    }
}
