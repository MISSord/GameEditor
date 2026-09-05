using UnityEngine;
using EGamePlay;
using ACTGameEditor.Combat;

namespace ACTGameEditor
{
    /// <summary>
    /// Shader 测试驱动：闪白/冰冻/溶解/扫描/球显/深度/锥显/迷雾。
    /// </summary>
    public sealed class CharacterShaderTester : MonoBehaviour
    {
        [SerializeField]
        CharacterRenderFX renderFX;

        [SerializeField]
        ScanPulseController scanPulse;

        [SerializeField]
        RevealVisionController revealVision;

        [SerializeField]
        RevealConeController revealCone;

        [SerializeField]
        DepthVisionController depthVision;

        [SerializeField]
        PlayerFogController playerFog;

        [SerializeField]
        Material characterMaterial;

        [Range(0f, 1f)]
        public float HitFlash;

        [Range(0f, 1f)]
        public float Dissolve;

        [Range(0f, 1f)]
        public float Freeze;

        [Min(0.01f)]
        public float FlashDuration = 0.12f;

        [Min(0.01f)]
        public float DissolveDuration = 1.2f;

        [SerializeField]
        bool showOnGuiHints = true;

        float _lastHitFlash = -1f;
        float _lastDissolve = -1f;
        float _lastFreeze = -1f;
        Vector2 _hintScroll;

        void Awake()
        {
            if (renderFX == null)
                renderFX = GetComponent<CharacterRenderFX>() ?? gameObject.AddComponent<CharacterRenderFX>();

            if (scanPulse == null)
                scanPulse = GetComponent<ScanPulseController>() ?? gameObject.AddComponent<ScanPulseController>();

            if (revealVision == null)
                revealVision = GetComponent<RevealVisionController>() ?? gameObject.AddComponent<RevealVisionController>();

            if (revealCone == null)
                revealCone = GetComponent<RevealConeController>() ?? gameObject.AddComponent<RevealConeController>();

            if (depthVision == null)
                depthVision = GetComponent<DepthVisionController>() ?? gameObject.AddComponent<DepthVisionController>();

            if (playerFog == null)
                playerFog = GetComponent<PlayerFogController>() ?? gameObject.AddComponent<PlayerFogController>();

            if (GetComponent<ProximityDitherFade>() == null)
                gameObject.AddComponent<ProximityDitherFade>();

            if (GetComponent<AfterimageController>() == null)
                gameObject.AddComponent<AfterimageController>();

            if (GetComponent<CharacterIceShell>() == null)
                gameObject.AddComponent<CharacterIceShell>();

            var objectFx = GetComponent<ObjectFxController>();
            if (objectFx == null)
                objectFx = gameObject.AddComponent<ObjectFxController>();
            objectFx.ApplyPreset(ObjectFxController.FxPreset.Character);
        }

        void Update()
        {
            if (renderFX == null)
                return;

            if (!Mathf.Approximately(HitFlash, _lastHitFlash))
            {
                renderFX.SetHitFlash(HitFlash);
                _lastHitFlash = HitFlash;
            }

            if (!Mathf.Approximately(Dissolve, _lastDissolve))
            {
                renderFX.SetDissolve(Dissolve);
                _lastDissolve = Dissolve;
            }

            if (!Mathf.Approximately(Freeze, _lastFreeze))
            {
                renderFX.SetFreeze(Freeze);
                _lastFreeze = Freeze;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                renderFX.Flash(FlashDuration);
                HitFlash = 1f;
                _lastHitFlash = HitFlash;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Dissolve = 0f;
                _lastDissolve = 0f;
                renderFX.PlayDissolve(DissolveDuration, () =>
                {
                    Dissolve = 1f;
                    _lastDissolve = 1f;
                });
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                renderFX.ResetFX();
                scanPulse?.CancelScan();
                revealVision?.CancelReveal();
                revealCone?.SetActive(false);
                depthVision?.SetActive(false);
                playerFog?.SetActive(false);
                HitFlash = 0f;
                Dissolve = 0f;
                Freeze = 0f;
                _lastHitFlash = 0f;
                _lastDissolve = 0f;
                _lastFreeze = 0f;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                Freeze = Freeze > 0.5f ? 0f : 1f;
                renderFX.SetFreeze(Freeze);
                _lastFreeze = Freeze;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
                scanPulse?.TriggerScan();

            if (Input.GetKeyDown(KeyCode.Alpha9))
                renderFX?.PlayAfterimage();

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                CombatPresentationDirector.Play(CombatFxSpec.HitStop(
                    CombatFxSource.Manual(0),
                    0.08f,
                    0.08f,
                    cameraImpact: true));
            }
        }

