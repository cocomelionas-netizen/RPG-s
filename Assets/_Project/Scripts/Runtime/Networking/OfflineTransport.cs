using System;
using Unity.Netcode;

namespace RPG.Networking
{
    /// <summary>
    /// A transport that never talks to the network. Starting a host with it runs the whole game
    /// (server logic and the local player) inside one process with no sockets, which is how
    /// browser (WebGL) builds can play without a server: browsers cannot listen for connections.
    /// Remote clients can never connect.
    /// </summary>
    public class OfflineTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;

        public override bool IsSupported => true;

        public override void Initialize(NetworkManager networkManager = null) { }

        public override bool StartServer() => true;

        public override bool StartClient() => false;

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = 0f;
            return NetworkEvent.Nothing;
        }

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        public override void DisconnectRemoteClient(ulong clientId) { }

        public override void DisconnectLocalClient() { }

        public override void Shutdown() { }
    }
}
