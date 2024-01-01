using GTFO.API;
using SNetwork;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;
using TMPro;
using UnityEngine;
using TheArchive.Utilities;

namespace Hikaria.NetworkQualityTracker.Managers;
public class NetworkQualityManager
{
    public static void RegisterPlayer(SNet_Player player)
    {
        NetworkQualityDataLookup.TryAdd(player.Lookup, new(player));
    }

    public static void UnregisterPlayer(SNet_Player player)
    {
        NetworkQualityDataLookup.Remove(player.Lookup);
        if (player.IsLocal)
        {
            NetworkQualityDataLookup.Clear();
        }
    }

    public static void SendHeartbeats()
    {
        foreach (var data in NetworkQualityDataLookup.Values)
        {
            if (data.Owner.IsLocal || !HeartbeatListeners.Any(p => p.Lookup == data.Owner.Lookup))
                continue;

            data.SendHeartbeat();
        }
    }

    public static void SendToMasterQualityReport()
    {
        if (NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var quality))
        {
            NetworkAPI.InvokeEvent(typeof(pToMasterNetworkQualityReport).FullName, quality.GetToMasterReport(), HeartbeatListeners, SNet_ChannelType.GameNonCritical);
        }
    }

    public static short LocalToMasterLatency
    {
        get
        {
            if (NetworkQualityDataLookup.TryGetValue(SNet.Master.Lookup, out var quality))
            {
                return quality.ToMasterLatency;
            }
            return 0;
        }
    }

    public static bool IsMasterHasHeartbeat => HeartbeatListeners.Any(p => p.IsMaster) || SNet.IsMaster;
    public static List<SNet_Player> HeartbeatListeners { get; } = new List<SNet_Player>();
    public static Dictionary<ulong, NetworkQuality> NetworkQualityDataLookup { get; } = new Dictionary<ulong, NetworkQuality>();
    public static long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static int WatermarkOffsetX { get; set; }
    public static int WatermarkOffsetY { get; set; }
    public static int PageLoadoutOffsetX { get; set; }
    public static int PageLoadoutOffsetY { get; set; }

    public static TextMeshPro NetworkQualityText { get; set; }
    public static Dictionary<int, TextMeshPro> PageLoadoutTextMeshes = new();
    public static TextMeshPro WatermarkTextMeshPro => GuiManager.WatermarkLayer.m_watermark.m_watermarkText;
}

public class NetworkQuality
{
    public NetworkQuality(SNet_Player player) { Owner = player; }

    public void ReceiveHeartbeat(pHeartbeat data)
    {
        SendHeartbeatAck(data);
    }

    public void ReceiveHeartbeatAck(pHeartbeatAck data)
    {
        var heartbeatIndex = data.Index;
        Latency = (short)(Owner.IsLocal || Owner.IsBot || !NetworkQualityManager.IsMasterHasHeartbeat ? 0 : NetworkQualityManager.CurrentTime - HeartbeatSendTimeLookup[heartbeatIndex]);
        if (Latency < MinLatency)
        {
            MinLatency = Latency;
        }
        if (Latency > MaxLatency)
        {
            MaxLatency = Latency;
        }
        NetworkJitter = (short)(MaxLatency - MinLatency);
        if (LatencyHistory.Count >= HeartbeatResetCount)
        {
            LatencyHistory.Dequeue();
        }
        LatencyHistory.Enqueue(Latency);
        if (heartbeatIndex > LastReceivedHeartbeatAckIndex + 1)
        {
            PacketLoss += (short)(heartbeatIndex - LastReceivedHeartbeatAckIndex - 1);
        }
        LastReceivedHeartbeatAckIndex = heartbeatIndex;
        if (heartbeatIndex > HeartbeatResetCount)
        {
            Reset();
        }
    }

    public void ReceiveNetworkQualityReport(pToMasterNetworkQualityReport data)
    {
        ToMasterLatency = data.ToMasterLatency;
        ToMasterPacketLossRate = data.ToMasterPacketLossRate;
    }

    public void SendHeartbeat()
    {
        HeartbeatSendIndex++;
        HeartbeatSendTimeLookup[HeartbeatSendIndex] = NetworkQualityManager.CurrentTime;
        NetworkAPI.InvokeEvent<pHeartbeat>(typeof(pHeartbeat).FullName, new(HeartbeatSendIndex), Owner, SNet_ChannelType.GameNonCritical);
    }

    public void SendHeartbeatAck(pHeartbeat data)
    {
        NetworkAPI.InvokeEvent<pHeartbeatAck>(typeof(pHeartbeatAck).FullName, new(data.Index), Owner, SNet_ChannelType.GameNonCritical);
    }

    public void Reset()
    {
        MaxLatency = 0;
        MinLatency = short.MaxValue;
        HeartbeatSendIndex = 0;
        PacketLoss = 0;
    }

    public void GetReport(out string latencyText, out string jitterText, out string packetLossText)
    {
        latencyText = string.Format(Settings.LatencyFormat, $"<{LatencyColorHexString}>{(SNet.IsMaster ? "Host" : $"{Latency}ms")}</color>");
        jitterText = string.Format(Settings.NetworkJitterFormat, $"<{NetworkJitterColorHexString}>{(SNet.IsMaster ? "Host" : $"{NetworkJitter}ms")}</color>");
        packetLossText = string.Format(Settings.PacketLossFormat, $"<{PacketLossColorHexString}>{(SNet.IsMaster ? "Host" : $"{PacketLossRate}%")}</color>");
    }

    public pToMasterNetworkQualityReport GetToMasterReport()
    {
        return new(ToMasterLatency, ToMasterPacketLossRate);
    }

    private static string GetColorHexString(float min, float max, float value)
    {
        Color green = new(0f, 0.7206f, 0f, 0.3137f);
        Color red = new(0.7206f, 0f, 0f, 0.3137f);
        float t = (value - min) / (max - min);
        return Color.Lerp(green, red, t).ToHexString();
    }

    public string LatencyColorHexString => GetColorHexString(60, 150, Latency);
    public string NetworkJitterColorHexString => GetColorHexString(20, 100, NetworkJitter);
    public string PacketLossColorHexString => GetColorHexString(0, 5, PacketLossRate);
    public SNet_Player Owner { get; private set; }
    public Queue<short> LatencyHistory { get; private set; } = new(HeartbeatResetCount);
    public short HeartbeatSendIndex { get; private set; } = 0;
    public short PacketLossRate => (short)(HeartbeatSendIndex == 0 ? 0 : (PacketLoss / HeartbeatSendIndex * 100f));
    public short PacketLoss { get; private set; } = 0;
    public short Latency { get; private set; } = 0;
    public short NetworkJitter { get; private set; } = 0;
    public short ToMasterLatency { get; private set; } = 0;
    public short ToMasterPacketLossRate { get; private set; } = 0;

    private Dictionary<short, long> HeartbeatSendTimeLookup = new();
    private short MinLatency = short.MaxValue;
    private short MaxLatency = 0;
    private short LastReceivedHeartbeatAckIndex = 0;
    private const short HeartbeatResetCount = 50;
}