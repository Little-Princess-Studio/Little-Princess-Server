// -----------------------------------------------------------------------
// <copyright file="RpcProperty.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Common.Rpc.RpcProperty;

/*
 * Deleted.
 * There are 3 way to implement rpc property:
 *
 * 1. Use After-compile technique such as Mono.Ceil. This approch allow us define proerpty such as follow:
 *
 * [RpcMethod(auth, setting)]
 * public string RpcProp() { get; set }
 *
 * This way needs us do post-compile process on output of LPS.Server.Dll to inject extra IL code on properties'
 * getter and setter method (such as null-check && notification and so on)
 * Post-compile process is like a magic, which is actually a 2-step compile.
 *
 * 2. Manually write duplicate setter code as follow:
 *
 *  [RpcProp(auth, setting)]
 *  public string rpcProp_ = ""
 *  public string RpcProp() { get => rpcProp_; set { do something here... } }
 *
 * which is like the code template like dependency property of WPF, which is an ugly but easy way.
 *
 * 3. Use RpcProperty<T> as the property type as follow:
 *
 * public readonly RpcProperty<string> RpcProp = new ("RpcProp", auth, setting);
 *
 * This way is a trade-off between 1 and 2 above, but this way is hardly to handle complex property type's change
 * notification such as:
 *
 * RpcDictionary<string, RpcDictionary<int, RpcList<int>>> RpcComplexProp { get; set; }
 *
 * LPS selected the third way to implement the property system.
 */
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using LPS.Common.Debug;
using LPS.Common.Entity;
using LPS.Common.Rpc.RpcProperty.RpcContainer;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncInfo;
using LPS.Common.Rpc.RpcPropertySync.RpcPropertySyncMessage;

/// <summary>
/// Callback when setting value to property.
/// </summary>
/// <param name="oldVal">Old value.</param>
/// <param name="newVal">New value.</param>
/// <typeparam name="T">Type of the value.</typeparam>
public delegate void OnSetValueCallBack<in T>(T oldVal, T newVal);

/// <summary>
/// Callback when updating existing value.
/// </summary>
/// <param name="key">Key of the value.</param>
/// <param name="oldVal">Old value.</param>
/// <param name="newVal">New value.</param>
/// <typeparam name="TK">Type of key.</typeparam>
/// <typeparam name="TV">Type of value.</typeparam>
public delegate void OnUpdateValueCallBack<in TK, in TV>(TK key, TV oldVal, TV newVal);

/// <summary>
/// Callback when adding elem to list.
/// </summary>
/// <param name="newVal">New elem value.</param>
/// <typeparam name="T">Type of the new elem.</typeparam>
public delegate void OnAddListElemCallBack<in T>(T newVal);

/// <summary>
/// Callback when removing elem from list/dict.
/// </summary>
/// <param name="key">Key of the value.</param>
/// <param name="oldVal">Old value.</param>
/// <typeparam name="TK">Type of key.</typeparam>
/// <typeparam name="TV">Type of value.</typeparam>
public delegate void OnRemoveElemCallBack<in TK, in TV>(TK key, TV oldVal);

/// <summary>
/// Callback when clearing the list/dict.
/// </summary>
public delegate void OnClearCallBack();

/// <summary>
/// Callback when insert item to list.
/// </summary>
/// <param name="index">Position index to insert.</param>
/// <param name="newVal">Value to insert.</param>
/// <typeparam name="T">Type of the value.</typeparam>
public delegate void OnInsertItemCallBack<in T>(int index, T newVal);

/// <summary>
/// Rpc property setting enum.
/// </summary>
[Flags]
public enum RpcPropertySetting
{
    /// <summary>
    /// None prop acts same as normal prop.
    /// </summary>
    None = 0x00000000,

    /// <summary>
    /// Permanent prop will be saved into DB.
    /// </summary>
    Permanent = 0x00000001,

