using GTFO.API;
using SNetwork;
using TheArchive.Utilities;
using TMPro;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;

namespace Hikaria.NetworkQualityTracker.Managers;
public class NetworkQualityManager
{
    public static void RegisterListener(SNet_Player player)
    {
        if (player.IsBot)
        {
            return;
        }
        NetworkQualityDataLookup.TryAdd(player.Lookup, new(player));
        if (!player.IsLocal)
        {
            HeartbeatListeners.Add(player);
        }
    }

    public static void UnregisterListener(SNet_Player player)
    {
        if (player.IsBot)
        {
            return;
        }
        if (PageLoadoutQualityTextMeshes.TryGetValue(player.PlayerSlotIndex(), out var text))
        {
            text.SetText(string.Empty);
            text.ForceMeshUpdate();
        }
        if (player.IsLocal)
        {
            NetworkQualityDataLookup.Clear();
            HeartbeatListeners.Clear();
            return;
        }

        NetworkQualityDataLookup.Remove(player.Lookup);
        HeartbeatListeners.Remove(player);
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

    public static void OnMasterChanged()
    {
        if (!IsMasterHasHeartbeat)
        {
            foreach (var data in NetworkQualityDataLookup.Values)
            {
                data.OnMasterChanged();
            }
        }
    }

    public static bool IsMasterHasHeartbeat => SNet.IsMaster || HeartbeatListeners.Any(p => p.IsMaster);
    public static List<SNet_Player> HeartbeatListeners { get; } = new();
    public static Dictionary<ulong, NetworkQualityData> NetworkQualityDataLookup { get; } = new();
    public static long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static TextMeshPro WatermarkQualityTextMesh { get; set; }
    public static Dictionary<int, TextMeshPro> PageLoadoutQualityTextMeshes { get; } = new();
    public static TextMeshPro WatermarkTextPrefab => GuiManager.WatermarkLayer.m_watermark.m_watermarkText;
}

public class NetworkQualityData
{
    public NetworkQualityData(SNet_Player player) { Owner = player; }

    public void ReceiveHeartbeat(pHeartbeat data)
    {
        SendHeartbeatAck(data);
    }

    public void ReceiveHeartbeatAck(pHeartbeatAck data)
    {
        var heartbeatIndex = data.Index;
        ToLocalLatency = (short)(Owner.IsLocal || Owner.IsBot || !NetworkQualityManager.IsMasterHasHeartbeat ? 0 : NetworkQualityManager.CurrentTime - HeartbeatSendTimeLookup[heartbeatIndex]);
        if (LatencyHistory.Count >= LatencyQueueMaxCap)
        {
            LatencyHistory.Dequeue();
        }
        LatencyHistory.Enqueue(ToLocalLatency);
        ToLocalNetworkJitter = (short)(LatencyHistory.Max() - LatencyHistory.Min());
        if (heartbeatIndex > LastReceivedHeartbeatAckIndex + 1)
        {
            PacketLoss += (short)(heartbeatIndex - LastReceivedHeartbeatAckIndex - 1);
        }
        LastReceivedHeartbeatAckIndex = heartbeatIndex;
        CheckToMasterQuality();
        if (heartbeatIndex > LatencyQueueMaxCap)
        {
            Reset();
        }
    }

    public void ReceiveNetworkQualityReport(pToMasterNetworkQualityReport data)
    {
        ToMasterLatency = data.ToMasterLatency;
        ToMasterPacketLossRate = data.ToMasterPacketLoss;
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
        HeartbeatSendIndex = 0;
        PacketLoss = 0;
    }

    public void OnMasterChanged()
    {
        ToMasterLatency = 0;
        ToMasterPacketLossRate = 0;
        ToMasterNetworkJitter = 0;
    }

