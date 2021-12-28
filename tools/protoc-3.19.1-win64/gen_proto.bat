cd bin

for %%F in (..\..\..\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\Core\Rpc\InnerMessages\ProtobufDefs --csharp_out=..\..\..\Core\Rpc\InnerMessages\ProtobufDefs
)
