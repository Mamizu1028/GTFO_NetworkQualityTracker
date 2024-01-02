using CellMenu;
using GTFO.API;
using Hikaria.NetworkQualityTracker.Handlers;
using SNetwork;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Loader;
using TMPro;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Managers.NetworkQualityManager;

namespace Hikaria.NetworkQualityTracker.Features
{
    [DisallowInGameToggle]
    [EnableFeatureByDefault]
    public class NetworkQualityTracker : Feature
    {
        public override string Name => "Network Quality Tracker";

        public override string Group => FeatureGroups.QualityOfLife;

        #region FeatureSettings
        [FeatureConfig]
        public static NetworkLatencySetting Settings { get; set; }

        public class NetworkLatencySetting
        {
            [FSDisplayName("在水印中显示")]
            public bool ShowInWatermark
            {
                get => s_ShowInWatermark;
                set
                {
                    s_ShowInWatermark = value;
                    WatermarkQualityTextMesh?.gameObject.SetActive(value);
                }
            }

            [FSDisplayName("在大厅详情界面显示")]
            public bool ShowInPageLoadout
            {
                get => s_ShowInPageLoadout;
                set
                {
                    s_ShowInPageLoadout = value;
                    foreach (var textMesh in PageLoadoutQualityTextMeshes.Values)
                    {
                        textMesh.gameObject.SetActive(value);
                    }
                }
            }

            [FSInline]
            [FSDisplayName("显示信息")]
            public ShowInfoSetting InfoSettings { get; set; } = new();

            [FSHeader("显示格式")]
            [FSDisplayName("延迟格式")]
            public string LatencyFormat { get; set; } = "延迟: {0}";
            [FSDisplayName("延迟抖动格式")]
            public string NetworkJitterFormat { get; set; } = "抖动: {0}";
            [FSDisplayName("丢包率格式")]
            public string PacketLossFormat { get; set; } = "丢包率: {0}";

            [FSInline]
 
            [FSHeader("位置设置")]
            [FSDisplayName("位置设置")]
            public PositionSetting Position { get; set; } = new();
        }

        public class ShowInfoSetting
        {
            [FSHeader("显示信息")]
            [FSDisplayName("与本地连接提示语")]
            public string ToLocalHint { get; set; } = "与本地连接质量";
            [FSDisplayName("与主机连接提示语")]
            public string ToMasterHint { get; set; } = "与主机连接质量";
            [FSDisplayName("显示到本地延迟")]
            public bool ShowToLocalLatency { get => NetworkQualityUpdater.ShowToLocalLatency; set => NetworkQualityUpdater.ShowToLocalLatency = value; }
            [FSDisplayName("显示到本地网络抖动")]
            public bool ShowToLocalNetworkJitter { get => NetworkQualityUpdater.ShowToLocalNetworkJitter; set => NetworkQualityUpdater.ShowToLocalNetworkJitter = value; }
            [FSDisplayName("显示到本地丢包率")]
            public bool ShowToLocalPacketLoss { get => NetworkQualityUpdater.ShowToLocalPacketLoss; set => NetworkQualityUpdater.ShowToLocalPacketLoss = value; }
            [FSDisplayName("显示到主机延迟")]
            public bool ShowToMasterLatency { get => NetworkQualityUpdater.ShowToMasterLatency; set => NetworkQualityUpdater.ShowToMasterLatency = value; }
            [FSDisplayName("显示到主机网络抖动")]
            public bool ShowToMasterNetworkJitter { get => NetworkQualityUpdater.ShowToMasterNetworkJitter; set => NetworkQualityUpdater.ShowToMasterNetworkJitter = value; }
            [FSDisplayName("显示到主机丢包率")]
            public bool ShowToMasterPacketLoss { get => NetworkQualityUpdater.ShowToMasterPacketLoss; set => NetworkQualityUpdater.ShowToMasterPacketLoss = value; }
        }

        public class PositionSetting
        {
            [FSHeader("水印位置")]
            [FSDisplayName("横向偏移量")]
            [FSDescription("单位: 像素")]
            public int WatermarkOffsetX
            {
                get => s_WatermarkOffsetX;
                set
                {
                    s_WatermarkOffsetX = value;
                    if (WatermarkQualityTextMesh != null)
                        WatermarkQualityTextMesh.transform.localPosition = new(s_WatermarkOffsetX, 17.5f + s_WatermarkOffsetY);
                }
            }

