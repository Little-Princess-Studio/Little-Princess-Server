using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Debug;
using LPS.Common.Core.Rpc.RpcPropertySync;

namespace LPS.Common.Core.Rpc.RpcProperty;

public abstract class RpcPlaintPropertyBase<T> : RpcProperty
{
    static RpcPlaintPropertyBase()
    {
        RpcGenericArgTypeCheckHelper.AssertIsValidPlaintType<T>();
    }

    private RpcPlaintPropertyBase() : base("", RpcPropertySetting.None, null)
    {
    }

    public RpcPlaintPropertyBase(string name, RpcPropertySetting setting, T value)
        : base(name, setting, new RpcPropertyContainer<T>(value))
    {
        this.Value.Name = name;
    }

    private void Set(T value)
    {
        if (this.IsShadowProperty)
        {
            throw new Exception("Shadow property cannot be modified manually");
        }

        // var old = ((RpcPropertyContainer<T>) this.Value).Value;
        ((RpcPropertyContainer<T>) this.Value).Value = value;
        
        var path = new List<string> {this.Name};
        
        Logger.Debug($"[Plaint Set] {value}");
        this.OnNotify(RpcPropertySyncOperation.SetValue, path, this.Value, RpcSyncPropertyType.PlaintAndCostume);
    }

    private T Get()
    {
        return (RpcPropertyContainer<T>) this.Value;
    }

    public T Val
    {
        get => Get();
        set => Set(value);
    }

    public static implicit operator T(RpcPlaintPropertyBase<T> container) => container.Val;

    public override Any ToProtobuf()
    {
        return Any.Pack(RpcHelper.RpcArgToProtobuf(this.Val));
    }

    public override void FromProtobuf(Any content)
    {
        this.Value =
            (RpcPropertyContainer<T>) RpcHelper.CreateRpcPropertyContainerByType(
                typeof(RpcPropertyContainer<T>),
                content);
        this.Value.Name = this.Name;
        this.Value.IsReferred = true;
    }
}