    /// <summary>
    /// ServerOnly prop will not sync with client-side, but will be serialized/deserialized when entity transferred.
    /// </summary>
    ServerOnly = 0x00000010,

    /// <summary>
    /// ServerClient is same as ServerOnly prop, but also for sync to shadow entities.
    /// </summary>
    ServerToShadow = 0x00000100,

    /// <summary>
    /// FastSync will sync as fast as possiable to shadow, ignoring prop change order.
    /// </summary>
    FastSync = 0x00001000,

    /// <summary>
    /// KeepSendOrder will sync props with keeping prop change order.
    /// </summary>
    KeepSendOrder = 0x00010000,

    /// <summary>
    /// Shadow is for shadow entities.
    /// </summary>
    Shadow = 0x00100000,
}

/// <summary>
/// RpcProperty base class.
/// </summary>
public abstract class RpcProperty
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the property settings.
    /// </summary>
    public RpcPropertySetting Setting { get; private set; }

    /// <summary>
    /// Gets or sets the owner of the property.
    /// </summary>
    public BaseEntity? Owner { get; set; }

    /// <summary>
    /// Gets or sets the value of the property.
    /// </summary>
    public RpcPropertyContainer Value { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether this property is a shadow property (readonly/sync-only property).
    /// </summary>
    public bool IsShadowProperty => this.Setting.HasFlag(RpcPropertySetting.Shadow);

    /// <summary>
    /// Gets a value indicating whether this property should sync to remote shadow property.
    /// </summary>
    public bool ShouldSyncToShadow => this.Setting.HasFlag(RpcPropertySetting.ServerToShadow);

    /// <summary>
    /// Gets or sets the implementation of how to send proroperty sync message.
    /// </summary>
    public ISendPropertySyncMessage? SendSyncMsgImpl { get; set; }

    /// <summary>
    /// Gets a value indicating whether this property could sync to client shadow property.
    /// </summary>
    public bool CanSyncToClient =>
        this.SendSyncMsgImpl != null && !this.IsShadowProperty && this.ShouldSyncToShadow;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcProperty"/> class.
    /// </summary>
    /// <param name="value">Initial value of the property.</param>
    protected RpcProperty(RpcPropertyContainer value)
    {
        this.Value = value;
    }

    /// <summary>
    /// Get protobuf content.
    /// </summary>
    /// <returns>Protobuf Any object.</returns>
    public abstract Any ToProtobuf();

    /// <summary>
    /// Construct property from protobuf content.
    /// </summary>
    /// <param name="content">Protobuf Any object.</param>
    public abstract void FromProtobuf(Any content);

    /// <summary>
    /// Init this RpcProperty.
    /// </summary>
    /// <param name="name">Name of the rpc property in property tree.</param>
    /// <param name="setting">Setting of the rpc property.</param>
    public virtual void Init(string name, RpcPropertySetting setting)
    {
        this.Name = name;
        this.Setting = setting;
    }

    /// <summary>
    /// Callback when need be notifed property modification.
    /// </summary>
    /// <param name="operation">Modification operation.</param>
    /// <param name="path">Property path.</param>
    /// <param name="new">New value.</param>
    /// <param name="propertyType">Type of the property.</param>
    /// <exception cref="ArgumentOutOfRangeException">ArgumentOutOfRangeException.</exception>
    internal void OnNotify(
        RpcPropertySyncOperation operation,
        List<string> path,
        RpcPropertyContainer? @new,
        RpcSyncPropertyType propertyType)
    {
        Logger.Debug(
            $"[OnNotify] {!this.CanSyncToClient}, {this.Owner == null}, {!this.CanSyncToClient || this.Owner == null}");
        if (!this.CanSyncToClient || this.Owner == null)
        {
            return;
        }

        Logger.Debug($"[OnNotify] {operation}, {string.Join(".", path)}, {@new}");
        switch (operation)
        {
            case RpcPropertySyncOperation.SetValue:
                this.OnSetValueInternal(path, @new!, propertyType);
                break;
            case RpcPropertySyncOperation.UpdatePair:
                this.OnUpdatePairInternal(path, @new!, propertyType);
                break;
            case RpcPropertySyncOperation.AddListElem:
                this.OnAddListElemInternal(path, @new!);
                break;
            case RpcPropertySyncOperation.RemoveElem:
                this.OnRemoveElemInternal(path, propertyType);
                break;
            case RpcPropertySyncOperation.Clear:
                this.OnClearInternal(path, propertyType);
                break;
            case RpcPropertySyncOperation.InsertElem:
                this.OnInsertItem(path, @new!);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private void OnSetValueInternal(
        List<string> path,
        RpcPropertyContainer newVal,
        RpcSyncPropertyType propertyType)
    {
        var fullPath = string.Join(',', path);
        RpcPropertySyncMessage syncMsg;
        switch (propertyType)
        {
            case RpcSyncPropertyType.PlaintAndCostume:
                syncMsg = new RpcPlaintAndCostumePropertySyncMessage(
                    this.Owner!.MailBox,
                    RpcPropertySyncOperation.SetValue,
                    fullPath,
                    newVal);
                break;
            case RpcSyncPropertyType.Dict:
                var syncDictMsg = new RpcDictPropertySyncMessage(
                    this.Owner!.MailBox,
                    RpcPropertySyncOperation.SetValue,
                    fullPath);
                syncDictMsg.Action!(newVal);
                syncMsg = syncDictMsg;
                break;
            case RpcSyncPropertyType.List:
                var syncListMsg = new RpcListPropertySyncMessage(
                    this.Owner!.MailBox,
                    RpcPropertySyncOperation.SetValue,
                    fullPath);
                syncListMsg.Action!(newVal);
                syncMsg = syncListMsg;
                break;
            default:
                throw new Exception($"Invalid rpc property type {propertyType}");
        }

        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
    }

    private void OnUpdatePairInternal(
        List<string> path,
        RpcPropertyContainer newVal,
        RpcSyncPropertyType propertyType)
    {
        var key = path.Last();
        path.RemoveAt(path.Count - 1);
        var fullPath = string.Join('.', path);
        var syncMsg = new RpcDictPropertySyncMessage(
            this.Owner!.MailBox,
            RpcPropertySyncOperation.UpdatePair,
            fullPath);
        syncMsg.Action!(key, newVal);
        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
    }

    private void OnAddListElemInternal(List<string> path, RpcPropertyContainer newVal)
    {
        var idx = Convert.ToInt32(path.Last());
        path.RemoveAt(path.Count - 1);
        var fullPath = string.Join('.', path);
        var syncMsg = new RpcListPropertySyncMessage(
            this.Owner!.MailBox,
            RpcPropertySyncOperation.AddListElem,
            fullPath);
        syncMsg.Action!(idx, newVal);
        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
    }

    private void OnRemoveElemInternal(List<string> path, RpcSyncPropertyType propertyType)
    {
        var key = path.Last();
        path.RemoveAt(path.Count - 1);
        var fullPath = string.Join('.', path);

        RpcPropertySyncMessage syncMsg;

        switch (propertyType)
        {
            case RpcSyncPropertyType.List:
                var syncMsgList = new RpcListPropertySyncMessage(
                    this.Owner!.MailBox,
                    RpcPropertySyncOperation.RemoveElem,
                    fullPath);
                syncMsgList.Action!(Convert.ToInt32(key));
                syncMsg = syncMsgList;
                break;
            case RpcSyncPropertyType.Dict:
                var syncMsgDict = new RpcDictPropertySyncMessage(
                    this.Owner!.MailBox,
                    RpcPropertySyncOperation.RemoveElem,
                    fullPath);
                syncMsgDict.Action!(key);
                syncMsg = syncMsgDict;
                break;
            case RpcSyncPropertyType.PlaintAndCostume:
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null);
        }

        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg);
    }

    private void OnClearInternal(List<string> path, RpcSyncPropertyType propertyType)
    {
        var fullPath = string.Join('.', path);

        RpcPropertySyncMessage syncMsg = propertyType switch
        {
            RpcSyncPropertyType.List => new RpcListPropertySyncMessage(
                this.Owner!.MailBox,
                RpcPropertySyncOperation.Clear,
                fullPath),
            RpcSyncPropertyType.Dict => new RpcDictPropertySyncMessage(
                this.Owner!.MailBox,
                RpcPropertySyncOperation.Clear,
                fullPath),
            _ => throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null),
        };
        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg);
    }

    private void OnInsertItem(List<string> path, RpcPropertyContainer newVal)
    {
        var idx = Convert.ToInt32(path.Last());
        path.RemoveAt(path.Count - 1);
        var fullPath = string.Join('.', path);
        var syncMsg = new RpcListPropertySyncMessage(
            this.Owner!.MailBox,
            RpcPropertySyncOperation.InsertElem,
            fullPath);
        syncMsg.Action!(idx, newVal);
        this.SendSyncMsgImpl!.SendSyncMsg(false, 0, syncMsg!);
    }
}

