using CellMenu;
using GTFO.API;
using Hikaria.NetworkQualityTracker.Handlers;
using Hikaria.NetworkQualityTracker.Managers;
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
    [EnableFeatureByDefault]
    public class NetworkQualityTracker : Feature
    {
        public override string Name => "Network Quality Tracker";

        public override string Group => FeatureGroups.GetOrCreate("网络质量跟踪器");

        #region FeatureSettings
        [FeatureConfig]
        public static NetworkLatencySetting Settings { get; set; }

        public class NetworkLatencySetting
        {
            [FSDisplayName("在水印中显示")]
            public bool ShowInWatermark { get; set; } = true;
            [FSDisplayName("在大厅详情界面显示")]
            public bool ShowInPageLoadout { get; set; } = true;

            [FSDisplayName("显示内容")]
            public List<NetworkQualityInfo> ShowQualityInfo { get => NetworkQualityUpdater.ShowQualityInfo; set => NetworkQualityUpdater.ShowQualityInfo = value; }

            [FSHeader("显示格式")]
            [FSDisplayName("延迟格式")]
            public string LatencyFormat { get; set; } = "延迟: {0}";
            [FSDisplayName("延迟抖动格式")]
            public string NetworkJitterFormat { get; set; } = "抖动: {0}";
            [FSDisplayName("丢包率格式")]
            public string PacketLossFormat { get; set; } = "丢包率: {0}";

            [FSDisplayName("位置设置")]
            public PositionSetting Position { get; set; } = new();
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
                    PositionNeedUpdate = true;
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
                    PositionNeedUpdate = true;
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
                    PositionNeedUpdate = true;
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
                    PositionNeedUpdate = true;
                }
            }
        }

        public static int s_WatermarkOffsetX { get; set; } = 0;
        public static int s_WatermarkOffsetY { get; set; } = 0;
        public static int s_PageLoadoutOffsetX { get; set; } = 0;
        public static int s_PageLoadoutOffsetY { get; set; } = 0;
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
                    WatermarkQualityTextMesh = GameObject.Instantiate(WatermarkTextPrefab);
                    WatermarkQualityTextMesh.transform.SetParent(WatermarkTextPrefab.transform.parent, false);
                    WatermarkQualityTextMesh.transform.localPosition = new Vector3(0f, 17.5f);
                    WatermarkQualityTextMesh.SetText("");
                    WatermarkQualityTextMesh.color = new(0.7075f, 0.7075f, 0.7075f, 0.4706f);
                    WatermarkQualityTextMesh.ForceMeshUpdate();
                    IsSetup = true;
                }
            }
        }

        [ArchivePatch(typeof(CM_PageRundown_New), nameof(CM_PageRundown_New.Setup))]
        private class CM_PageRundown_New__Setup__Patch
        {
            private static bool IsSetup;
            private static void Postfix()
            {
                if (!IsSetup)
                {
                    GameObject go = new GameObject("NetworkQualityUpdater");
                    GameObject.DontDestroyOnLoad(go);
                    go.AddComponent<NetworkQualityUpdater>();
                    IsSetup = true;
                }
            }
        }


        [ArchivePatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.SetHasPlayer))]
        private class CM_PlayerLobbyBar__SetHasPlayer__Patch
        {
            private static void Postfix(CM_PlayerLobbyBar __instance)
            {
                /*
                var player = __instance.m_player;
                if (player == null || player.IsBot || !NetworkQualityData.IsMasterHasHeartbeat)
                    pair.transform.gameObject.SetActive(false);
                else
                    pair.transform.gameObject.SetActive(!player.IsLocal);
                */
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

        public static void UpdatePostion()
        {
            WatermarkQualityTextMesh.transform.localPosition = new Vector3(s_WatermarkOffsetX, 17.5f + s_WatermarkOffsetY);

            foreach (var textMesh in PageLoadoutQualityTextMeshes.Values)
            {
                textMesh.transform.localPosition = new(s_PageLoadoutOffsetX, s_PageLoadoutOffsetY);
            }
        }

        public static bool PositionNeedUpdate;

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
            if (NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
            {
                quality.ReceiveHeartbeat(data);
            }
        }

        private static void OnReceiveHeartbeatAck(ulong senderID, pHeartbeatAck data)
        {
            if (NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
            {
                quality.ReceiveHeartbeatAck(data);
            }
        }

        private static void OnReceiveBroadcastListenHeartbeat(ulong senderID, pBroadcastListenHeartbeat data)
        {
            if (SNet.TryGetPlayer(senderID, out var player))
            {
                NetworkQualityManager.RegisterListener(player);
            }
        }

        private static void OnReceiveNetworkQualityReport(ulong senderID, pToMasterNetworkQualityReport data)
        {
            if (NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(senderID, out var quality))
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

        public enum NetworkQualityInfo
        {
            ToLocalLatency,
            ToLocalPacketLoss,
            ToLocalNetworkJitter,
            ToMasterLatency,
            ToMasterPacketLoss,
            ToMasterNetworkJitter
        }
    }
}