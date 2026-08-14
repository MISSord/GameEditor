using UnityEngine;

namespace ACTGameEditor
{
    /// <summary>
    /// 图形效果开关调试面板（类似设置里的 Bloom/AO 开关）。
    /// </summary>
    public sealed class GraphicsFxDebugPanel : MonoBehaviour
    {
        [SerializeField]
        bool showPanel = true;

        [SerializeField]
        GraphicsFxConfig config;

        Vector2 _scroll;

        static readonly GraphicsFxId[] GlobalIds =
        {
            GraphicsFxId.Bloom,
            GraphicsFxId.Vignette,
            GraphicsFxId.ColorAdjustments,
            GraphicsFxId.Tonemapping,
            GraphicsFxId.SSAO,
            GraphicsFxId.SoftShadows,
            GraphicsFxId.HitFlash,
            GraphicsFxId.Dissolve,
            GraphicsFxId.OcclusionOutline,
            GraphicsFxId.ForceOutline,
            GraphicsFxId.ScanPulse,
            GraphicsFxId.ScanEdgeHighlight,
            GraphicsFxId.RevealVision,
            GraphicsFxId.DepthVision,
            GraphicsFxId.PlayerFog,
            GraphicsFxId.ProximityDither,
            GraphicsFxId.Afterimage,
        };

        void Awake()
        {
            var service = GraphicsFxService.Instance;
            if (config != null && service.Config == null)
                service.SetConfig(config, apply: true);
        }

        void OnGUI()
        {
            if (!showPanel)
                return;

            var service = GraphicsFxService.Instance;
            const float w = 280f;
            const float h = 360f;
            Rect area = new Rect(Screen.width - w - 12f, 12f, w, h);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("图形效果开关");

            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < GlobalIds.Length; i++)
            {
                GraphicsFxId id = GlobalIds[i];
                bool cur = service.IsEnabled(id);
                bool next = GUILayout.Toggle(cur, GetDisplayName(id));
                if (next != cur)
                    service.SetEnabled(id, next);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("恢复默认"))
                service.ResetToDefaults();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string GetDisplayName(GraphicsFxId id) => id switch
        {
            GraphicsFxId.Bloom => "泛光 Bloom",
            GraphicsFxId.Vignette => "暗角 Vignette",
            GraphicsFxId.ColorAdjustments => "色彩调整",
            GraphicsFxId.Tonemapping => "色调映射",
            GraphicsFxId.SSAO => "环境光遮蔽 SSAO",
            GraphicsFxId.SoftShadows => "柔和阴影",
            GraphicsFxId.HitFlash => "受击闪白",
            GraphicsFxId.Dissolve => "溶解",
            GraphicsFxId.OcclusionOutline => "遮挡描边",
            GraphicsFxId.ForceOutline => "强制描边",
            GraphicsFxId.ScanPulse => "扫描脉冲",
            GraphicsFxId.ScanEdgeHighlight => "扫描边缘高亮",
            GraphicsFxId.RevealVision => "显现视野",
            GraphicsFxId.DepthVision => "深度视界",
            GraphicsFxId.PlayerFog => "玩家迷雾",
            GraphicsFxId.ProximityDither => "近距镂空渐隐",
            GraphicsFxId.Afterimage => "闪避残影",
            _ => id.ToString(),
        };
    }
}
