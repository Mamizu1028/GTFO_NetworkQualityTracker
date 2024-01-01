using BepInEx.Unity.IL2CPP.Utils;
using System.Collections;
using UnityEngine;
using Hikaria.NetworkQualityTracker.Features;
using Hikaria.NetworkQualityTracker.Managers;

namespace Hikaria.NetworkQualityTracker.Handlers;

public class NetworkQualityUpdater : MonoBehaviour
{
    public static NetworkQualityUpdater Instance { get; private set; }

    private const float TextUpdateInterval = 0.5f;

    private const float HeartbeatSendInterval = 0.5f;

    private const float ToMasterQualityReportSendInterval = 2f;

    private void Awake()
    {
        Instance = this;
        this.StartCoroutine(HeartbeatCoroutine());
        this.StartCoroutine(ToMasterQualityCoroutine());
        this.StartCoroutine(TextUpdateCoroutine());
    }

    private static IEnumerator HeartbeatCoroutine()
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
                data.GetReport(out var latencyText, out var jitterText, out var packetLossText);
                if (data.Owner.IsMaster)
                {
                    if (NetworkQualityManager.NetworkQualityText != null)
                    {
                        NetworkQualityManager.NetworkQualityText.SetText($"{latencyText}, {jitterText}, {packetLossText}");
                        NetworkQualityManager.NetworkQualityText.ForceMeshUpdate();
                    }
                }
                if (NetworkQualityManager.PageLoadoutTextMeshes.TryGetValue(data.Owner.PlayerSlotIndex(), out var textMesh))
                {
                    textMesh.SetText($"{latencyText}\n{packetLossText}");
                }
            }
            yield return yielder;
        }
    }

    private static IEnumerator ToMasterQualityCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(ToMasterQualityReportSendInterval);
        while (true)
        {
            NetworkQualityManager.SendToMasterQualityReport();
            yield return yielder;
        }
    }
}