/// <summary>
/// Interface to send sync message.
/// </summary>
public interface ISendPropertySyncMessage
{
    /// <summary>
    /// Send sync message to remote.
    /// </summary>
    /// <param name="keepOrder">If this message should keep sending order.</param>
    /// <param name="delayTime">The min delay time to send the message.</param>
    /// <param name="syncMsg">Sync message.</param>
    void SendSyncMsg(bool keepOrder, uint delayTime, RpcPropertySyncMessage syncMsg);
}

/// <summary>
/// Operation interface of set value.
/// </summary>
public interface ISyncOpActionSetValue
{
    /// <summary>
    /// Apply setting value option with sync arguments.
    /// </summary>
    /// <param name="syncArg">Sync arguments.</param>
    public void Apply(RepeatedField<Any> syncArg);
}

/// <summary>
/// Operation interface of update key-value pair.
/// </summary>
public interface ISyncOpActionUpdatePair
{
    /// <summary>
    /// Apply updating key-value pair option with sync arguments.
    /// </summary>
    /// <param name="syncArg">Sync arguments.</param>
    public void Apply(RepeatedField<Any> syncArg);
}

/// <summary>
/// Operation interface of add elem to list.
/// </summary>
public interface ISyncOpActionAddElem
{
    /// <summary>
    /// Apply adding element option with sync arguments.
    /// </summary>
    /// <param name="syncArg">Sync arguments.</param>
    public void Apply(RepeatedField<Any> syncArg);
}

