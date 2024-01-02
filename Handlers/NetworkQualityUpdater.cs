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

    private const float ToMasterQualityReportSendInterval = 0.5f;

    public static List<NetworkQualityInfo> ShowQualityInfo { get; set; } = new()
    {
        NetworkQualityInfo.ToLocalLatency,
        NetworkQualityInfo.ToLocalNetworkJitter,
        NetworkQualityInfo.ToLocalPacketLoss,
        NetworkQualityInfo.ToMasterLatency,
        NetworkQualityInfo.ToMasterNetworkJitter,
        NetworkQualityInfo.ToMasterPacketLoss
    };

    private void Awake()
    {
        Instance = this;
    }

    public static void StartCoroutine()
    {
        Instance.StartCoroutine(SendHeartbeatCoroutine());
        Instance.StartCoroutine(SendToMasterQualityCoroutine());
        Instance.StartCoroutine(TextUpdateCoroutine());
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

    private static StringBuilder sb = new(300);

    private static IEnumerator TextUpdateCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(TextUpdateInterval);
        var fixedUpdateYielder = new WaitForFixedUpdate();

        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.GetToMasterReportText(out var toMasterLatencyText, out var toMasterJitterText, out var toMasterPacketLossRateText);
                if (data.Owner.IsLocal)
                {
                    if (NetworkQualityManager.WatermarkQualityTextMesh != null && s_ShowInWatermark)
                    {
                        NetworkQualityManager.WatermarkQualityTextMesh.SetText($"{toMasterLatencyText}, {toMasterJitterText}, {toMasterPacketLossRateText}");
                        NetworkQualityManager.WatermarkQualityTextMesh.ForceMeshUpdate();
                        yield return fixedUpdateYielder;
                    }
                }
                if (NetworkQualityManager.PageLoadoutQualityTextMeshes.TryGetValue(data.Owner.PlayerSlotIndex(), out var textMesh) && s_ShowInPageLoadout)
                {
                    data.GetToLocalReportText(out var toLocalLatencyText, out var toLocalJitterText, out var toLocalPacketLossRateText);

                    if (!data.Owner.IsLocal)
                    {
                        if (AnyToLocal)
                        {
                            sb.Append("与本地连接质量:\n");
                        }

                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalLatency))
                            sb.Append($"{toLocalLatencyText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalNetworkJitter))
                            sb.Append($"{toLocalJitterText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalPacketLoss))
                            sb.Append($"{toLocalPacketLossRateText}\n");
                    }

                    if (!SNet.IsMaster)
                    {
                        if (AnyToMaster)
                        {
                            sb.Append("与主机连接质量:\n");
                        }

                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterLatency))
                            sb.Append($"{toMasterLatencyText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterNetworkJitter))
                            sb.Append($"{toMasterJitterText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterPacketLoss))
                            sb.Append($"{toMasterPacketLossRateText}\n");
                    }

                    textMesh.SetText(sb.ToString());
                    textMesh.ForceMeshUpdate();
                    sb.Clear();
                }
            }
            yield return yielder;
        }
    }

    private static bool AnyToMaster => ShowQualityInfo.Any(p => p == NetworkQualityInfo.ToMasterLatency || p == NetworkQualityInfo.ToMasterPacketLoss || p == NetworkQualityInfo.ToMasterNetworkJitter);
    private static bool AnyToLocal => ShowQualityInfo.Any(p => p == NetworkQualityInfo.ToLocalLatency || p == NetworkQualityInfo.ToLocalPacketLoss || p == NetworkQualityInfo.ToLocalNetworkJitter);

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