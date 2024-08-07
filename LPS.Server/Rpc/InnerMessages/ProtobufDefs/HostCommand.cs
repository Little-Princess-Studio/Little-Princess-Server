// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: host_command.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace LPS.Server.Rpc.InnerMessages {

  /// <summary>Holder for reflection information generated from host_command.proto</summary>
  public static partial class HostCommandReflection {

    #region Descriptor
    /// <summary>File descriptor for host_command.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static HostCommandReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChJob3N0X2NvbW1hbmQucHJvdG8SHExQUy5TZXJ2ZXIuUnBjLklubmVyTWVz",
            "c2FnZXMaGWdvb2dsZS9wcm90b2J1Zi9hbnkucHJvdG8ibgoLSG9zdENvbW1h",
            "bmQSOwoEVHlwZRgBIAEoDjItLkxQUy5TZXJ2ZXIuUnBjLklubmVyTWVzc2Fn",
            "ZXMuSG9zdENvbW1hbmRUeXBlEiIKBEFyZ3MYAiADKAsyFC5nb29nbGUucHJv",
            "dG9idWYuQW55KoUBCg9Ib3N0Q29tbWFuZFR5cGUSDwoLU3luY1NlcnZlcnMQ",
            "ABINCglTeW5jR2F0ZXMQARIICgRPcGVuEAISCAoEU3RvcBADEhYKElN5bmNT",
            "ZXJ2aWNlTWFuYWdlchAEEhMKD1JlY29ubmVjdFNlcnZlchAFEhEKDVJlY29u",
            "bmVjdEdhdGUQBmIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Google.Protobuf.WellKnownTypes.AnyReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::LPS.Server.Rpc.InnerMessages.HostCommandType), }, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::LPS.Server.Rpc.InnerMessages.HostCommand), global::LPS.Server.Rpc.InnerMessages.HostCommand.Parser, new[]{ "Type", "Args" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Enums
  public enum HostCommandType {
    [pbr::OriginalName("SyncServers")] SyncServers = 0,
    [pbr::OriginalName("SyncGates")] SyncGates = 1,
    [pbr::OriginalName("Open")] Open = 2,
    [pbr::OriginalName("Stop")] Stop = 3,
    [pbr::OriginalName("SyncServiceManager")] SyncServiceManager = 4,
    [pbr::OriginalName("ReconnectServer")] ReconnectServer = 5,
    [pbr::OriginalName("ReconnectGate")] ReconnectGate = 6,
  }

  #endregion

  #region Messages
  public sealed partial class HostCommand : pb::IMessage<HostCommand>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HostCommand> _parser = new pb::MessageParser<HostCommand>(() => new HostCommand());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HostCommand> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::LPS.Server.Rpc.InnerMessages.HostCommandReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HostCommand() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HostCommand(HostCommand other) : this() {
      type_ = other.type_;
      args_ = other.args_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HostCommand Clone() {
      return new HostCommand(this);
    }

    /// <summary>Field number for the "Type" field.</summary>
    public const int TypeFieldNumber = 1;
    private global::LPS.Server.Rpc.InnerMessages.HostCommandType type_ = global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::LPS.Server.Rpc.InnerMessages.HostCommandType Type {
      get { return type_; }
      set {
        type_ = value;
      }
    }

    /// <summary>Field number for the "Args" field.</summary>
    public const int ArgsFieldNumber = 2;
    private static readonly pb::FieldCodec<global::Google.Protobuf.WellKnownTypes.Any> _repeated_args_codec
        = pb::FieldCodec.ForMessage(18, global::Google.Protobuf.WellKnownTypes.Any.Parser);
    private readonly pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> args_ = new pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Google.Protobuf.WellKnownTypes.Any> Args {
      get { return args_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HostCommand);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HostCommand other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Type != other.Type) return false;
      if(!args_.Equals(other.args_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Type != global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers) hash ^= Type.GetHashCode();
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
      if (Type != global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Type);
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
      if (Type != global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Type);
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
      if (Type != global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Type);
      }
      size += args_.CalculateSize(_repeated_args_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HostCommand other) {
      if (other == null) {
        return;
      }
      if (other.Type != global::LPS.Server.Rpc.InnerMessages.HostCommandType.SyncServers) {
        Type = other.Type;
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
            Type = (global::LPS.Server.Rpc.InnerMessages.HostCommandType) input.ReadEnum();
            break;
          }
          case 18: {
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
            Type = (global::LPS.Server.Rpc.InnerMessages.HostCommandType) input.ReadEnum();
            break;
          }
          case 18: {
            args_.AddEntriesFrom(ref input, _repeated_args_codec);
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
