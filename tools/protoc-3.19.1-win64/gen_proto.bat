cd bin

for %%F in (..\..\..\Core\RPC\InnerMessages\ProtobufDefs\*.proto) do (
    .\protoc.exe %%~nxF --proto_path=..\..\..\Core\RPC\InnerMessages\ProtobufDefs --csharp_out=..\..\..\Core\RPC\InnerMessages\ProtobufDefs
)
