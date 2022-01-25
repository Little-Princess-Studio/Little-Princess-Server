#!bin/bash

cd bin
chmod 755 protoc

DEF_FILE_DIR_SERVER=../../../LPS.Server/Core/Rpc/InnerMessages/ProtobufDefs
DEF_FILE_DIR_COMMON=../../../LPS.Common/Core/Rpc/InnerMessages/ProtobufDefs

ls $DEF_FILE_DIR_SERVER | grep .proto | xargs -I filename ./protoc filename --proto_path=$DEF_FILE_DIR_SERVER --proto_path=$DEF_FILE_DIR_COMMON --csharp_out=$DEF_FILE_DIR_SERVER
ls $DEF_FILE_DIR_COMMON | grep .proto | xargs -I filename ./protoc filename --proto_path=$DEF_FILE_DIR_COMMON --csharp_out=$DEF_FILE_DIR_COMMON
