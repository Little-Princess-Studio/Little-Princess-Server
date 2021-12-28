#!bin/sh

cd bin
chmod 755 protoc

DEF_FILE_DIR=../../../Core/Rpc/InnerMessages/ProtobufDefs

ls $DEF_FILE_DIR | grep .proto | xargs -I filename ./protoc filename --proto_path=$DEF_FILE_DIR --csharp_out=$DEF_FILE_DIR
