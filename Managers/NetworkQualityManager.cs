using Hikaria.Core;
using Hikaria.Core.SNetworkExt;
using SNetwork;
using TheArchive.Utilities;
using TMPro;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;
using static Hikaria.NetworkQualityTracker.Managers.NetworkQualityManager;
using static Hikaria.NetworkQualityTracker.Utils.Utils;
using Version = Hikaria.Core.Version;

namespace Hikaria.NetworkQualityTracker.Managers;

public static class NetworkQualityManager
{
    public struct pHeartbeat
    {
        public ulong Index = 0UL;

        public pHeartbeat()
        {
        }
    }

    public struct pToMasterNetworkQualityReport
    {
        public pToMasterNetworkQualityReport(uint toMasterLatency, uint toMasterNetworkJitter, uint toMasterPacketLossRate)
        {
            ToMasterLatency = toMasterLatency;
            ToMasterPacketLoss = toMasterPacketLossRate;
            ToMasterNetworkJitter = toMasterNetworkJitter;
        }

        public uint ToMasterLatency;
        public uint ToMasterPacketLoss;
        public uint ToMasterNetworkJitter;
    }

    public struct pHeartbeatAck
    {
        public ulong Index = 0UL;

        public pHeartbeatAck()
        {
        }
    }

    private static readonly Version Miniver = new("1.1.0");

    public static void Setup()
    {
        CoreAPI.OnPlayerModsSynced += OnPlayerModsSynced;
        GameEventAPI.OnMasterChanged += OnMasterChanged;
        GameEventAPI.OnPlayerSlotChanged += OnPlayerSlotChanged;
        s_HeartbeatAction = SNetExt_BroadcastAction<pHeartbeat>.Create(typeof(pHeartbeat).FullName, OnReceiveHeartbeat, HeartbeatListenerFilter, SNet_ChannelType.GameNonCritical);
        s_ToMasterNetworkQualityReportAction = SNetExt_BroadcastAction<pToMasterNetworkQualityReport>.Create(typeof(pToMasterNetworkQualityReport).FullName, OnReceiveNetworkQualityReport, HeartbeatListenerFilter, SNet_ChannelType.GameNonCritical);
        s_HeartbeatAckPacket = SNetExt_Packet<pHeartbeatAck>.Create(typeof(pHeartbeatAck).FullName, OnReceiveHeartbeatAck, null, false, SNet_ChannelType.GameNonCritical);
        s_HeartbeatAction.OnPlayerAddedToListeners += RegisterListener;
        s_HeartbeatAction.OnPlayerRemovedFromListeners += UnregisterListener;
    }

    private static void OnPlayerSlotChanged(SNet_Player player, SNet_SlotType type, SNet_SlotHandleType handle, int index)
    {
        if (handle == SNet_SlotHandleType.Assign || handle == SNet_SlotHandleType.Set)
        {
            PlayerCharacterIndexLookup[player.Lookup] = player.CharacterSlot?.index ?? -1;
        }
    }

    private static void OnPlayerModsSynced(SNet_Player player, IEnumerable<pModInfo> mods)
    {
        if (player.IsMaster)
        {
            IsMasterHasHeartbeat = CoreAPI.IsPlayerInstalledMod(player, PluginInfo.GUID, Miniver);
        }
    }

    private static void OnMasterChanged()
    {
        IsMasterHasHeartbeat = CoreAPI.IsPlayerInstalledMod(SNet.Master, PluginInfo.GUID, Miniver);
        if (!IsMasterHasHeartbeat)
        {
            WatermarkQualityTextMesh.SetText(string.Empty);
            WatermarkQualityTextMesh.ForceMeshUpdate();
        }
        WatermarkQualityTextMesh?.gameObject.SetActive(!SNet.Master && IsMasterHasHeartbeat && s_ShowInWatermark);
        foreach (var data in NetworkQualityDataLookup.Values)
        {
            data.OnMasterChanged();
        }
    }

    private static bool HeartbeatListenerFilter(SNet_Player player)
    {
        return CoreAPI.IsPlayerInstalledMod(player, PluginInfo.GUID, Miniver);
    }

