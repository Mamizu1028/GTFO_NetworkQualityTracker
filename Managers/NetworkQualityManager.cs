using Hikaria.Core;
using Hikaria.Core.SNetworkExt;
using Hikaria.NetworkQualityTracker.Handlers;
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

    private static readonly Version Miniver = new("1.2.0");

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
            PlayerSlotIndexLookup[player.Lookup] = player.PlayerSlot?.index ?? -1;
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
        if (PlayerSlotIndexLookup.TryGetValue(player.Lookup, out var index) && PageLoadoutQualityTextMeshes.TryGetValue(index, out var text))
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
        HeartbeatSendTimeLookup.Remove(HeartbeatSendIndex - 10);
        HeartbeatSendTimeLookup[HeartbeatSendIndex] = CurrentTime;
        heatbeat.Index = HeartbeatSendIndex;
        s_HeartbeatAction.Do(heatbeat);
        foreach (var key in NetworkQualityDataLookup.Keys)
        {
            var data = NetworkQualityDataLookup[key];
            if (!data.Owner.IsLocal)
            {
                data.CheckConnection();
                if (data.Owner.IsMaster)
                {
                    var localQuality = NetworkQualityDataLookup[SNet.LocalPlayer.Lookup];
                    localQuality.ToMasterLatency = data.ToLocalLatency;
                    localQuality.ToMasterNetworkJitter = data.ToLocalNetworkJitter;
                    localQuality.ToMasterPacketLossRate = data.ToLocalPacketLossRate;
                    localQuality.IsToMasterAlive = data.IsAlive;
                    s_ToMasterNetworkQualityReportAction.Do(localQuality.GetToMasterReportData());
                }
            }
        }
    }

    public static bool IsMasterHasHeartbeat { get; private set; }

    public static Dictionary<ulong, NetworkQualityData> NetworkQualityDataLookup = new();
    public static Dictionary<ulong, int> PlayerSlotIndexLookup = new();

    public static long CurrentTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static TextMeshPro WatermarkQualityTextMesh { get; set; }
    public static Dictionary<int, TextMeshPro> PageLoadoutQualityTextMeshes { get; } = new();
    public static TextMeshPro WatermarkTextPrefab => GuiManager.WatermarkLayer.m_watermark.m_watermarkText;

    public static SNetExt_BroadcastAction<pHeartbeat> s_HeartbeatAction;
    public static SNetExt_Packet<pHeartbeatAck> s_HeartbeatAckPacket;
    public static SNetExt_BroadcastAction<pToMasterNetworkQualityReport> s_ToMasterNetworkQualityReportAction;

    private static pHeartbeat heatbeat = new();

    internal static Dictionary<long, long> HeartbeatSendTimeLookup = new();
    internal static long HeartbeatSendIndex = 0L;

    public static Color COLOR_GREEN { get; private set; } = new(0f, 0.7206f, 0f, 0.3137f);
    public static Color COLOR_RED { get; private set; } = new(0.7206f, 0f, 0f, 0.3137f);

    public class NetworkQualityData
    {
        public NetworkQualityData(SNet_Player player) { Owner = player; }

        public void EnqueuePacket(long index, bool received)
        {
            PacketReceiveLookup.Remove(index - 100);
            if (!PacketReceiveLookup.TryAdd(index, received))
            {
                return;
            }
            if (PacketReceiveQueue.Count == PacketReceiveQueueMaxCap)
            {
                bool dequeuedPacket = PacketReceiveQueue.Dequeue();
                if (!dequeuedPacket)
                {
                    PacketLossCount--;
                }
            }
            PacketReceiveQueue.Enqueue(received);
            if (!received)
            {
                PacketLossCount++;
            }
        }

        public void ReceiveHeartbeat(pHeartbeat data)
        {
            SendHeartbeatAck(data);
        }

        public void ReceiveHeartbeatAck(pHeartbeatAck data)
        {
            if (!HeartbeatStarted)
            {
                LastReceivedTime = CurrentTime;
                PacketReceiveQueue.Clear();
                PacketReceiveLookup.Clear();
                PacketLossCount = 0;
                NetworkJitterQueue.Clear();
                HeartbeatStartIndex = data.Index;
                HeartbeatStarted = true;
            }
            var heartbeatIndex = data.Index;
            if (!HeartbeatSendTimeLookup.TryGetValue(heartbeatIndex, out var sendTime))
                return;
            var LastToLocalLatency = ToLocalLatency;
            ToLocalLatency = (int)(CurrentTime - sendTime);
            if (NetworkJitterQueue.Count == NetworkJitterQueueMaxCap)
            {
                NetworkJitterQueue.Dequeue();
            }
            NetworkJitterQueue.Enqueue(Math.Abs(ToLocalLatency - LastToLocalLatency));
            ToLocalNetworkJitter = NetworkJitterQueue.Max();

            EnqueuePacket(heartbeatIndex, true);
            LastReceivedTime = CurrentTime;
        }

        public void ReceiveNetworkQualityReport(pToMasterNetworkQualityReport data)
        {
            ToMasterLatency = data.ToMasterLatency;
            ToMasterPacketLossRate = data.ToMasterPacketLoss;
            ToMasterNetworkJitter = data.ToMasterNetworkJitter;
            IsToMasterAlive = data.IsToMasterAlive;
        }

        public void CheckConnection()
        {
            IsAlive = CurrentTime - LastReceivedTime <= NetworkQualityUpdater.HeartbeatSendInterval * 4000;
            if (Owner.IsMaster)
            {
                if (NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var data))
                {
                    data.IsToMasterAlive = IsAlive;
                }
            }

            var heartbeatIndexToCheck = HeartbeatSendIndex - 4;
            if (heartbeatIndexToCheck < HeartbeatStartIndex || !HeartbeatStarted)
                return;
            if (HeartbeatSendTimeLookup.TryGetValue(heartbeatIndexToCheck, out var time))
            {
                if (!PacketReceiveLookup.ContainsKey(heartbeatIndexToCheck) && CurrentTime - time > NetworkQualityUpdater.HeartbeatSendInterval * 4000)
                {
                    EnqueuePacket(heartbeatIndexToCheck, false);
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
            IsToMasterAlive = SNet.IsMaster;
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
        public string ToMasterPacketLossColorHexString => GetColorHexString(0, 10, ToMasterPacketLossRate);

        public int ToLocalPacketLossRate => PacketReceiveQueue.Count == 0 ? 0 : (int)(PacketLossCount * 100f / PacketReceiveQueue.Count);
        public int ToLocalLatency { get; private set; } = 0;
        public int ToLocalNetworkJitter { get; private set; } = 0;
        public int ToMasterLatency { get; internal set; } = 0;
        public int ToMasterNetworkJitter { get; internal set; } = 0;
        public int ToMasterPacketLossRate { get; internal set; } = 0;

        private Queue<int> NetworkJitterQueue = new(NetworkJitterQueueMaxCap);
        private Queue<bool> PacketReceiveQueue = new(PacketReceiveQueueMaxCap);
        private Dictionary<long, bool> PacketReceiveLookup = new(PacketReceiveQueueMaxCap);

        public SNet_Player Owner { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsToMasterAlive { get; internal set; }

        private static pHeartbeatAck heatbeatAck = new();

        private bool HeartbeatStarted;
        private long HeartbeatStartIndex = -1;
        private int PacketLossCount = 0;
        private long LastReceivedTime = -1;
        private const int NetworkJitterQueueMaxCap = 20;
        private const int PacketReceiveQueueMaxCap = 100;
    }
}