    public void CheckToMasterQuality()
    {
        if (!NetworkQualityManager.IsMasterHasHeartbeat)
            return;
        if (Owner.IsLocal)
        {
            if (SNet.IsMaster)
            {
                ToMasterLatency = 0;
                ToMasterNetworkJitter = 0;
                ToMasterPacketLossRate = 0;
            }
            else if (NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(SNet.Master.Lookup, out var quality))
            {
                ToMasterLatency = quality.ToLocalLatency;
                ToMasterNetworkJitter = quality.ToLocalNetworkJitter;
                ToMasterPacketLossRate = quality.ToLocalPacketLossRate;
            }
        }
    }

    public void GetToLocalReportText(out string toLocalLatencyText, out string toLocalJitterText, out string toLocalPacketLossRateText)
    {
        toLocalLatencyText = string.Format(Settings.LatencyFormat, $"<{LatencyColorHexString}>{$"{ToLocalLatency}ms"}</color>");
        toLocalJitterText = string.Format(Settings.NetworkJitterFormat, $"<{NetworkJitterColorHexString}>{$"{ToLocalNetworkJitter}ms"}</color>");
        toLocalPacketLossRateText = string.Format(Settings.PacketLossFormat, $"<{PacketLossColorHexString}>{$"{ToLocalPacketLossRate}%"}</color>");
    }

    public void GetToMasterReportText(out string toMasterLatencyText, out string toMasterJitterText, out string toMasterPacketLossRateText)
    {
        toMasterLatencyText = string.Format(Settings.LatencyFormat, $"<{LatencyColorHexString}>{(!NetworkQualityManager.IsMasterHasHeartbeat ? "未知" : $"{ToMasterLatency}ms")}</color>");
        toMasterJitterText = string.Format(Settings.NetworkJitterFormat, $"<{NetworkJitterColorHexString}>{(!NetworkQualityManager.IsMasterHasHeartbeat ? "未知" : $"{ToMasterNetworkJitter}ms")}</color>");
        toMasterPacketLossRateText = string.Format(Settings.PacketLossFormat, $"<{PacketLossColorHexString}>{(!NetworkQualityManager.IsMasterHasHeartbeat ? "未知" : $"{ToMasterPacketLossRate}%")}</color>");
    }

    public pToMasterNetworkQualityReport GetToMasterReportData()
    {
        return new(ToMasterLatency, ToMasterNetworkJitter, ToMasterPacketLossRate);
    }

    private static string GetColorHexString(float min, float max, float value)
    {
        Color green = new(0f, 0.7206f, 0f, 0.3137f);
        Color red = new(0.7206f, 0f, 0f, 0.3137f);
        float t = (value - min) / (max - min);
        return Color.Lerp(green, red, t).ToHexString();
    }

    public string LatencyColorHexString => GetColorHexString(60, 150, ToLocalLatency);
    public string NetworkJitterColorHexString => GetColorHexString(20, 100, ToLocalNetworkJitter);
    public string PacketLossColorHexString => GetColorHexString(0, 5, ToLocalPacketLossRate);
    public SNet_Player Owner { get; private set; }
    public Queue<short> LatencyHistory { get; private set; } = new(LatencyQueueMaxCap);
    public short HeartbeatSendIndex { get; private set; } = 0;
    public short ToLocalPacketLossRate => (short)(HeartbeatSendIndex == 0 ? 0 : (PacketLoss / HeartbeatSendIndex * 100f));
    public short PacketLoss { get; private set; } = 0;
    public short ToLocalLatency { get; private set; } = 0;
    public short ToLocalNetworkJitter { get; private set; } = 0;
    public short ToMasterLatency { get; private set; } = 0;
    public short ToMasterNetworkJitter { get; private set; } = 0;
    public short ToMasterPacketLossRate { get; private set; } = 0;

    private Dictionary<short, long> HeartbeatSendTimeLookup = new();
    private short LastReceivedHeartbeatAckIndex = 0;
    private const short LatencyQueueMaxCap = 50;
}