/// <summary>
/// Operation interface of removing elem from dict/list.
/// </summary>
public interface ISyncOpActionRemoveElem
{
    /// <summary>
    /// Apply removing element option with sync arguments.
    /// </summary>
    /// <param name="syncArg">Sync arguments.</param>
    public void Apply(RepeatedField<Any> syncArg);
}

/// <summary>
/// Operation interface of clearing list/dict to list.
/// </summary>
public interface ISyncOpActionClear
{
    /// <summary>
    /// Apply clearing list/dict with sync arguments.
    /// </summary>
    public void Apply();
}

/// <summary>
/// Operation interface of insert elem to list.
/// </summary>
public interface ISyncOpActionInsertElem
{
    /// <summary>
    /// Apply inserting elem to list with sync arguments.
    /// </summary>
    /// <param name="syncArg">Sync arguments.</param>
    public void Apply(RepeatedField<Any> syncArg);
}

// public class RpcShadowPlaintProperty<T> : RpcPlainProperty<T>
// {
//     public RpcShadowPlaintProperty(string name) : base(name, RpcPropertySetting.Shadow, default(T)!)
//     {
//     }
// }
//
// public class RpcShadowComplexProperty<T> : RpcComplexProperty<T>
//     where T : RpcPropertyContainer
// {
//     public RpcShadowComplexProperty(string name) : base(name, RpcPropertySetting.Shadow, null!)
//     {
//     }
// }

