// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: database_manager_inner_rpc.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace LPS.Server.Rpc.InnerMessages {

  /// <summary>Holder for reflection information generated from database_manager_inner_rpc.proto</summary>
  public static partial class DatabaseManagerInnerRpcReflection {

    #region Descriptor
    /// <summary>File descriptor for database_manager_inner_rpc.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static DatabaseManagerInnerRpcReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiBkYXRhYmFzZV9tYW5hZ2VyX2lubmVyX3JwYy5wcm90bxIcTFBTLlNlcnZl",
            "ci5ScGMuSW5uZXJNZXNzYWdlcxoZZ29vZ2xlL3Byb3RvYnVmL2FueS5wcm90",
            "byJiChdEYXRhYmFzZU1hbmFnZXJJbm5lclJwYxINCgVycGNJZBgBIAEoDRIU",
            "Cgxpbm5lckFwaU5hbWUYAiABKAkSIgoEQXJncxgDIAMoCzIULmdvb2dsZS5w",
            "cm90b2J1Zi5BbnkiTgoaRGF0YWJhc2VNYW5hZ2VySW5uZXJScGNSZXMSDQoF",
            "cnBjSWQYASABKA0SIQoDUmVzGAIgASgLMhQuZ29vZ2xlLnByb3RvYnVmLkFu",
            "eWIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Google.Protobuf.WellKnownTypes.AnyReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpc), global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpc.Parser, new[]{ "RpcId", "InnerApiName", "Args" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpcRes), global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpcRes.Parser, new[]{ "RpcId", "Res" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class DatabaseManagerInnerRpc : pb::IMessage<DatabaseManagerInnerRpc>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<DatabaseManagerInnerRpc> _parser = new pb::MessageParser<DatabaseManagerInnerRpc>(() => new DatabaseManagerInnerRpc());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<DatabaseManagerInnerRpc> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpcReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpc() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpc(DatabaseManagerInnerRpc other) : this() {
      rpcId_ = other.rpcId_;
      innerApiName_ = other.innerApiName_;
      args_ = other.args_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpc Clone() {
      return new DatabaseManagerInnerRpc(this);
    }

    /// <summary>Field number for the "rpcId" field.</summary>
    public const int RpcIdFieldNumber = 1;
    private uint rpcId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint RpcId {
      get { return rpcId_; }
      set {
        rpcId_ = value;
      }
    }

    /// <summary>Field number for the "innerApiName" field.</summary>
    public const int InnerApiNameFieldNumber = 2;
    private string innerApiName_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string InnerApiName {
      get { return innerApiName_; }
      set {
        innerApiName_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "Args" field.</summary>
    public const int ArgsFieldNumber = 3;
    private static readonly pb::FieldCodec<global::Google.Protobuf.WellKnownTypes.Any> _repeated_args_codec
        = pb::FieldCodec.ForMessage(26, global::Google.Protobuf.WellKnownTypes.Any.Parser);
    private readonly pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> args_ = new pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> Args {
      get { return args_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as DatabaseManagerInnerRpc);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(DatabaseManagerInnerRpc other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (RpcId != other.RpcId) return false;
      if (InnerApiName != other.InnerApiName) return false;
      if(!args_.Equals(other.args_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (RpcId != 0) hash ^= RpcId.GetHashCode();
      if (InnerApiName.Length != 0) hash ^= InnerApiName.GetHashCode();
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
      if (RpcId != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcId);
      }
      if (InnerApiName.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(InnerApiName);
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
      if (RpcId != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcId);
      }
      if (InnerApiName.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(InnerApiName);
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
      if (RpcId != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(RpcId);
      }
      if (InnerApiName.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(InnerApiName);
      }
      size += args_.CalculateSize(_repeated_args_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(DatabaseManagerInnerRpc other) {
      if (other == null) {
        return;
      }
      if (other.RpcId != 0) {
        RpcId = other.RpcId;
      }
      if (other.InnerApiName.Length != 0) {
        InnerApiName = other.InnerApiName;
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
            RpcId = input.ReadUInt32();
            break;
          }
          case 18: {
            InnerApiName = input.ReadString();
            break;
          }
          case 26: {
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
            RpcId = input.ReadUInt32();
            break;
          }
          case 18: {
            InnerApiName = input.ReadString();
            break;
          }
          case 26: {
            args_.AddEntriesFrom(ref input, _repeated_args_codec);
            break;
          }
        }
      }
    }
    #endif

  }

  public sealed partial class DatabaseManagerInnerRpcRes : pb::IMessage<DatabaseManagerInnerRpcRes>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<DatabaseManagerInnerRpcRes> _parser = new pb::MessageParser<DatabaseManagerInnerRpcRes>(() => new DatabaseManagerInnerRpcRes());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<DatabaseManagerInnerRpcRes> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Server.Rpc.InnerMessages.DatabaseManagerInnerRpcReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpcRes() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpcRes(DatabaseManagerInnerRpcRes other) : this() {
      rpcId_ = other.rpcId_;
      res_ = other.res_ != null ? other.res_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public DatabaseManagerInnerRpcRes Clone() {
      return new DatabaseManagerInnerRpcRes(this);
    }

    /// <summary>Field number for the "rpcId" field.</summary>
    public const int RpcIdFieldNumber = 1;
    private uint rpcId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public uint RpcId {
      get { return rpcId_; }
      set {
        rpcId_ = value;
      }
    }

    /// <summary>Field number for the "Res" field.</summary>
    public const int ResFieldNumber = 2;
    private global::Google.Protobuf.WellKnownTypes.Any res_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Google.Protobuf.WellKnownTypes.Any Res {
      get { return res_; }
      set {
        res_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as DatabaseManagerInnerRpcRes);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(DatabaseManagerInnerRpcRes other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (RpcId != other.RpcId) return false;
      if (!object.Equals(Res, other.Res)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (RpcId != 0) hash ^= RpcId.GetHashCode();
      if (res_ != null) hash ^= Res.GetHashCode();
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
      if (RpcId != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcId);
      }
      if (res_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Res);
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
      if (RpcId != 0) {
        output.WriteRawTag(8);
        output.WriteUInt32(RpcId);
      }
      if (res_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Res);
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
      if (RpcId != 0) {
        size += 1 + pb::CodedOutputStream.ComputeUInt32Size(RpcId);
      }
      if (res_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Res);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(DatabaseManagerInnerRpcRes other) {
      if (other == null) {
        return;
      }
      if (other.RpcId != 0) {
        RpcId = other.RpcId;
      }
      if (other.res_ != null) {
        if (res_ == null) {
          Res = new global::Google.Protobuf.WellKnownTypes.Any();
        }
        Res.MergeFrom(other.Res);
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
            RpcId = input.ReadUInt32();
            break;
          }
          case 18: {
            if (res_ == null) {
              Res = new global::Google.Protobuf.WellKnownTypes.Any();
            }
            input.ReadMessage(Res);
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
            RpcId = input.ReadUInt32();
            break;
          }
          case 18: {
            if (res_ == null) {
              Res = new global::Google.Protobuf.WellKnownTypes.Any();
            }
            input.ReadMessage(Res);
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
