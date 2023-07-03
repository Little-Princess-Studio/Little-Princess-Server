// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: client_create_entity.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace LPS.Common.Rpc.InnerMessages {

  /// <summary>Holder for reflection information generated from client_create_entity.proto</summary>
  public static partial class ClientCreateEntityReflection {

    #region Descriptor
    /// <summary>File descriptor for client_create_entity.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ClientCreateEntityReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChpjbGllbnRfY3JlYXRlX2VudGl0eS5wcm90bxIcTFBTLkNvbW1vbi5ScGMu",
            "SW5uZXJNZXNzYWdlcxoNbWFpbGJveC5wcm90byJxChJDbGllbnRDcmVhdGVF",
            "bnRpdHkSFwoPRW50aXR5Q2xhc3NOYW1lGAEgASgJEkIKE1NlcnZlckNsaWVu",
            "dE1haWxCb3gYAiABKAsyJS5MUFMuQ29tbW9uLlJwYy5Jbm5lck1lc3NhZ2Vz",
            "Lk1haWxCb3hiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::LPS.Common.Rpc.InnerMessages.MailboxReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Common.Rpc.InnerMessages.ClientCreateEntity), global::LPS.Common.Rpc.InnerMessages.ClientCreateEntity.Parser, new[]{ "EntityClassName", "ServerClientMailBox" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class ClientCreateEntity : pb::IMessage<ClientCreateEntity>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<ClientCreateEntity> _parser = new pb::MessageParser<ClientCreateEntity>(() => new ClientCreateEntity());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<ClientCreateEntity> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Common.Rpc.InnerMessages.ClientCreateEntityReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ClientCreateEntity() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ClientCreateEntity(ClientCreateEntity other) : this() {
      entityClassName_ = other.entityClassName_;
      serverClientMailBox_ = other.serverClientMailBox_ != null ? other.serverClientMailBox_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public ClientCreateEntity Clone() {
      return new ClientCreateEntity(this);
    }

    /// <summary>Field number for the "EntityClassName" field.</summary>
    public const int EntityClassNameFieldNumber = 1;
    private string entityClassName_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string EntityClassName {
      get { return entityClassName_; }
      set {
        entityClassName_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "ServerClientMailBox" field.</summary>
    public const int ServerClientMailBoxFieldNumber = 2;
    private global::LPS.Common.Rpc.InnerMessages.MailBox serverClientMailBox_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::LPS.Common.Rpc.InnerMessages.MailBox ServerClientMailBox {
      get { return serverClientMailBox_; }
      set {
        serverClientMailBox_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as ClientCreateEntity);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(ClientCreateEntity other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (EntityClassName != other.EntityClassName) return false;
      if (!object.Equals(ServerClientMailBox, other.ServerClientMailBox)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (EntityClassName.Length != 0) hash ^= EntityClassName.GetHashCode();
      if (serverClientMailBox_ != null) hash ^= ServerClientMailBox.GetHashCode();
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
      if (EntityClassName.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(EntityClassName);
      }
      if (serverClientMailBox_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(ServerClientMailBox);
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
      if (EntityClassName.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(EntityClassName);
      }
      if (serverClientMailBox_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(ServerClientMailBox);
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
      if (EntityClassName.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(EntityClassName);
      }
      if (serverClientMailBox_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(ServerClientMailBox);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(ClientCreateEntity other) {
      if (other == null) {
        return;
      }
      if (other.EntityClassName.Length != 0) {
        EntityClassName = other.EntityClassName;
      }
      if (other.serverClientMailBox_ != null) {
        if (serverClientMailBox_ == null) {
          ServerClientMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
        }
        ServerClientMailBox.MergeFrom(other.ServerClientMailBox);
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
          case 10: {
            EntityClassName = input.ReadString();
            break;
          }
          case 18: {
            if (serverClientMailBox_ == null) {
              ServerClientMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
            }
            input.ReadMessage(ServerClientMailBox);
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
          case 10: {
            EntityClassName = input.ReadString();
            break;
          }
          case 18: {
            if (serverClientMailBox_ == null) {
              ServerClientMailBox = new global::LPS.Common.Rpc.InnerMessages.MailBox();
            }
            input.ReadMessage(ServerClientMailBox);
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
