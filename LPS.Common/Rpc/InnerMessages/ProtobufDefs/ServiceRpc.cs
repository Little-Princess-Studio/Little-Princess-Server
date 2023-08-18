// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: service_rpc.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace LPS.Common.Rpc.InnerMessages {

  /// <summary>Holder for reflection information generated from service_rpc.proto</summary>
  public static partial class ServiceRpcReflection {

    #region Descriptor
    /// <summary>File descriptor for service_rpc.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ServiceRpcReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChFzZXJ2aWNlX3JwYy5wcm90bxIcTFBTLkNvbW1vbi5ScGMuSW5uZXJNZXNz",
            "YWdlcxoNbWFpbGJveC5wcm90bxoZZ29vZ2xlL3Byb3RvYnVmL2FueS5wcm90",
            "byK2AgoKU2VydmljZVJwYxINCgVScGNJRBgBIAEoDRJBCg1TZW5kZXJNYWls",
            "Qm94GAIgASgLMiUuTFBTLkNvbW1vbi5ScGMuSW5uZXJNZXNzYWdlcy5NYWls",
            "Qm94SACIAQESEwoLU2VydmljZU5hbWUYAyABKAkSEgoKTWV0aG9kTmFtZRgE",
            "IAEoCRITCgtSYW5kb21TaGFyZBgFIAEoCBIPCgdTaGFyZElEGAYgASgNEhIK",
            "Ck5vdGlmeU9ubHkYByABKAgSPQoHUnBjVHlwZRgIIAEoDjIsLkxQUy5Db21t",
            "b24uUnBjLklubmVyTWVzc2FnZXMuU2VydmljZVJwY1R5cGUSIgoEQXJncxgJ",
            "IAMoCzIULmdvb2dsZS5wcm90b2J1Zi5BbnlCEAoOX1NlbmRlck1haWxCb3gi",
            "iAEKElNlcnZpY2VScGNDYWxsQmFjaxINCgVScGNJRBgBIAEoDRI9CgdScGNU",
            "eXBlGAIgASgOMiwuTFBTLkNvbW1vbi5ScGMuSW5uZXJNZXNzYWdlcy5TZXJ2",
            "aWNlUnBjVHlwZRIkCgZSZXN1bHQYAyABKAsyFC5nb29nbGUucHJvdG9idWYu",
            "QW55KsgBCg5TZXJ2aWNlUnBjVHlwZRITCg9TZXJ2ZXJUb1NlcnZpY2UQABIT",
            "Cg9TZXJ2aWNlVG9TZXJ2ZXIQARIUChBTZXJ2aWNlVG9TZXJ2aWNlEAISEgoO",
            "Q2xpZW50VG9TZXJ2ZXIQAxISCg5TZXJ2ZXJUb0NsaWVudBAEEhEKDUh0dHBU",
            "b1NlcnZpY2UQBRIRCg1TZXJ2aWNlVG9IdHBwEAYSEwoPQ2xpZW50VG9TZXJ2",
            "aWNlEAcSEwoPU2VydmljZVRvQ2xpZW50EAhiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::LPS.Common.Rpc.InnerMessages.MailboxReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.AnyReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::LPS.Common.Rpc.InnerMessages.ServiceRpcType), }, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Common.Rpc.InnerMessages.ServiceRpc), global::LPS.Common.Rpc.InnerMessages.ServiceRpc.Parser, new[]{ "RpcID", "SenderMailBox", "ServiceName", "MethodName", "RandomShard", "ShardID", "NotifyOnly", "RpcType", "Args" }, new[]{ "SenderMailBox" }, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Common.Rpc.InnerMessages.ServiceRpcCallBack), global::LPS.Common.Rpc.InnerMessages.ServiceRpcCallBack.Parser, new[]{ "RpcID", "RpcType", "Result" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Enums
  public enum ServiceRpcType {
    [pbr::OriginalName("ServerToService")] ServerToService = 0,
    [pbr::OriginalName("ServiceToServer")] ServiceToServer = 1,
    [pbr::OriginalName("ServiceToService")] ServiceToService = 2,
    [pbr::OriginalName("ClientToServer")] ClientToServer = 3,
    [pbr::OriginalName("ServerToClient")] ServerToClient = 4,
    [pbr::OriginalName("HttpToService")] HttpToService = 5,
    [pbr::OriginalName("ServiceToHtpp")] ServiceToHtpp = 6,
    [pbr::OriginalName("ClientToService")] ClientToService = 7,
    [pbr::OriginalName("ServiceToClient")] ServiceToClient = 8,
  }

  #endregion

  #region Messages
  public sealed partial class ServiceRpc : pb::IMessage<ServiceRpc>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<ServiceRpc> _parser = new pb::MessageParser<ServiceRpc>(() => new ServiceRpc());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<ServiceRpc> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Common.Rpc.InnerMessages.ServiceRpcReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpc() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpc(ServiceRpc other) : this() {
      rpcID_ = other.rpcID_;
      senderMailBox_ = other.senderMailBox_ != null ? other.senderMailBox_.Clone() : null;
      serviceName_ = other.serviceName_;
      methodName_ = other.methodName_;
      randomShard_ = other.randomShard_;
      shardID_ = other.shardID_;
      notifyOnly_ = other.notifyOnly_;
      rpcType_ = other.rpcType_;
      args_ = other.args_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpc Clone() {
      return new ServiceRpc(this);
    }

    /// <summary>Field number for the "RpcID" field.</summary>
    public const int RpcIDFieldNumber = 1;
    private uint rpcID_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint RpcID {
      get { return rpcID_; }
      set {
        rpcID_ = value;
      }
    }

    /// <summary>Field number for the "SenderMailBox" field.</summary>
    public const int SenderMailBoxFieldNumber = 2;
    private global::LPS.Common.Rpc.InnerMessages.MailBox senderMailBox_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::LPS.Common.Rpc.InnerMessages.MailBox SenderMailBox {
      get { return senderMailBox_; }
      set {
        senderMailBox_ = value;
      }
    }

    /// <summary>Field number for the "ServiceName" field.</summary>
    public const int ServiceNameFieldNumber = 3;
    private string serviceName_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string ServiceName {
      get { return serviceName_; }
      set {
        serviceName_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "MethodName" field.</summary>
    public const int MethodNameFieldNumber = 4;
    private string methodName_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string MethodName {
      get { return methodName_; }
      set {
        methodName_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "RandomShard" field.</summary>
    public const int RandomShardFieldNumber = 5;
    private bool randomShard_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool RandomShard {
      get { return randomShard_; }
      set {
        randomShard_ = value;
      }
    }

    /// <summary>Field number for the "ShardID" field.</summary>
    public const int ShardIDFieldNumber = 6;
    private uint shardID_;
    /// <summary>
    /// -1 for random
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint ShardID {
      get { return shardID_; }
      set {
        shardID_ = value;
      }
    }

    /// <summary>Field number for the "NotifyOnly" field.</summary>
    public const int NotifyOnlyFieldNumber = 7;
    private bool notifyOnly_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool NotifyOnly {
      get { return notifyOnly_; }
      set {
        notifyOnly_ = value;
      }
    }

    /// <summary>Field number for the "RpcType" field.</summary>
    public const int RpcTypeFieldNumber = 8;
    private global::LPS.Common.Rpc.InnerMessages.ServiceRpcType rpcType_ = global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::LPS.Common.Rpc.InnerMessages.ServiceRpcType RpcType {
      get { return rpcType_; }
      set {
        rpcType_ = value;
      }
    }

    /// <summary>Field number for the "Args" field.</summary>
    public const int ArgsFieldNumber = 9;
    private static readonly pb::FieldCodec<global::Google.Protobuf.WellKnownTypes.Any> _repeated_args_codec
        = pb::FieldCodec.ForMessage(74, global::Google.Protobuf.WellKnownTypes.Any.Parser);
    private readonly pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> args_ = new pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> Args {
      get { return args_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as ServiceRpc);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(ServiceRpc other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (RpcID != other.RpcID) return false;
      if (!object.Equals(SenderMailBox, other.SenderMailBox)) return false;
      if (ServiceName != other.ServiceName) return false;
      if (MethodName != other.MethodName) return false;
      if (RandomShard != other.RandomShard) return false;
      if (ShardID != other.ShardID) return false;
      if (NotifyOnly != other.NotifyOnly) return false;
      if (RpcType != other.RpcType) return false;
      if(!args_.Equals(other.args_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (RpcID != 0) hash ^= RpcID.GetHashCode();
      if (senderMailBox_ != null) hash ^= SenderMailBox.GetHashCode();
      if (ServiceName.Length != 0) hash ^= ServiceName.GetHashCode();
      if (MethodName.Length != 0) hash ^= MethodName.GetHashCode();
      if (RandomShard != false) hash ^= RandomShard.GetHashCode();
      if (ShardID != 0) hash ^= ShardID.GetHashCode();
      if (NotifyOnly != false) hash ^= NotifyOnly.GetHashCode();
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) hash ^= RpcType.GetHashCode();
      hash ^= args_.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (RpcID != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcID);
      }
      if (senderMailBox_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(SenderMailBox);
      }
      if (ServiceName.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(ServiceName);
      }
      if (MethodName.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(MethodName);
      }
      if (RandomShard != false) {
        output.WriteRawTag(40);
        output.WriteBool(RandomShard);
      }
      if (ShardID != 0) {
        output.WriteRawTag(48);
        output.WriteUInt32(ShardID);
      }
      if (NotifyOnly != false) {
        output.WriteRawTag(56);
        output.WriteBool(NotifyOnly);
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        output.WriteRawTag(64);
        output.WriteEnum((int) RpcType);
      }
      args_.WriteTo(output, _repeated_args_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (RpcID != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcID);
      }
      if (senderMailBox_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(SenderMailBox);
      }
      if (ServiceName.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(ServiceName);
      }
      if (MethodName.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(MethodName);
      }
      if (RandomShard != false) {
        output.WriteRawTag(40);
        output.WriteBool(RandomShard);
      }
      if (ShardID != 0) {
        output.WriteRawTag(48);
        output.WriteUInt32(ShardID);
      }
      if (NotifyOnly != false) {
        output.WriteRawTag(56);
        output.WriteBool(NotifyOnly);
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        output.WriteRawTag(64);
        output.WriteEnum((int) RpcType);
      }
      args_.WriteTo(ref output, _repeated_args_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (RpcID != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(RpcID);
      }
      if (senderMailBox_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(SenderMailBox);
      }
      if (ServiceName.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(ServiceName);
      }
      if (MethodName.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(MethodName);
      }
      if (RandomShard != false) {
        size += 1 + 1;
      }
      if (ShardID != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(ShardID);
      }
      if (NotifyOnly != false) {
        size += 1 + 1;
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) RpcType);
      }
      size += args_.CalculateSize(_repeated_args_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(ServiceRpc other) {
      if (other == null) {
        return;
      }
      if (other.RpcID != 0) {
        RpcID = other.RpcID;
      }
      if (other.senderMailBox_ != null) {
        if (senderMailBox_ == null) {
          SenderMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
        }
        SenderMailBox.MergeFrom(other.SenderMailBox);
      }
      if (other.ServiceName.Length != 0) {
        ServiceName = other.ServiceName;
      }
      if (other.MethodName.Length != 0) {
        MethodName = other.MethodName;
      }
      if (other.RandomShard != false) {
        RandomShard = other.RandomShard;
      }
      if (other.ShardID != 0) {
        ShardID = other.ShardID;
      }
      if (other.NotifyOnly != false) {
        NotifyOnly = other.NotifyOnly;
      }
      if (other.RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        RpcType = other.RpcType;
      }
      args_.Add(other.args_);
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            RpcID = input.ReadUInt32();
            break;
          }
          case 18: {
            if (senderMailBox_ == null) {
              SenderMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
            }
            input.ReadMessage(SenderMailBox);
            break;
          }
          case 26: {
            ServiceName = input.ReadString();
            break;
          }
          case 34: {
            MethodName = input.ReadString();
            break;
          }
          case 40: {
            RandomShard = input.ReadBool();
            break;
          }
          case 48: {
            ShardID = input.ReadUInt32();
            break;
          }
          case 56: {
            NotifyOnly = input.ReadBool();
            break;
          }
          case 64: {
            RpcType = (global::LPS.Common.Rpc.InnerMessages.ServiceRpcType) input.ReadEnum();
            break;
          }
          case 74: {
            args_.AddEntriesFrom(input, _repeated_args_codec);
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            RpcID = input.ReadUInt32();
            break;
          }
          case 18: {
            if (senderMailBox_ == null) {
              SenderMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
            }
            input.ReadMessage(SenderMailBox);
            break;
          }
          case 26: {
            ServiceName = input.ReadString();
            break;
          }
          case 34: {
            MethodName = input.ReadString();
            break;
          }
          case 40: {
            RandomShard = input.ReadBool();
            break;
          }
          case 48: {
            ShardID = input.ReadUInt32();
            break;
          }
          case 56: {
            NotifyOnly = input.ReadBool();
            break;
          }
          case 64: {
            RpcType = (global::LPS.Common.Rpc.InnerMessages.ServiceRpcType) input.ReadEnum();
            break;
          }
          case 74: {
            args_.AddEntriesFrom(ref input, _repeated_args_codec);
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class ServiceRpcCallBack : pb::IMessage<ServiceRpcCallBack>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<ServiceRpcCallBack> _parser = new pb::MessageParser<ServiceRpcCallBack>(() => new ServiceRpcCallBack());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<ServiceRpcCallBack> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Common.Rpc.InnerMessages.ServiceRpcReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpcCallBack() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpcCallBack(ServiceRpcCallBack other) : this() {
      rpcID_ = other.rpcID_;
      rpcType_ = other.rpcType_;
      result_ = other.result_ != null ? other.result_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ServiceRpcCallBack Clone() {
      return new ServiceRpcCallBack(this);
    }

    /// <summary>Field number for the "RpcID" field.</summary>
    public const int RpcIDFieldNumber = 1;
    private uint rpcID_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint RpcID {
      get { return rpcID_; }
      set {
        rpcID_ = value;
      }
    }

    /// <summary>Field number for the "RpcType" field.</summary>
    public const int RpcTypeFieldNumber = 2;
    private global::LPS.Common.Rpc.InnerMessages.ServiceRpcType rpcType_ = global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::LPS.Common.Rpc.InnerMessages.ServiceRpcType RpcType {
      get { return rpcType_; }
      set {
        rpcType_ = value;
      }
    }

    /// <summary>Field number for the "Result" field.</summary>
    public const int ResultFieldNumber = 3;
    private global::Google.Protobuf.WellKnownTypes.Any result_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Google.Protobuf.WellKnownTypes.Any Result {
      get { return result_; }
      set {
        result_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as ServiceRpcCallBack);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(ServiceRpcCallBack other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (RpcID != other.RpcID) return false;
      if (RpcType != other.RpcType) return false;
      if (!object.Equals(Result, other.Result)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (RpcID != 0) hash ^= RpcID.GetHashCode();
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) hash ^= RpcType.GetHashCode();
      if (result_ != null) hash ^= Result.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (RpcID != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcID);
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        output.WriteRawTag(16);
        output.WriteEnum((int) RpcType);
      }
      if (result_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Result);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (RpcID != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcID);
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        output.WriteRawTag(16);
        output.WriteEnum((int) RpcType);
      }
      if (result_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Result);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (RpcID != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(RpcID);
      }
      if (RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) RpcType);
      }
      if (result_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Result);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(ServiceRpcCallBack other) {
      if (other == null) {
        return;
      }
      if (other.RpcID != 0) {
        RpcID = other.RpcID;
      }
      if (other.RpcType != global::LPS.Common.Rpc.InnerMessages.ServiceRpcType.ServerToService) {
        RpcType = other.RpcType;
      }
      if (other.result_ != null) {
        if (result_ == null) {
          Result = new global::Google.Protobuf.WellKnownTypes.Any();
        }
        Result.MergeFrom(other.Result);
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            RpcID = input.ReadUInt32();
            break;
          }
          case 16: {
            RpcType = (global::LPS.Common.Rpc.InnerMessages.ServiceRpcType) input.ReadEnum();
            break;
          }
          case 26: {
            if (result_ == null) {
              Result = new global::Google.Protobuf.WellKnownTypes.Any();
            }
            input.ReadMessage(Result);
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            RpcID = input.ReadUInt32();
            break;
          }
          case 16: {
            RpcType = (global::LPS.Common.Rpc.InnerMessages.ServiceRpcType) input.ReadEnum();
            break;
          }
          case 26: {
            if (result_ == null) {
              Result = new global::Google.Protobuf.WellKnownTypes.Any();
            }
            input.ReadMessage(Result);
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