// public class RpcComplexProperty<T> : RpcProperty
//     where T : RpcPropertyContainer
// {
//     public RpcComplexProperty(string name, RpcPropertySetting setting, T value)
//         : base(name, setting, value)
//     {
//         if (!this.IsShadowProperty)
//         {
//             this.Value.Name = name;
//             this.Value.IsReferred = true;
//
//             this.Value.UpdateTopOwner(this);
//         }
//     }
//
//     public void Set(T value)
//     {
//         if (this.IsShadowProperty)
//         {
//             throw new Exception("Shadow property cannot be modified manually");
//         }
//
//         var container = this.Value;
//         var old = this.Value;
//
//         old.RemoveFromPropTree();
//         old.InsertToPropTree(null, this.Name, this);
//
//         this.Value = value;
//         var path = new List<string> {this.Name};
//         this.OnNotify(RpcPropertySyncOperation.SetValue, path, value, RpcSyncPropertyType.PlaintAndCostume);
//     }
//
//     private T Get()
//     {
//         return (T) this.Value;
//     }
//
//     public T Val
//     {
//         get => Get();
//         private set => Set(value);
//     }
//
//     public static implicit operator T(RpcComplexProperty<T> complex) => complex.Val;
//
//     public override Any ToProtobuf()
//     {
//         return this.Val.ToRpcArg();
//     }
//
//     public override void FromProtobuf(Any content)
//     {
//         if (!this.IsShadowProperty)
//         {
//             this.Val.RemoveFromPropTree();
//         }
//         else
//         {
//             // for shadow property, generic rpc container may not be registered
//             if (!RpcHelper.IsRpcContainerRegistered(typeof(T)))
//             {
//                 RpcHelper.RegisterRpcPropertyContainer(typeof(T));
//             }
//         }
//
//         this.Value = (T) RpcHelper.CreateRpcPropertyContainerByType(typeof(T), content);
//         this.Val.InsertToPropTree(null, this.Name, this);
//     }
// }
//
// public class RpcPlainProperty<T> : RpcProperty
// {
//     static RpcPlainProperty()
//     {
//         RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
//     }
//
//     private RpcPlainProperty() : base("", RpcPropertySetting.None, null)
//     {
//     }
//
//     public RpcPlainProperty(string name, RpcPropertySetting setting, T value)
//         : base(name, setting, new RpcPropertyContainer<T>(value))
//     {
//         this.Value.Name = name;
//     }
//
//     private void Set(T value)
//     {
//         if (this.IsShadowProperty)
//         {
//             throw new Exception("Shadow property cannot be modified manually");
//         }
//
//         var old = ((RpcPropertyContainer<T>) this.Value).Value;
//         ((RpcPropertyContainer<T>) this.Value).Value = value;
//
//         var path = new List<string> {this.Name};
//         this.OnNotify(RpcPropertySyncOperation.SetValue, path, this.Value, RpcSyncPropertyType.PlaintAndCostume);
//     }
//
//     private T Get()
//     {
//         return (RpcPropertyContainer<T>) this.Value;
//     }
//
//     public T Val
//     {
//         get => Get();
//         set => Set(value);
//     }
//
//     public static implicit operator T(RpcPlainProperty<T> container) => container.Val;
//
//     public override Any ToProtobuf()
//     {
//         return Any.Pack(RpcHelper.RpcArgToProtobuf(this.Val));
//     }
//
//     public override void FromProtobuf(Any content)
//     {
//         this.Value =
//             (RpcPropertyContainer<T>) RpcHelper.CreateRpcPropertyContainerByType(
//                 typeof(RpcPropertyContainer<T>),
//                 content);
//         this.Value.Name = this.Name;
//         this.Value.IsReferred = true;
//     }
// }