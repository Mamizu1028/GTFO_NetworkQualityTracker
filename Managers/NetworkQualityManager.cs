using Hikaria.Core;
using Hikaria.Core.SNetworkExt;
using SNetwork;
using TheArchive.Utilities;
using TMPro;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;
using static Hikaria.NetworkQualityTracker.Utils.Utils;
using Version = Hikaria.Core.Version;

namespace Hikaria.NetworkQualityTracker.Managers;

public static class NetworkQualityManager
{
    public struct pHeartbeat
    {
        public long Index = 0L;

        public pHeartbeat()
        {
        }
    }

    public struct pToMasterNetworkQualityReport
    {
        public pToMasterNetworkQualityReport(int toMasterLatency, int toMasterNetworkJitter, int toMasterPacketLossRate, bool isToMasterAlive)
        {
            ToMasterLatency = toMasterLatency;
            ToMasterPacketLoss = toMasterPacketLossRate;
            ToMasterNetworkJitter = toMasterNetworkJitter;
            IsToMasterAlive = isToMasterAlive;
        }

        public int ToMasterLatency;
        public int ToMasterPacketLoss;
        public int ToMasterNetworkJitter;
        public bool IsToMasterAlive;
    }

    public struct pHeartbeatAck
    {
        public long Index = 0L;

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
            HeartbeatSendIndex = 0L;
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

    internal static Dictionary<long, long> HeartbeatSendTimeLookup = new(21600);
    internal static long HeartbeatSendIndex = 0L;

    public static Color COLOR_GREEN { get; private set; } = new(0f, 0.7206f, 0f, 0.3137f);
    public static Color COLOR_RED { get; private set; } = new(0.7206f, 0f, 0f, 0.3137f);


    public class NetworkQualityData
    {
        public NetworkQualityData(SNet_Player player) { Owner = player; }

        public void EnqueuePacketReceived(bool received)
        {
            if (PacketReceiveQueue.Count == 100)
            {
                bool dequeuedPacket = PacketReceiveQueue.Dequeue();
                if (!dequeuedPacket)
                {
                    packetLossCount--;
                }
            }
            PacketReceiveQueue.Enqueue(received);
            if (!received)
            {
                packetLossCount++;
            }
        }
        private int packetLossCount = 0;

        public void ReceiveHeartbeat(pHeartbeat data)
        {
            SendHeartbeatAck(data);
        }

        public void ReceiveHeartbeatAck(pHeartbeatAck data)
        {
            if (LastReceivedHeartbeatAckIndex == -1)
            {
                LastReceivedHeartbeatAckIndex = data.Index;
            }
            var heartbeatIndex = data.Index;
            LatencyHistory.TryPeek(out var LastToLocalLatency);
            ToLocalLatency = (int)(CurrentTime - HeartbeatSendTimeLookup[heartbeatIndex]);
            if (LatencyHistory.Count >= LatencyHistoryMaxCap)
            {
                LatencyHistory.Dequeue();
            }
            LatencyHistory.Enqueue(ToLocalLatency);

            if (NetworkJitterQueue.Count >= NetworkJitterQueueMaxCap)
            {
                NetworkJitterQueue.Dequeue();
            }

            NetworkJitterQueue.Enqueue(Math.Abs(ToLocalLatency - LastToLocalLatency));
            ToLocalNetworkJitter = NetworkJitterQueue.Max();

            if (!PacketLossLookup.Contains(data.Index))
            {
                var expectedIndex = LastReceivedHeartbeatAckIndex + 1;
                if (heartbeatIndex > expectedIndex)
                {
                    for (var i = expectedIndex; i < heartbeatIndex; i++)
                    {
                        EnqueuePacketReceived(false);
                    }
                }
                EnqueuePacketReceived(true);
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
            IsToMasterAlive = data.IsToMasterAlive;
        }

        public void UpdateConnectionCheck()
        {
            if (Owner.IsLocal) return;

            IsAlive = CurrentTime - LastReceivedTime <= 1000;
            if (Owner.IsMaster)
            {
                if (NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var data))
                {
                    data.IsToMasterAlive = IsAlive;
                }
            }

            if (HeartbeatSendIndex - LastReceivedHeartbeatAckIndex > 2)
            {
                for (var i = LastReceivedHeartbeatAckIndex; i < HeartbeatSendIndex - 2; i++)
                {
                    PacketLossLookup.Add(i);
                    EnqueuePacketReceived(false);
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
            IsToMasterAlive = false;
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
            toLocalLatencyText = LocalizationService.Format(2, $"<{ToLocalLatencyColorHexString}>{$"{ToLocalLatency}ms"}</color>");
            toLocalJitterText = LocalizationService.Format(3, $"<{ToLocalNetworkJitterColorHexString}>{$"{ToLocalNetworkJitter}ms"}</color>");
            toLocalPacketLossRateText = LocalizationService.Format(4, $"<{ToLocalPacketLossColorHexString}>{$"{ToLocalPacketLossRate}%"}</color>");
        }

        public void GetToMasterReportText(out string toMasterLatencyText, out string toMasterJitterText, out string toMasterPacketLossRateText)
        {
            toMasterLatencyText = LocalizationService.Format(2, $"<{ToMasterLatencyColorHexString}>{(!IsMasterHasHeartbeat ? LocalizationService.Get(1) : $"{ToMasterLatency}ms")}</color>");
            toMasterJitterText = LocalizationService.Format(3, $"<{ToMasterNetworkJitterColorHexString}>{(!IsMasterHasHeartbeat ? LocalizationService.Get(1) : $"{ToMasterNetworkJitter}ms")}</color>");
            toMasterPacketLossRateText = LocalizationService.Format(4, $"<{ToMasterPacketLossColorHexString}>{(!IsMasterHasHeartbeat ? LocalizationService.Get(1) : $"{ToMasterPacketLossRate}%")}</color>");
        }

        public pToMasterNetworkQualityReport GetToMasterReportData()
        {
            return new(ToMasterLatency, ToMasterNetworkJitter, ToMasterPacketLossRate, IsToMasterAlive);
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

        public int ToLocalPacketLossRate => (int)(PacketReceiveQueue.Count == 0 ? 0U : (packetLossCount * 100f / PacketReceiveQueue.Count));
        public int ToLocalLatency { get; private set; } = 0;
        public int ToLocalNetworkJitter { get; private set; } = 0;
        public int ToMasterLatency { get; private set; } = 0;
        public int ToMasterNetworkJitter { get; private set; } = 0;
        public int ToMasterPacketLossRate { get; private set; } = 0;

        private Queue<int> LatencyHistory = new(LatencyHistoryMaxCap);
        private Queue<int> NetworkJitterQueue = new(NetworkJitterQueueMaxCap);
        private Queue<bool> PacketReceiveQueue = new(PacketReceiveQueueMaxCap);
        private HashSet<long> PacketLossLookup = new(PacketReceiveQueueMaxCap);

        public SNet_Player Owner { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsToMasterAlive { get; private set; }

        private static pHeartbeatAck heatbeatAck = new();

        private long LastReceivedHeartbeatAckIndex = -1;
        private long LastReceivedTime = CurrentTime;
        private const int LatencyHistoryMaxCap = 50;
        private const int NetworkJitterQueueMaxCap = 20;
        private const int PacketReceiveQueueMaxCap = 100;
    }
}