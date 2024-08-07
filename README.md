# Little Princess Server

`Little Princess Server`是面向网络游戏开发的服务端框架，它具有以下特性：

### 1. 纯C#开发与并发调度：
   完全使用C#语言开发，利用.NET Core的强大功能。
   通过多线程和协程技术，框架实现了高效的并发任务调度，确保了高吞吐量和低延迟。

### 2. 框架支持两种同步机制（In working）
   同时默认提供状态同步和帧同步的实现。

### 3. 多进程Mesh服务器支持：
   利用多进程架构，框架能够创建服务器Mesh网络，增强了服务器间的通信能力和负载均衡，提高了系统的稳定性和扩展性。

### 4. 微服务支持
   提供微服务支持，能够灵活地扩展业务。

### 5. 服务端热更新（In working）
   集成了ILRuntime，框架支持服务端代码的热更新，无需重启服务器即可更新逻辑，减少了维护成本和停机时间。
   
### 6. protobuf协议支持：
   原生protobuf支持，无私有协议，即便客户端不使用C#也能定制自己的客户端。

### 7. 服务器容灾
   支持服务器进程实例监控，关键进程能够在崩溃后自动重启并重新加入集群。

### 8. 插件化（In working）
   支持插件化，允许开发者根据需要定制功能，例如通过编写插件替换默认数据库、MQ、Cache等中间件。