            [FSDisplayName("纵向向偏移量")]
            [FSDescription("单位: 像素")]
            public int WatermarkOffsetY
            {
                get => s_WatermarkOffsetY;
                set
                {
                    s_WatermarkOffsetY = value;
                    if (WatermarkQualityTextMesh != null)
                        WatermarkQualityTextMesh.transform.localPosition = new(s_WatermarkOffsetX, 17.5f + s_WatermarkOffsetY);
                }
            }

            [FSHeader("大厅详情页位置")]
            [FSDisplayName("横向偏移量")]
            [FSDescription("单位: 像素")]
            public int PageLoadoutOffsetX
            {
                get => s_PageLoadoutOffsetX;
                set
                {
                    s_PageLoadoutOffsetX = value;
                    foreach (var textMesh in PageLoadoutQualityTextMeshes.Values)
                    {
                        textMesh.transform.localPosition = new(1050f + s_PageLoadoutOffsetX, -515f + s_PageLoadoutOffsetY);
                    }
                }
            }
            [FSDisplayName("纵向向偏移量")]
            [FSDescription("单位: 像素")]
            public int PageLoadoutOffsetY
            {
                get => s_PageLoadoutOffsetY;
                set
                {
                    s_PageLoadoutOffsetY = value;
                    foreach (var textMesh in PageLoadoutQualityTextMeshes.Values)
                    {
                        textMesh.transform.localPosition = new(1050f + s_PageLoadoutOffsetX, -515f + s_PageLoadoutOffsetY);
                    }
                }
            }
        }

        private static int s_WatermarkOffsetX = 0;
        private static int s_WatermarkOffsetY = 0;
        private static int s_PageLoadoutOffsetX = 0;
        private static int s_PageLoadoutOffsetY = 0;
        public static bool s_ShowInWatermark { get; private set; } = true;
        public static bool s_ShowInPageLoadout { get; private set; } = true;
        #endregion

        #region FeatureHooks
        [ArchivePatch(typeof(SNet_Core_STEAM), nameof(SNet_Core_STEAM.CreateLocalPlayer))]
        private class SNet_Core_STEAM__CreateLocalPlayer__Patch
        {
            private static bool IsSetup;
            private static void Postfix()
            {
                if (!IsSetup)
                {
                    NetworkQualityUpdater.StartCoroutine();
                    IsSetup = true;
                }
            }
        }

        [ArchivePatch(typeof(PUI_Watermark), nameof(PUI_Watermark.UpdateWatermark))]
        private class PUI_Watermark__UpdateWatermark__Patch
        {
            private static bool IsSetup;

            private static void Postfix()
            {
                if (!IsSetup)
                {
                    WatermarkQualityTextMesh = UnityEngine.Object.Instantiate(WatermarkTextPrefab);
                    WatermarkQualityTextMesh.transform.SetParent(WatermarkTextPrefab.transform.parent, false);
                    WatermarkQualityTextMesh.transform.localPosition = new Vector3(s_WatermarkOffsetX, 17.5f + s_WatermarkOffsetY);
                    WatermarkQualityTextMesh.SetText("");
                    WatermarkQualityTextMesh.color = new(0.7075f, 0.7075f, 0.7075f, 0.4706f);
                    WatermarkQualityTextMesh.ForceMeshUpdate();

                    foreach (var bar in CM_PageLoadout.Current.m_playerLobbyBars)
                    {
                        int index = bar.PlayerSlotIndex;
                        if (!PageLoadoutQualityTextMeshes.ContainsKey(index))
                        {
                            var textMesh = UnityEngine.Object.Instantiate(WatermarkTextPrefab, bar.m_hasPlayerRoot.transform, false);
                            textMesh.transform.localPosition = new(1050f + s_PageLoadoutOffsetX, -515f + s_PageLoadoutOffsetY, 0);
                            textMesh.fontStyle &= ~FontStyles.UpperCase;
                            textMesh.alignment = TextAlignmentOptions.TopLeft;
                            textMesh.fontSize = 25;
                            textMesh.rectTransform.sizeDelta = new(500, textMesh.rectTransform.sizeDelta.y);
                            textMesh.color = new(1f, 1f, 1f, 0.7059f);
                            textMesh.SetText("");
                            PageLoadoutQualityTextMeshes[index] = textMesh;
                        }
                    }

                    GameObject go = new GameObject("NetworkQualityUpdater");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent<NetworkQualityUpdater>();

                    IsSetup = true;
                }
            }
        }

