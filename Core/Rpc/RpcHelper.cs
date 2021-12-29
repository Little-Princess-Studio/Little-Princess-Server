using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LPS.Core.Debug;
using LPS.Core.Entity;
using LPS.Core.Ipc;
using LPS.Core.Rpc.InnerMessages;

namespace LPS.Core.Rpc
{
    public static class RpcHelper
    {

#nullable enable
        public static async Task HandleMessage(
            Connection conn,
            Func<bool> stopCondition,
            Action<Message> onGotMessage,
            Action? onExitLoop)
#nullable disable
        {
            var buf = new byte[512];
            var messageBuf = new MessageBuffer();
            var socket = conn.Socket;

            try
            {
                while (conn.Status == ConnectStatus.Connected && !stopCondition())
                {
                    var len = await socket.ReceiveAsync(buf, SocketFlags.None, conn.TokenSource.Token);

                    if (len < 1)
                    {
                        break;
                    }

                    if (messageBuf.TryRecieveFromRaw(buf, len, out var pkg))
                    {
                        var type = (PackageType)pkg.Header.Type;

                        var pb = PackageHelper.GetProtoBufObjectByType(type, pkg);
                        var arg = Tuple.Create(pb, conn, pkg.Header.ID);
                        var msg = new Message(type, arg);

                        Logger.Info($"msg recv: {msg.Key}");

                        onGotMessage(msg);
                    }
                }

                Logger.Debug("Connection Closed.");
            }
            catch (OperationCanceledException ex)
            {
                var ipEndPoint = (IPEndPoint)socket.RemoteEndPoint;
                Logger.Error(ex, $"IO Task canceled {ipEndPoint.Address} {ipEndPoint.Port}");
            }
            catch (Exception ex)
            {
                var ipEndPoint = (IPEndPoint)socket.RemoteEndPoint;
                Logger.Error(ex, $"Read socket data failed, socket will close {ipEndPoint.Address} {ipEndPoint.Port}");
            }

            // connections_.Remove(conn);
            onExitLoop?.Invoke();

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Close socket failed");
            }
            finally
            {
                socket.Close();
            }
        }

        public static async Task<MailBox> CreateEntityLocally(string EntityClassName, Dictionary<string, object> desc)
        {
            return null;
        }

        public static async Task<DistributeEntity> CreateEntityAnywhere()
        {
            return null;
        }
    }
}