using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Rpc;

namespace LPS.Core.Entity
{
    [EntityClass]
    public abstract class CellEntity : BaseEntity
    {
        public readonly Dictionary<string, DistributeEntity> Entities = new();
        public Action<DistributeEntity> EntityLeaveCallBack { get; init; }
        public Action<DistributeEntity> EntityEnterCallBack { get; init; }
        
        public CellEntity(string desc)
        {
        }

        public DistributeEntity? GetEntityById(string entityId)
        {
            this.Entities.TryGetValue(entityId, out var entity);
            return entity;
        }

        public void OnEntityEnter(DistributeEntity entity)
        {
            this.Entities.Add(entity.MailBox.Id, entity);
            this.EntityEnterCallBack.Invoke(entity);
        }

        public void OnEntityLeave(DistributeEntity entity)
        {
            this.Entities.Remove(entity.MailBox.Id);
            this.EntityLeaveCallBack.Invoke(entity);
        }

        [RpcMethod(Authority.ServerOnly)]
        public ValueTask<(bool, MailBox)> RequireTransfer(MailBox entityMailBox, 
            string entityClassName,
            string serialContent, 
            string transferInfo)
        {
            Logger.Debug($"transfer request: {entityMailBox} {entityClassName} {serialContent} {transferInfo}");
            
            var res = false;
            do
            {
                if (this.Entities.ContainsKey(entityMailBox.Id))
                {
                    break;
                }
            
                var entity = RpcServerHelper.BuildEntityFromSerialContent(
                    new MailBox(entityMailBox.Id, this.MailBox.Ip, this.MailBox.Port, this.MailBox.HostNum), 
                    entityClassName, 
                    serialContent);
                entity.OnTransferred(transferInfo);
                this.Entities.Add(entityMailBox.Id, entity);
                this.OnEntityEnter(entity);
                res = true;
            } while (false);

            return ValueTask.FromResult((res, entityMailBox));
        }

        public abstract void Tick();
    }
}