        //[ArchivePatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.SetHasPlayer))]
        private class CM_PlayerLobbyBar__SetHasPlayer__Patch
        {
            private static void Postfix(CM_PlayerLobbyBar __instance, bool hasPlayer)
            {
                if (!hasPlayer)
                    return;
                var index = __instance.PlayerSlotIndex;
                var player = __instance.m_player;
                if (PageLoadoutQualityTextMeshes.TryGetValue(index, out var textMesh))
                {
                    if (player == null || player.IsBot || !IsMasterHasHeartbeat)
                        textMesh.transform.gameObject.SetActive(false);
                    else
                        textMesh.transform.gameObject.SetActive(!player.IsLocal);
                }
            }
        }

        [ArchivePatch(typeof(SNet_GlobalManager), nameof(SNet_GlobalManager.Setup))]
        private class SNet_GlobalManager__Setup__Patch
        {
            private static void Postfix()
            {
                SNet_Events.OnPlayerEvent += new Action<SNet_Player, SNet_PlayerEvent, SNet_PlayerEventReason>(OnPlayerEvent);
                SNet_Events.OnMasterChanged += new Action(OnMasterChanged);
            }
        }

        private static void OnPlayerEvent(SNet_Player player, SNet_PlayerEvent playerEvent, SNet_PlayerEventReason reason)
        {
            NetworkAPI.InvokeEvent<pBroadcastListenHeartbeat>(typeof(pBroadcastListenHeartbeat).FullName, new(), SNet_ChannelType.GameNonCritical);
            switch (playerEvent)
            {
                case SNet_PlayerEvent.PlayerLeftSessionHub:
                case SNet_PlayerEvent.PlayerAgentDeSpawned:
                    UnregisterListener(player);
                    break;
                case SNet_PlayerEvent.PlayerAgentSpawned:
                    if (player.IsLocal)
                        RegisterListener(player);
                    break;
            }
        }
        #endregion

        #region FeatureMethods
        public override void Init()
        {
            LoaderWrapper.ClassInjector.RegisterTypeInIl2Cpp<NetworkQualityUpdater>();
            NetworkAPI.RegisterEvent<pHeartbeat>(typeof(pHeartbeat).FullName, OnReceiveHeartbeat);
            NetworkAPI.RegisterEvent<pBroadcastListenHeartbeat>(typeof(pBroadcastListenHeartbeat).FullName, OnReceiveBroadcastListenHeartbeat);
            NetworkAPI.RegisterEvent<pHeartbeatAck>(typeof(pHeartbeatAck).FullName, OnReceiveHeartbeatAck);
            NetworkAPI.RegisterEvent<pToMasterNetworkQualityReport>(typeof(pToMasterNetworkQualityReport).FullName, OnReceiveNetworkQualityReport);
        }
        #endregion

        #region NetworkStructsHandler

        private static void OnReceiveHeartbeat(ulong senderID, pHeartbeat data)
        {
            if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
            {
                quality.ReceiveHeartbeat(data);
            }
        }

        private static void OnReceiveHeartbeatAck(ulong senderID, pHeartbeatAck data)
        {
            if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
            {
                quality.ReceiveHeartbeatAck(data);
            }
        }

        private static void OnReceiveBroadcastListenHeartbeat(ulong senderID, pBroadcastListenHeartbeat data)
        {
            if (SNet.TryGetPlayer(senderID, out var player))
            {
                RegisterListener(player);
            }
        }

        private static void OnReceiveNetworkQualityReport(ulong senderID, pToMasterNetworkQualityReport data)
        {
            if (NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
            {
                quality.ReceiveNetworkQualityReport(data);
            }
        }
        #endregion

        #region NetworkStructs
        public struct pBroadcastListenHeartbeat
        {
        }

        public struct pHeartbeat
        {
            public pHeartbeat(short index) { Index = index; }

            public short Index = 0;
        }

        public struct pToMasterNetworkQualityReport
        {
            public pToMasterNetworkQualityReport(short toMasterLatency, short toMasterNetworkJitter, short toMasterPacketLossRate)
            {
                ToMasterLatency = toMasterLatency;
                ToMasterPacketLoss = toMasterPacketLossRate;
                ToMasterNetworkJitter = toMasterNetworkJitter;
            }

            public short ToMasterLatency;
            public short ToMasterPacketLoss;
            public short ToMasterNetworkJitter;
        }

        public struct pHeartbeatAck
        {
            public pHeartbeatAck(short index)
            {
                Index = index;
            }

            public short Index = 0;
        }
        #endregion
    }
}