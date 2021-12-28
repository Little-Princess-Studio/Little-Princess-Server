# RPCFlow
1. Serialize RPC request as Protobuf `EntityRPC` message;
2. Send message with target mailbox to the connected gate;
3. Gate find which server the target mailbox exists on;
4. Send the message to target server;