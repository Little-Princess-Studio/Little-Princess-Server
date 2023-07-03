cd bin

for %%F in (..\..\..\LPS.Server\Rpc\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF^
    --proto_path=..\..\..\LPS.Server\Rpc\InnerMessages\ProtobufDefs^
    --proto_path=..\..\..\LPS.Common\RPC\InnerMessages\ProtobufDefs^
    --csharp_out=..\..\..\LPS.Server\Rpc\InnerMessages\ProtobufDefs
)

for %%F in (..\..\..\LPS.Common\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\LPS.Common\Rpc\InnerMessages\ProtobufDefs --csharp_out=..\..\..\LPS.Common\Rpc\InnerMessages\ProtobufDefs
)
