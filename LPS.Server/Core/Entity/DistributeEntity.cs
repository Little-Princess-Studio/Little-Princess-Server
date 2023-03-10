// -----------------------------------------------------------------------
// <copyright file="DistributeEntity.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core.Entity
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Google.Protobuf.WellKnownTypes;
    using LPS.Common.Core.Debug;
    using LPS.Common.Core.Entity;
    using LPS.Common.Core.Rpc;
    using LPS.Common.Core.Rpc.InnerMessages;
    using LPS.Common.Core.Rpc.RpcProperty;
    using LPS.Common.Core.Rpc.RpcPropertySync;
    using MailBox = LPS.Common.Core.Rpc.MailBox;

    /// <summary>
    /// Distribute entity is the entity which can be created on any server process.
    /// Generally, we should only call/notify distribute entities by their mailbox via RPC call.
    /// </summary>
    [EntityClass]
    public abstract class DistributeEntity : BaseEntity, ISendPropertySyncMessage
    {
        /// <summary>
        /// Gets or sets the cell of the entity.
        /// </summary>
        public CellEntity Cell { get; set; } = null!;

        /// <summary>
        /// Gets or sets the sync message handler of the entity.
        /// </summary>
        public Action<bool, uint, RpcPropertySyncMessage>? SendSyncMessageHandler { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributeEntity"/> class.
        /// </summary>
        /// <param name="desc">Description string for constructing DistributeEntity.</param>
        protected DistributeEntity(string desc)
            : this()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributeEntity"/> class.
        /// </summary>
        protected DistributeEntity()
        {
            this.IsFrozen = true;
        }

        /// <inheritdoc/>
        public override void SetPropertyTree(Dictionary<string, RpcProperty> propertyTree)
        {
            base.SetPropertyTree(propertyTree);
            foreach (var (_, v) in this.PropertyTree!)
            {
                v.SendSyncMsgImpl = this;
            }
        }

        /// <inheritdoc/>
        public void SendSyncMsg(bool keepOrder, uint delayTime, RpcPropertySyncMessage syncMsg)
        {
            if (syncMsg == null)
            {
                throw new ArgumentNullException(nameof(syncMsg));
            }

            Logger.Debug($"[SendSyncMsg] {keepOrder} {delayTime} {syncMsg}");
            this.SendSyncMessageHandler?.Invoke(keepOrder, delayTime, syncMsg);
        }

        /// <summary>
        /// Do full properties sync.
        /// </summary>
        /// <param name="onSyncContentReady">Callback when sync ready.</param>
        public void FullSync(Action<string, Any> onSyncContentReady)
        {
            var treeDict = new DictWithStringKeyArg();

            foreach (var (key, value) in this.PropertyTree!)
            {
                if (value.CanSyncToClient)
                {
                    treeDict.PayLoad.Add(key, value.ToProtobuf());
                }
            }

            onSyncContentReady.Invoke(this.MailBox.Id, Any.Pack(treeDict));
        }

        /// <summary>
        /// Ack method of sync request.
        /// </summary>
        public void FullSyncAck()
        {
            // todo: timeout of sync
            this.IsFrozen = false;
        }

        /// <summary>
        /// Migrate current DistributeEntity to another DistributeEntity
        /// return ture if successfully migrate, otherwise false
        /// Steps for migrating to another DistributeEntity:
        ///
        /// 1. set origin entity status to frozen
        /// 2. send request rpc to target DistributeEntity and wait
        /// 3. target entity set entity status to frozen
        /// 4. target entity rebuild self with migrateInfo (OnMigratedIn is called)
        /// 5. destroy current origin entity.
        ///
        /// </summary>
        /// <param name="targetMailBox">Target entity migrate to.</param>
        /// <param name="migrateInfo">Info of the migration.</param>
        /// <returns>If the migration success.</returns>
        public virtual async Task<bool> MigrateTo(MailBox targetMailBox, string migrateInfo)
        {
            if (targetMailBox.CompareOnlyID(this.MailBox))
            {
                return false;
            }

            this.IsFrozen = true;

            Logger.Info($"start migrate, from {this.MailBox} to {targetMailBox}");

            try
            {
                var res = await this.Call<bool>(
                    targetMailBox,
                    nameof(this.RequireMigrate),
                    this.MailBox,
                    this.GetType().Name,
                    migrateInfo);

                if (!res)
                {
                    this.IsFrozen = false;
                    throw new Exception("Error when migrate distribute entity");
                }

                // destroy self
                this.Cell.OnEntityLeave(this);
                this.Destroy();

                return true;
            }
            catch (Exception e)
            {
                this.IsFrozen = false;
                Logger.Error(e, "Error when migrate distribute entity");
                throw;
            }
        }

        /// <summary>
        /// Rpc for request a migrate.
        /// </summary>
        /// <param name="originMailBox">Original entity who wants to migrate into this entity.</param>
        /// <param name="migrateInfo">Migrate info.</param>
        /// <returns>If the migration success.</returns>
        [RpcMethod(authority: Authority.ServerOnly)]
        public Task<bool> RequireMigrate(MailBox originMailBox, string migrateInfo)
        {
            this.IsFrozen = true;
            try
            {
                this.OnMigratedIn(originMailBox, migrateInfo);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to migrate in");
                return Task.FromResult(false);
            }
            finally
            {
                this.IsFrozen = false;
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Callback when migrated in.
        /// </summary>
        /// <param name="originMailBox">Original entity who wants to migrate into this entity.</param>
        /// <param name="migrateInfo">Migrate info.</param>
        public virtual void OnMigratedIn(MailBox originMailBox, string migrateInfo)
        {
        }

        /// <summary>
        /// Steps for entity transfer to other server
        ///
        /// 1. set entity status to frozen
        /// 2. serialize entity
        /// 3. send request rpc to target cell and wait
        /// 4. remote cell creates a clone entity locally
        /// 5. remote clone entity rebuild
        /// 6. remote cell add new entity
        /// 7. remote server bind gate connection to created new entity
        /// 8. origin entity notify client change mailbox (if entity is ServerClientEntity)
        /// 9. destroy origin entity.
        ///
        /// </summary>
        /// <param name="targetCellMailBox">Target cell's mailbox this entity wants to transfer.</param>
        /// <param name="transferInfo">Tansfer infomation.</param>
        /// <returns>Task.</returns>
        /// <exception cref="Exception">Throw exception if failed to transfer.</exception>
        public virtual async Task TransferIntoCell(MailBox targetCellMailBox, string transferInfo)
        {
            if (targetCellMailBox.CompareOnlyID(this.Cell.MailBox))
            {
                return;
            }

            this.IsFrozen = true;

            // todo: serialContent is the serialized rpc property tree of entity
            Logger.Debug($"start transfer to {targetCellMailBox}");

            var serialContent = string.Empty;
            try
            {
                var (res, mailbox) = await this.Call<(bool, MailBox)>(
                    targetCellMailBox,
                    nameof(CellEntity.RequireTransfer),
                    this.MailBox,
                    this.GetType().Name,
                    serialContent,
                    transferInfo);

                if (!res)
                {
                    this.IsFrozen = false;
                    throw new Exception("Error when transfer to cell");
                }

                this.Cell.OnEntityLeave(this);
                this.Destroy();

                Logger.Debug($"transfer success, new mailbox {mailbox}");
            }
            catch (Exception e)
            {
                this.IsFrozen = false;
                Logger.Error(e, "Error when transfer to cell");
                throw;
            }
        }

        /// <summary>
        /// Callback when finishing transferring.
        /// </summary>
        /// <param name="transferInfo">Transfer info.</param>
        public virtual void OnTransferred(string transferInfo)
        {
        }
    }
}