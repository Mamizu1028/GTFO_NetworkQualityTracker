using BepInEx.Unity.IL2CPP.Utils;
using CellMenu;
using Hikaria.NetworkQualityTracker.Managers;
using SNetwork;
using System.Collections;
using System.Text;
using TMPro;
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
        Instance.StartCoroutine(PlaceTextMesh());
        Instance.StartCoroutine(SendHeartbeatCoroutine());
        Instance.StartCoroutine(SendToMasterQualityCoroutine());
        Instance.StartCoroutine(TextUpdateCoroutine());
    }

    private static IEnumerator PlaceTextMesh()
    {
        var yielder = new WaitForSecondsRealtime(1f);
        while (true)
        {
            if (CM_PageLoadout.Current == null || CM_PageLoadout.Current.m_playerLobbyBars == null
                || CM_PageLoadout.Current.m_playerLobbyBars.Count == 0)
            {
                yield return yielder;
            }
            foreach (var bar in CM_PageLoadout.Current.m_playerLobbyBars)
            {
                int index = bar.PlayerSlotIndex;
                if (!NetworkQualityManager.PageLoadoutQualityTextMeshes.ContainsKey(index))
                {
                    var textMesh = GameObject.Instantiate(bar.m_nickText);
                    textMesh.m_ignoreActiveState = true;
                    textMesh.transform.SetParent(bar.m_hasPlayerRoot.transform, false);
                    textMesh.transform.localPosition.Set(0f, -0f, 0f);
                    textMesh.fontStyle = FontStyles.Normal;
                    textMesh.SetText("Testing");
                    textMesh.ForceMeshUpdate();
                    NetworkQualityManager.PageLoadoutQualityTextMeshes[index] = textMesh;
                }
            }
            if (NetworkQualityManager.PageLoadoutQualityTextMeshes.Count == 4)
            {
                yield break;
            }
            yield return yielder;
        }
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
            if (PositionNeedUpdate)
            {
                UpdatePostion();
                PositionNeedUpdate = false;
            }
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.GetToMasterReportText(out var toMasterLatencyText, out var toMasterJitterText, out var toMasterPacketLossRateText);
                if (data.Owner.IsLocal)
                {
                    if (NetworkQualityManager.WatermarkQualityTextMesh != null)
                    {
                        NetworkQualityManager.WatermarkQualityTextMesh.SetText($"{toMasterLatencyText}, {toMasterJitterText}, {toMasterPacketLossRateText}");
                        NetworkQualityManager.WatermarkQualityTextMesh.ForceMeshUpdate();
                    }
                }
                if (NetworkQualityManager.PageLoadoutQualityTextMeshes.TryGetValue(data.Owner.PlayerSlotIndex(), out var textMesh))
                {
                    StringBuilder sb = new(300);

                    if (!data.Owner.IsLocal)
                    {
                        data.GetToLocalReportText(out var toLocalLatencyText, out var toLocalJitterText, out var toLocalPacketLossRateText);
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalLatency))
                            sb.Append($"{toLocalLatencyText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalNetworkJitter))
                            sb.Append($"{toLocalJitterText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToLocalPacketLoss))
                            sb.Append($"{toLocalPacketLossRateText}\n");
                    }

                    if (!SNet.IsMaster)
                    {
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterLatency))
                            sb.Append($"{toMasterLatencyText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterNetworkJitter))
                            sb.Append($"{toMasterJitterText}\n");
                        if (ShowQualityInfo.Contains(NetworkQualityInfo.ToMasterPacketLoss))
                            sb.Append($"{toMasterPacketLossRateText}\n");
                    }

                    if (sb.Length > 0)
                    {
                        sb.Remove(sb.Length - 1, 1);
                    }

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