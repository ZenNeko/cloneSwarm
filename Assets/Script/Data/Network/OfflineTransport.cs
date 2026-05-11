using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mock NetworkTransport สำหรับ Solo / Offline mode
/// — ไม่สร้าง UDP/TCP socket ทำงานได้บนทุก platform รวมถึง WebGL
/// — ใช้เฉพาะ StartHost() โหมด Solo (ไม่มี remote client เลย)
/// </summary>
public class OfflineTransport : NetworkTransport
{
    public override ulong ServerClientId => 0;

    public override void Initialize(NetworkManager networkManager = null) { }

    public override bool StartServer() => true;
    public override bool StartClient() => true;

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery) { }

    public override NetworkEvent PollEvent(out ulong clientId,
                                           out ArraySegment<byte> payload,
                                           out float receiveTime)
    {
        clientId    = 0;
        payload     = default;
        receiveTime = Time.realtimeSinceStartup;
        return NetworkEvent.Nothing;
    }

    public override ulong GetCurrentRtt(ulong clientId)          => 0;
    public override void  DisconnectRemoteClient(ulong clientId) { }
    public override void  DisconnectLocalClient()                { }
    public override void  Shutdown()                             { }
}