    private static void OnReceiveHeartbeat(ulong senderID, pHeartbeat data)
    {
        if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality) && !quality.Owner.IsLocal)
        {
            quality.ReceiveHeartbeat(data);
        }
    }

    private static void OnReceiveHeartbeatAck(ulong senderID, pHeartbeatAck data)
    {
        if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality) && !quality.Owner.IsLocal)
        {
            quality.ReceiveHeartbeatAck(data);
        }
    }

    private static void OnReceiveNetworkQualityReport(ulong senderID, pToMasterNetworkQualityReport data)
    {
        if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality) && !quality.Owner.IsLocal)
        {
            quality.ReceiveNetworkQualityReport(data);
        }
    }

    public static void RegisterListener(SNet_Player player)
    {
        NetworkQualityDataLookup.TryAdd(player.Lookup, new(player));
    }

    public static void UnregisterListener(SNet_Player player)
    {
        if (PlayerCharacterIndexLookup.TryGetValue(player.Lookup, out var index) && PageLoadoutQualityTextMeshes.TryGetValue(index, out var text))
        {
            text.SetText(string.Empty);
            text.ForceMeshUpdate();
        }
        if (player.IsLocal)
        {
            WatermarkQualityTextMesh.SetText(string.Empty);
            WatermarkQualityTextMesh.ForceMeshUpdate();
            NetworkQualityDataLookup.Clear();
            HeartbeatSendTimeLookup.Clear();
            HeartbeatSendIndex = 0UL;
            return;
        }

        NetworkQualityDataLookup.Remove(player.Lookup);
    }

    public static void SendHeartbeats()
    {
        HeartbeatSendIndex++;
        HeartbeatSendTimeLookup[HeartbeatSendIndex] = CurrentTime;
        heatbeat.Index = HeartbeatSendIndex;
        s_HeartbeatAction.Do(heatbeat);
    }

    public static bool IsMasterHasHeartbeat { get; private set; }
    public static Dictionary<ulong, NetworkQualityData> NetworkQualityDataLookup = new();
    public static Dictionary<ulong, int> PlayerCharacterIndexLookup = new();

    public static long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static TextMeshPro WatermarkQualityTextMesh { get; set; }
    public static Dictionary<int, TextMeshPro> PageLoadoutQualityTextMeshes { get; } = new();
    public static TextMeshPro WatermarkTextPrefab => GuiManager.WatermarkLayer.m_watermark.m_watermarkText;

    public static SNetExt_BroadcastAction<pHeartbeat> s_HeartbeatAction;
    public static SNetExt_Packet<pHeartbeatAck> s_HeartbeatAckPacket;
    public static SNetExt_BroadcastAction<pToMasterNetworkQualityReport> s_ToMasterNetworkQualityReportAction;


    private static pHeartbeat heatbeat = new();

    internal static Dictionary<ulong, long> HeartbeatSendTimeLookup = new(21600);
    internal static ulong HeartbeatSendIndex = 0UL;

    public static Color COLOR_GREEN { get; private set; } = new(0f, 0.7206f, 0f, 0.3137f);
    public static Color COLOR_RED { get; private set; } = new(0.7206f, 0f, 0f, 0.3137f);
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
        LatencyHistory.TryPeek(out var LastToLocalLatency);
        ToLocalLatency = (uint)(CurrentTime - HeartbeatSendTimeLookup[heartbeatIndex]);
        if (LatencyHistory.Count >= LatencyHistoryMaxCap)
        {
            LatencyHistory.Dequeue();
        }
        LatencyHistory.Enqueue(ToLocalLatency);

        if (NetworkJitterQueue.Count >= NetworkJitterQueueMaxCap)
        {
            NetworkJitterQueue.Dequeue();
        }

        NetworkJitterQueue.Enqueue((uint)Math.Abs(ToLocalLatency - LastToLocalLatency));
        ToLocalNetworkJitter = NetworkJitterQueue.Max();

        if (!PacketLossLookup.Contains(data.Index))
        {
            var expectedIndex = LastReceivedHeartbeatAckIndex + 1;
            if (heartbeatIndex > expectedIndex)
            {
                for (var i = expectedIndex; i < heartbeatIndex; i++)
                {
                    if (PacketReceiveQueue.Count >= PacketReceiveQueueMaxCap)
                    {
                        PacketReceiveQueue.Dequeue();
                    }
                    PacketReceiveQueue.Enqueue(false);
                }
            }
            if (PacketReceiveQueue.Count >= PacketReceiveQueueMaxCap)
            {
                PacketReceiveQueue.Dequeue();
            }
            PacketReceiveQueue.Enqueue(true);
        }

        LastReceivedHeartbeatAckIndex = heartbeatIndex;

        if (!Owner.IsLocal)
            UpdateToMasterQuality();

        LastReceivedTime = CurrentTime;
    }

    public void ReceiveNetworkQualityReport(pToMasterNetworkQualityReport data)
    {
        ToMasterLatency = data.ToMasterLatency;
        ToMasterPacketLossRate = data.ToMasterPacketLoss;
        ToMasterNetworkJitter = data.ToMasterNetworkJitter;
    }

    public void UpdateConnectionCheck()
    {
        if (Owner.IsLocal) return;

        IsAlive = CurrentTime - LastReceivedTime <= 1000;

        if (HeartbeatSendIndex - LastReceivedHeartbeatAckIndex > 2)
        {
            for (var i = LastReceivedHeartbeatAckIndex; i < HeartbeatSendIndex - 2; i++)
            {
                PacketLossLookup.Add(i);
                if (PacketReceiveQueue.Count >= PacketReceiveQueueMaxCap)
                {
                    PacketReceiveQueue.Dequeue();
                }
                PacketReceiveQueue.Enqueue(false);
            }
        }
    }

    public void SendHeartbeatAck(pHeartbeat data)
    {
        heatbeatAck.Index = data.Index;
        s_HeartbeatAckPacket.Send(heatbeatAck, Owner);
    }

    public void OnMasterChanged()
    {
        ToMasterLatency = 0;
        ToMasterPacketLossRate = 0;
        ToMasterNetworkJitter = 0;
    }

    public void UpdateToMasterQuality()
    {
        if (!IsMasterHasHeartbeat || !Owner.IsMaster) return;

        if (Owner.IsLocal)
        {
            ToMasterLatency = 0;
            ToMasterNetworkJitter = 0;
            ToMasterPacketLossRate = 0;
        }
        else if (NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var quality))
        {
            quality.ToMasterLatency = ToLocalLatency;
            quality.ToMasterNetworkJitter = ToLocalNetworkJitter;
            quality.ToMasterPacketLossRate = ToLocalPacketLossRate;
            s_ToMasterNetworkQualityReportAction.Do(quality.GetToMasterReportData());
        }
    }

    public void GetToLocalReportText(out string toLocalLatencyText, out string toLocalJitterText, out string toLocalPacketLossRateText)
    {
        toLocalLatencyText = string.Format(Settings.LatencyFormat, $"<{ToLocalLatencyColorHexString}>{$"{ToLocalLatency}ms"}</color>");
        toLocalJitterText = string.Format(Settings.NetworkJitterFormat, $"<{ToLocalNetworkJitterColorHexString}>{$"{ToLocalNetworkJitter}ms"}</color>");
        toLocalPacketLossRateText = string.Format(Settings.PacketLossFormat, $"<{ToLocalPacketLossColorHexString}>{$"{ToLocalPacketLossRate}%"}</color>");
    }

    public void GetToMasterReportText(out string toMasterLatencyText, out string toMasterJitterText, out string toMasterPacketLossRateText)
    {
        toMasterLatencyText = string.Format(Settings.LatencyFormat, $"<{ToMasterLatencyColorHexString}>{(!IsMasterHasHeartbeat ? "未知" : $"{ToMasterLatency}ms")}</color>");
        toMasterJitterText = string.Format(Settings.NetworkJitterFormat, $"<{ToMasterNetworkJitterColorHexString}>{(!IsMasterHasHeartbeat ? "未知" : $"{ToMasterNetworkJitter}ms")}</color>");
        toMasterPacketLossRateText = string.Format(Settings.PacketLossFormat, $"<{ToMasterPacketLossColorHexString}>{(!IsMasterHasHeartbeat ? "未知" : $"{ToMasterPacketLossRate}%")}</color>");
    }

    public pToMasterNetworkQualityReport GetToMasterReportData()
    {
        return new(ToMasterLatency, ToMasterNetworkJitter, ToMasterPacketLossRate);
    }

    private static string GetColorHexString(float min, float max, float value)
    {
        float t = (value - min) / (max - min);

        RGBToHSL(COLOR_GREEN, out var h1, out var s1, out var l1);

        RGBToHSL(COLOR_RED, out var h2, out var s2, out var l2);

        float h = Mathf.Lerp(h1, h2, t);
        float s = Mathf.Lerp(s1, s2, t);
        float l = Mathf.Lerp(l1, l2, t);

        Color interpolatedColor = HSLToRGB(h, s, l);

        return interpolatedColor.ToHexString();
    }

    public string ToLocalLatencyColorHexString => GetColorHexString(60, 150, ToLocalLatency);
    public string ToLocalNetworkJitterColorHexString => GetColorHexString(20, 100, ToLocalNetworkJitter);
    public string ToLocalPacketLossColorHexString => GetColorHexString(0, 10, ToLocalPacketLossRate);
    public string ToMasterLatencyColorHexString => GetColorHexString(60, 150, ToMasterLatency);
    public string ToMasterNetworkJitterColorHexString => GetColorHexString(20, 100, ToMasterNetworkJitter);
    public string ToMasterPacketLossColorHexString => GetColorHexString(0, 5, ToMasterPacketLossRate);

    public uint ToLocalPacketLossRate => (uint)(!PacketReceiveQueue.Any() ? 0U : ((float)PacketReceiveQueue.Count(p => !p) / PacketReceiveQueue.Count * 100f));
    public uint ToLocalLatency { get; private set; } = 0;
    public uint ToLocalNetworkJitter { get; private set; } = 0;
    public uint ToMasterLatency { get; private set; } = 0;
    public uint ToMasterNetworkJitter { get; private set; } = 0;
    public uint ToMasterPacketLossRate { get; private set; } = 0;

    private Queue<uint> LatencyHistory = new(LatencyHistoryMaxCap);
    private Queue<uint> NetworkJitterQueue = new(NetworkJitterQueueMaxCap);
    private Queue<bool> PacketReceiveQueue = new(PacketReceiveQueueMaxCap);
    private HashSet<ulong> PacketLossLookup = new(PacketReceiveQueueMaxCap);

    public SNet_Player Owner { get; private set; }
    public bool IsAlive { get; private set; } = true;

    private static pHeartbeatAck heatbeatAck = new();

    private ulong LastReceivedHeartbeatAckIndex = 0;
    private long LastReceivedTime = -1;
    private const int LatencyHistoryMaxCap = 50;
    private const int NetworkJitterQueueMaxCap = 20;
    private const int PacketReceiveQueueMaxCap = 100;
}