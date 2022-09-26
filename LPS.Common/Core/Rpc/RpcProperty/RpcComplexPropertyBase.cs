using Google.Protobuf.WellKnownTypes;
using LPS.Common.Core.Rpc.RpcPropertySync;

namespace LPS.Common.Core.Rpc.RpcProperty;

public class RpcComplexPropertyBase<T> : RpcProperty
    where T : RpcPropertyContainer
{
    public RpcComplexPropertyBase(string name, RpcPropertySetting setting, T value)
        : base(name, setting, value)
    {
        if (!this.IsShadowProperty)
        {
            this.Value.Name = name;
            this.Value.IsReferred = true;

            this.Value.UpdateTopOwner(this);
        }
    }

    public void Set(T value)
    {
        if (this.IsShadowProperty)
        {
            throw new Exception("Shadow property cannot be modified manually");
        }
            
        var container = this.Value;
        var old = this.Value;

        old.RemoveFromPropTree();
        old.InsertToPropTree(null, this.Name, this);

        this.Value = value;
        var path = new List<string> {this.Name};
        this.OnNotify(RpcPropertySyncOperation.SetValue, path, value, RpcSyncPropertyType.PlaintAndCostume);
    }

    private T Get()
    {
        return (T) this.Value;
    }

    public T Val
    {
        get => Get();
        private set => Set(value);
    }

    public static implicit operator T(RpcComplexPropertyBase<T> complex) => complex.Val;

    public override Any ToProtobuf()
    {
        return this.Val.ToRpcArg();
    }

    public override void FromProtobuf(Any content)
    {
        if (!this.IsShadowProperty)
        {
            this.Val.RemoveFromPropTree();
        }
        else
        {
            // for shadow property, generic rpc container may not be registered
            if (!RpcHelper.IsRpcContainerRegistered(typeof(T)))
            {
                RpcHelper.RegisterRpcPropertyContainer(typeof(T));
            }
        }
        this.Value = (T) RpcHelper.CreateRpcPropertyContainerByType(typeof(T), content);
        this.Val.InsertToPropTree(null, this.Name, this);
    }
}