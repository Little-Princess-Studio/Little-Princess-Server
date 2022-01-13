#!bin/bash

cd bin
chmod 755 protoc

DEF_FILE_DIR_SERVER=../../../LSP.Server/Core/Rpc/InnerMessages/ProtobufDefs
DEF_FILE_DIR_COMMON=../../../LSP.Common/Core/Rpc/InnerMessages/ProtobufDefs

ls $DEF_FILE_DIR | grep .proto | xargs -I filename ./protoc filename --proto_path=$DEF_FILE_DIR_SERVER --csharp_out=$DEF_FILE_DIR_SERVER
ls $DEF_FILE_DIR | grep .proto | xargs -I filename ./protoc filename --proto_path=$DEF_FILEDEF_FILE_DIR_COMMON_DIR --csharp_out=$DEF_FILE_DIR_COMMON
