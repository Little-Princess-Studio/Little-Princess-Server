cd bin

for %%F in (..\..\..\LPS.Server\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF^
    --proto_path=..\..\..\LPS.Server\Core\Rpc\InnerMessages\ProtobufDefs^
    --proto_path=..\..\..\LPS.Common\Core\RPC\InnerMessages\ProtobufDefs^
    --csharp_out=..\..\..\LPS.Server\Core\Rpc\InnerMessages\ProtobufDefs
)

for %%F in (..\..\..\LPS.Common\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\LPS.Common\Core\Rpc\InnerMessages\ProtobufDefs --csharp_out=..\..\..\LPS.Common\Core\Rpc\InnerMessages\ProtobufDefs
)
