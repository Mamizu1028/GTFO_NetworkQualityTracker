using BepInEx.Unity.IL2CPP.Utils;
using Hikaria.NetworkQualityTracker.Managers;
using SNetwork;
using System.Collections;
using System.Text;
using TheArchive.Utilities;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;

namespace Hikaria.NetworkQualityTracker.Handlers;

public class NetworkQualityUpdater : MonoBehaviour
{
    public static NetworkQualityUpdater Instance { get; private set; }

    private const float TextUpdateInterval = 0.5f;
    private const float HeartbeatSendInterval = 0.5f;
    private const float ToMasterQualityReportSendInterval = 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    public static void StartCoroutine()
    {
        Instance.StartCoroutine(SendHeartbeatCoroutine());
        Instance.StartCoroutine(SendToMasterQualityCoroutine());
        Instance.StartCoroutine(TextUpdateCoroutine());
        Instance.StartCoroutine(WatchdogCoroutine());
    }

    private static IEnumerator WatchdogCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(1f);
        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.UpdateConnectionCheck();
            }
            yield return yielder;
        }
    }

    private static IEnumerator SendHeartbeatCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(HeartbeatSendInterval);
        while (true)
        {
            if (SNet.IsInLobby)
            {
                NetworkQualityManager.SendHeartbeats();
            }
            yield return yielder;
        }
    }

    private static IEnumerator TextUpdateCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(TextUpdateInterval);

        StringBuilder sb = new(300);

        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.GetToMasterReportText(out var toMasterLatencyText, out var toMasterJitterText, out var toMasterPacketLossRateText);
                if (s_ShowInWatermark && data.Owner.IsLocal)
                {
                    if (NetworkQualityManager.WatermarkQualityTextMesh != null)
                    {
                        NetworkQualityManager.WatermarkQualityTextMesh.SetText($"{toMasterLatencyText}, {toMasterJitterText}, {toMasterPacketLossRateText}");
                        NetworkQualityManager.WatermarkQualityTextMesh.ForceMeshUpdate();
                    }
                }
                if (s_ShowInPageLoadout && NetworkQualityManager.PlayerCharacterIndexLookup.TryGetValue(data.Owner.Lookup, out var index) && NetworkQualityManager.PageLoadoutQualityTextMeshes.TryGetValue(index, out var textMesh))
                {
                    if (!data.Owner.IsLocal && AnyShowToLocal)
                    {
                        data.GetToLocalReportText(out var toLocalLatencyText, out var toLocalJitterText, out var toLocalPacketLossRateText);

                        sb.AppendLine(LocalizationService.Get(7));
                        if (!data.IsAlive)
                            sb.AppendLine($"<{NetworkQualityManager.COLOR_RED.ToHexString()}>{LocalizationService.Get(5)}</color>");
                        if (ShowToLocalLatency)
                            sb.AppendLine($"{toLocalLatencyText}");
                        if (ShowToLocalNetworkJitter)
                            sb.AppendLine($"{toLocalJitterText}");
                        if (ShowToLocalPacketLoss)
                            sb.AppendLine($"{toLocalPacketLossRateText}");
                    }

                    if (NetworkQualityManager.IsMasterHasHeartbeat && !SNet.IsMaster && !data.Owner.IsMaster && AnyShowToMaster)
                    {
                        sb.AppendLine(LocalizationService.Get(8));
                        if (!data.IsToMasterAlive)
                            sb.AppendLine($"<{NetworkQualityManager.COLOR_RED.ToHexString()}>{LocalizationService.Get(6)}</color>");
                        if (ShowToMasterLatency)
                            sb.AppendLine($"{toMasterLatencyText}");
                        if (ShowToMasterNetworkJitter)
                            sb.AppendLine($"{toMasterJitterText}");
                        if (ShowToMasterPacketLoss)
                            sb.AppendLine($"{toMasterPacketLossRateText}");
                    }

                    textMesh.SetText(sb.ToString());
                    textMesh.ForceMeshUpdate();
                    sb.Clear();
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
            if (NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var quality))
            {
                quality.UpdateToMasterQuality();
            }

            yield return yielder;
        }
    }

    public static bool ShowToLocalLatency = true;
    public static bool ShowToLocalNetworkJitter = true;
    public static bool ShowToLocalPacketLoss = true;
    public static bool ShowToMasterLatency = true;
    public static bool ShowToMasterNetworkJitter = true;
    public static bool ShowToMasterPacketLoss = true;
    private static bool AnyShowToLocal => ShowToLocalLatency || ShowToLocalNetworkJitter || ShowToLocalPacketLoss;
    private static bool AnyShowToMaster => ShowToMasterLatency || ShowToMasterNetworkJitter || ShowToMasterPacketLoss;
}