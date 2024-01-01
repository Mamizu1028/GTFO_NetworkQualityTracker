using BepInEx.Unity.IL2CPP.Utils;
using Hikaria.NetworkQualityTracker.Managers;
using SNetwork;
using System.Collections;
using System.Text;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;

namespace Hikaria.NetworkQualityTracker.Handlers;

public class NetworkQualityUpdater : MonoBehaviour
{
    public static NetworkQualityUpdater Instance { get; private set; }

    private const float TextUpdateInterval = 0.5f;

    private const float HeartbeatSendInterval = 0.5f;

    private const float ToMasterQualityReportSendInterval = 2f;

    public static List<NetworkQualityInfo> ShowQualityInfo { get; set; } = new()
    {
        NetworkQualityInfo.Latency,
        NetworkQualityInfo.NetworkJitter,
        NetworkQualityInfo.PacketLoss,
        NetworkQualityInfo.ToMasterLatency,
        NetworkQualityInfo.ToMasterNetworkJitter,
        NetworkQualityInfo.ToMasterPacketLoss
    };

    private void Awake()
    {
        Instance = this;
        this.StartCoroutine(SendHeartbeatCoroutine());
        this.StartCoroutine(SendToMasterQualityCoroutine());
        this.StartCoroutine(TextUpdateCoroutine());
    }

    private static IEnumerator SendHeartbeatCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(HeartbeatSendInterval);
        while (true)
        {
            NetworkQualityManager.SendHeartbeats();
            yield return yielder;
        }
    }

    private static IEnumerator TextUpdateCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(TextUpdateInterval);
        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                if (!SNet.IsMaster && data.Owner.IsLocal)
                {
                    data.GetReport(out var latencyText, out var jitterText, out var packetLossText);
                    if (data.Owner.IsMaster)
                    {
                        if (NetworkQualityManager.NetworkQualityText != null)
                        {
                            NetworkQualityManager.NetworkQualityText.SetText($"{latencyText}, {jitterText}, {packetLossText}");
                            NetworkQualityManager.NetworkQualityText.ForceMeshUpdate();
                        }
                    }
                }
                data.GetToMasterReport(out var toMasterLatencyText, out var toMasterJitterText, out var toMasterPacketLossText);
                if (NetworkQualityManager.PageLoadoutTextMeshes.TryGetValue(data.Owner.PlayerSlotIndex(), out var textMesh))
                {
                    StringBuilder sb = new(300);
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.Latency))
                        sb.Append($"{toMasterLatencyText}\n");
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.NetworkJitter))
                        sb.Append($"{toMasterLatencyText}\n");
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.PacketLoss))
                        sb.Append($"{toMasterLatencyText}\n");
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterLatency))
                        sb.Append($"{toMasterLatencyText}\n"); 
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterNetworkJitter))
                        sb.Append($"{toMasterJitterText}\n"); 
                    if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterPacketLoss))
                        sb.Append($"{toMasterPacketLossText}\n");
                    textMesh.SetText(sb.ToString());
                }
            }
            yield return yielder;
        }
    }

    private static IEnumerator SendToMasterQualityCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(ToMasterQualityReportSendInterval);
        while (true)
        {
            NetworkQualityManager.SendToMasterQualityReport();
            yield return yielder;
        }
    }
}