        void OnGUI()
        {
            if (!showOnGuiHints)
                return;

            const float pad = 12f;
            const float panelW = 560f;
            float panelH = Mathf.Min(Screen.height - pad * 2f, 520f);
            GUILayout.BeginArea(new Rect(pad, pad, panelW, panelH), GUI.skin.box);

            GUILayout.Label("角色 Shader 测试面板");
            _hintScroll = GUILayout.BeginScrollView(_hintScroll, false, true, GUILayout.ExpandHeight(true));

            GUILayout.Label("F = 冰冻 (Freeze + Ice 外壳)");
            GUILayout.Label("1 = 受击闪白 (Hit Flash)");
            GUILayout.Label("2 = 溶解 (Dissolve)");
            GUILayout.Label("3 = 重置 / 取消显现·圆锥·迷雾");
            GUILayout.Label("4 = 扫描脉冲 (边缘高亮)");
            GUILayout.Label("9 = 闪避残影 (Afterimage x3)");
            GUILayout.Label("0 = HitStop + CA/RadialBlur 冲击镜头");
            GUILayout.Label("5 = 球形显现 (+浅蓝屏罩)");
            GUILayout.Label("6 = 深度视界 (近白远灰)");
            GUILayout.Label("7 = 圆锥显现（角色朝向；物体需 RevealMasked）");
            GUILayout.Label("8 = 玩家迷雾（雾区原地，可视半径跟随）");
            GUILayout.Label("近距镂空: 相机贴脸时物体抖镂空透出后方（非透明）");
            GUILayout.Label("星空: StarfieldSky 穹顶（ACT/Starfield，随视角变化、时间流动）");
            GUILayout.Label("效果预设: ACTGame/Fx Preset（角色 / 场景物仅镂空 / 仅描边）");
            GUILayout.Label($"球形显现: {(revealVision != null && revealVision.IsActive ? "开启" : "关闭")}");
            GUILayout.Label($"圆锥显现: {(revealCone != null && revealCone.IsActive ? "开启" : "关闭")}");
            GUILayout.Label($"深度视界: {(DepthVisionState.IsActive ? "开启" : "关闭")}");
            GUILayout.Label($"玩家迷雾: {(playerFog != null && playerFog.IsActive ? "开启" : "关闭")}");

            if (playerFog != null && playerFog.IsActive)
            {
                GUILayout.Space(6f);
                GUILayout.Label("—— 迷雾参数（也可在角色 Inspector → Player Fog Controller）——");
                GUILayout.Label($"清晰半径 ClearRadius: {playerFog.ClearRadius:0.0}");
                playerFog.ClearRadius = GUILayout.HorizontalSlider(playerFog.ClearRadius, 1f, 30f);
                GUILayout.Label($"雾过渡 FogFade: {playerFog.FogFade:0.0}");
                playerFog.FogFade = GUILayout.HorizontalSlider(playerFog.FogFade, 0.5f, 20f);
                GUILayout.Label($"强度 Intensity: {playerFog.Intensity:0.00}");
                playerFog.Intensity = GUILayout.HorizontalSlider(playerFog.Intensity, 0f, 1f);

                GUILayout.Label($"雾盒边长（长宽高）: {playerFog.BoxSize:0.0}");
                playerFog.BoxSize = GUILayout.HorizontalSlider(playerFog.BoxSize, 4f, 80f);

                playerFog.FogSky = GUILayout.Toggle(playerFog.FogSky, "天空也罩雾 FogSky");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
