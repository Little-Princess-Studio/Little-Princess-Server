cd bin

for %%F in (..\..\..\LSP.Server\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\LSP.Server\Core\Rpc\InnerMessages\ProtobufDefs --csharp_out=..\..\..\LSP.Server\Core\Rpc\InnerMessages\ProtobufDefs
)

for %%F in (..\..\..\LSP.Common\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\LSP.Common\Core\Rpc\InnerMessages\ProtobufDefs --csharp_out=..\..\..\LSP.Common\Core\Rpc\InnerMessages\ProtobufDefs
)
