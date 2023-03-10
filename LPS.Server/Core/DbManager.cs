// -----------------------------------------------------------------------
// <copyright file="DbManager.cs" company="Little Princess Studio">
// Copyright (c) Little Princess Studio. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace LPS.Server.Core
{
    using System.Collections.Concurrent;
    using Google.Protobuf;
    using LPS.Common.Core.Debug;
    using LPS.Server.Core.Rpc;

    /// <summary>
    /// Database Manager.
    /// </summary>
    public class DbManager : IInstance
    {
        /// <inheritdoc/>
        public string Ip { get; }

        /// <inheritdoc/>
        public int Port { get; }

        /// <inheritdoc/>
        public int HostNum { get; }

        /// <inheritdoc/>
        public InstanceType InstanceType => InstanceType.DbManager;

        private readonly TcpServer tcpDbManagerServer;
        private readonly TcpClient clientToHostManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbManager"/> class.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="hostNum">Hostnum.</param>
        /// <param name="hostManagerIp">Ip of the host manager.</param>
        /// <param name="hostManagerPort">Port of the host manager.</param>
        /// <param name="cacheInfo">Global cache info.</param>
        public DbManager(
            string ip,
            int port,
            int hostNum,
            string hostManagerIp,
            int hostManagerPort,
            (string IP, int Port, string DefaultDb) cacheInfo)
        {
            this.Ip = ip;
            this.Port = port;
            this.HostNum = hostNum;

            // tcp gate server handles msg from server/other gates
            this.tcpDbManagerServer = new(ip, port)
            {
                OnInit = this.RegisterMessageFromServerHandlers,
                OnDispose = this.UnregisterMessageFromServerHandlers,
            };

            this.clientToHostManager = new TcpClient(
                hostManagerIp,
                hostManagerPort,
                new ConcurrentQueue<(TcpClient, IMessage, bool)>());
        }

        /// <inheritdoc/>
        public void Stop()
        {
            this.clientToHostManager.Stop();
            this.tcpDbManagerServer.Stop();
        }

        /// <inheritdoc/>
        public void Loop()
        {
            Logger.Debug($"Start dbmanager at {this.Ip}:{this.Port}");

            this.tcpDbManagerServer.Run();
            this.clientToHostManager.Run();

            this.clientToHostManager.WaitForExit();
            this.tcpDbManagerServer.WaitForExit();
            Logger.Debug("DbManager Exit.");
        }

        private void RegisterMessageFromServerHandlers()
        {
        }

        private void UnregisterMessageFromServerHandlers()
        {
        }
    }
}