# Little Princess Server

`Little Princess Server`是面向网络游戏开发的服务端框架，它具有以下特性：

1. 纯C#开发，使用多线程和协程来调度并发
2. 同时支持状态同步和帧同步
3. 利用多进程支持服务器Mesh
4. 对无状态和有状态业务提供不同的业务，有状态业务提供`Stateful Service`，无状态业务提供`Stateless Service`
5. 通过`ILRuntime`支持服务端热更新
6. 采用`protobuf`协议，方便对接各种语言的客户端
7. 内核精简，开箱即用，也支持插件化定制功能