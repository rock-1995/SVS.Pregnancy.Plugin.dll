using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SVSPregnancy
{
    public class PregnancyDebugUI : MonoBehaviour
    {
        public PregnancyDebugUI(IntPtr ptr) : base(ptr) { }

        private bool  _show       = false;
        private Rect  _windowRect = new Rect(20, 80, 560, 610);
        private Vector2 _charScroll = Vector2.zero;
        private int   _selectedCharaId = -1;

        // ── Belly-settings panel ──────────────────────────────────────────
        private bool     _showBellySettings = false;
        private Vector2  _vtxScroll         = Vector2.zero;
        private string   _bellyMsg          = "";

        private float _vtxContentHeight = 2400f;
        private string _startDayBuf = "40";

        // Lazily created 1×1 white texture for the progress bar fill
        private static Texture2D _whiteTex;
        private static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                    UnityEngine.Object.DontDestroyOnLoad(_whiteTex);
                }
                return _whiteTex;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(PregnancyPlugin.DebugUIKey.Value))
                _show = !_show;
        }

        void OnGUI()
        {
            if (!_show) return;
            _windowRect = GUILayout.Window(
                98765, _windowRect,
                (GUI.WindowFunction)((Action<int>)DrawWindow),
                "SVSPregnancy Debug");
        }

        private void DrawWindow(int id)
        {
            var worldCtrl = PregnancyPlugin._worldController;
            if (worldCtrl == null || !worldCtrl._inited)
            {
                DrawForceApplyPanel();
                GUI.DragWindow();
                return;
            }

            var females = worldCtrl._PregnancyCharaControllers
                .Select(p => p.ToObject<PregnancyCharaController>())
                .Where(c => c != null && c._pregnancyInfo != null && c._pregnancyInfo._sex == 1)
                .ToList();

            GUILayout.BeginHorizontal();

            // ── Left panel: character list ────────────────────────────────
            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label("Characters:");
            _charScroll = GUILayout.BeginScrollView(_charScroll, GUILayout.Height(200));
            foreach (var ctrl in females)
            {
                string label    = GetName(ctrl);
                bool   selected = ctrl._charaId == _selectedCharaId;
                var    style    = selected ? GUI.skin.box : GUI.skin.button;
                if (GUILayout.Button(label, style))
                {
                    if (_selectedCharaId != ctrl._charaId && BellyVertexMorph.ForceApplyEnabled)
                    {
                        BellyVertexMorph.ForceApplyEnabled = false;
                        BellyVertexMorph.ForgetAll();
                        ApplyBellyAll();
                    }
                    _selectedCharaId = ctrl._charaId;
                }
            }
            if (females.Count == 0)
                GUILayout.Label("(none)");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // ── Right panel: selected character info ──────────────────────
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            var sel = females.FirstOrDefault(c => c._charaId == _selectedCharaId);
            if (sel == null && females.Count > 0)
            {
                sel = females[0];
                _selectedCharaId = sel._charaId;
            }

            if (sel != null)
                DrawCharaInfo(sel);
            else
                GUILayout.Label("No female characters in world controller.");

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // ── Apply / Reset buttons ─────────────────────────────────────
            GUILayout.Space(4);
            if (BellyVertexMorph.Paused)
                GUILayout.Label("⚠ Deform paused — click Apply to resume");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Belly Deform (All)"))
                ApplyBellyAll();
            if (GUILayout.Button("Reset Deform (All)"))
                ResetBellyAll();
            if (GUILayout.Button("Dump Mesh Info (Log)"))
                DumpMeshInfoAll();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Dump Normal Detail (Log)"))
                DumpNormalDetailAll();
            GUILayout.EndHorizontal();

            // ── Belly deform settings (collapsible) ───────────────────────
            GUILayout.Space(6);
            var toggleLabel = _showBellySettings ? "▼ Belly Deform Settings" : "▶ Belly Deform Settings";
            if (GUILayout.Button(toggleLabel))
            {
                _showBellySettings = !_showBellySettings;
            }

            if (_showBellySettings)
                DrawVtxSettings();

            GUI.DragWindow();
        }

        // ─────────────────────────────────────────────────────────────────
        // Force-apply panel  (shown when world controller is absent, e.g.
        // in character creation / studio where pregnancy data doesn't exist)
        // ─────────────────────────────────────────────────────────────────
        private void DrawForceApplyPanel()
        {
            GUILayout.Label("Manual shape preview does not change pregnancy or saved state.");
            DrawVtxSettings();
        }

        // ─────────────────────────────────────────────────────────────────
        // Character panel
        // ─────────────────────────────────────────────────────────────────
        private void DrawCharaInfo(PregnancyCharaController ctrl)
        {
            var info = ctrl._pregnancyInfo;

            GUILayout.Label($"[{GetName(ctrl)}]  charaId = {ctrl._charaId}");
            GUILayout.Space(4);

            if (info.IsPregnant)
            {
                float progress = Mathf.Clamp01((float)info._day / info._currentMaximalPregnantDays);
                int   stage    = ctrl.CheckBabySize();
                string stageStr = stage switch
                {
                    0 => "0 (None)", 1 => "1 (S)", 2 => "2 (M)",
                    3 => "3 (L)",    4 => "4 (XL)", _ => stage.ToString()
                };

                GUILayout.Label(
                    $"Pregnant  Day {info._day} / {info._currentMaximalPregnantDays}  " +
                    $"({(int)(progress * 100)}%)   Stage {stageStr}");

                Rect barRect = GUILayoutUtility.GetRect(
                    GUIContent.none, GUIStyle.none,
                    GUILayout.Height(16), GUILayout.ExpandWidth(true));
                GUI.Box(barRect, GUIContent.none);
                if (progress > 0f)
                {
                    var fill = new Rect(barRect.x + 1, barRect.y + 1,
                                        (barRect.width - 2) * progress, barRect.height - 2);
                    var prev = GUI.color;
                    GUI.color = Color.Lerp(Color.green, Color.red, progress);
                    GUI.DrawTexture(fill, WhiteTex);
                    GUI.color = prev;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Day:", GUILayout.Width(34));
                int newDay = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                    info._day, 0f, info._currentMaximalPregnantDays));
                GUILayout.Label(newDay.ToString(), GUILayout.Width(40));
                GUILayout.EndHorizontal();
                if (newDay != info._day)
                    info._day = newDay;

                if (info.IsCoolingdown)
                {
                    GUILayout.Space(4);
                    GUILayout.Label($"Post-birth cooldown: {info._cooldown} days remaining");
                }
            }
            else if (info.IsCoolingdown)
            {
                GUILayout.Label($"Post-birth cooldown: {info._cooldown} days remaining");
                GUILayout.Space(4);
                if (GUILayout.Button("Reset Cooldown"))
                    info._cooldown = 0;
            }
            else
            {
                GUILayout.Label("Not pregnant.");
                GUILayout.Space(4);
                if (GUILayout.Button("Force Conceive (Debug)"))
                    ctrl.Conceive("Debug", "Debug", true);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Vertex morph settings panel — text fields + sliders
        // ─────────────────────────────────────────────────────────────────

        [HideFromIl2Cpp]
        private void DrawVtxSettings()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(BellyVertexMorph.ForceApplyEnabled ? "Preview: ON (click to stop)" : "Enable Preview"))
            {
                BellyVertexMorph.ForceApplyEnabled = !BellyVertexMorph.ForceApplyEnabled;
                BellyVertexMorph.ForceApplyCharaId = _selectedCharaId;
                if (!BellyVertexMorph.ForceApplyEnabled) BellyVertexMorph.ForgetAll();
                ApplyBellyAll();
            }
            if (GUILayout.Button("Reset / restore meshes"))
            {
                BellyVertexMorph.ForceApplyEnabled = false;
                ResetBellyAll();
            }
            GUILayout.EndHorizontal();

            var area = GUILayoutUtility.GetRect(0, 360, GUILayout.ExpandWidth(true));
            _vtxScroll = GUI.BeginScrollView(area, _vtxScroll, new Rect(0, 0, area.width - 20, _vtxContentHeight));
            float width = area.width - 26;
            float y = 0;
            BellyVertexMorph.ForceApplyRate = Slider("Growth stage", BellyVertexMorph.ForceApplyRate, 0, 1, ref y, width);
        VtxSettings p = BellyDeformSettings.Vtx;
        p.GrowthFullness = Slider("Forward fullness", p.GrowthFullness, 0.5f, 1.6f, ref y, width);
        p.GrowthWidth = Slider("Belly width", p.GrowthWidth, 0.5f, 2f, ref y, width);
        p.UpperReach = Slider("Upper abdomen reach", p.UpperReach, 0.75f, 1.2f, ref y, width);
        p.VerticalRange = Slider("Vertical influence range", p.VerticalRange, .6f, 1.2f, ref y, width);
        p.WallSmoothing = Slider("Whole-abdomen smoothing", p.WallSmoothing, 0, 2, ref y, width);
        p.SagStrength = Slider("Belly sag (0 = off)", p.SagStrength, 0, 2, ref y, width);
        p.MidVolume = Slider("Second-stage volume", p.MidVolume, .75f, 1.5f, ref y, width);
        p.LowerPoleLift = Slider("Late lower-pole lift", p.LowerPoleLift, 0, 2, ref y, width);
        p.SkinClearance = Slider("Skin clearance", p.SkinClearance, 0, .2f, ref y, width);
        p.LateSettle = Slider("Late settling", p.LateSettle, 0, 1, ref y, width);
        p.ClothOffset = Slider("Clothing displacement", p.ClothOffset, 0.8f, 1.3f, ref y, width);
        p.ClothTopMult = p.ClothBotMult = p.ClothBraMult = p.ClothShortsMult = p.ClothPanstMult = p.ClothOtherMult = p.ClothOffset;
        GUI.Label(new Rect(0,y,width,24),"Virtual axis / pose response");y+=28;
        p.VirtualAxisStrength=Slider("Virtual axis strength (0 = native)",p.VirtualAxisStrength,0,1,ref y,width);
        p.AxisBlendStart=Slider("Blend start / torso span",p.AxisBlendStart,0,.15f,ref y,width);
        p.AxisBlendFull=Slider("Full blend / torso span",p.AxisBlendFull,.05f,.6f,ref y,width);
        GUI.Label(new Rect(0,y,width,24),"Lower attachment / original body height");y+=28;
        p.LowerTransitionStart=Slider("Lower start above pelvic floor / span",p.LowerTransitionStart,-.50f,.75f,ref y,width);
        p.LowerTransitionWidth=Slider("Lower transition width / span",p.LowerTransitionWidth,.02f,1.50f,ref y,width);
        p.LowerTransitionBias=Slider("Lower curve bias (- earlier / + later)",p.LowerTransitionBias,-2,2,ref y,width);
        GUI.Label(new Rect(0,y,width,24),"Upper release / original body height");y+=28;
        p.UpperTransitionStart=Slider("Upper start above waist datum / span",p.UpperTransitionStart,-.50f,1f,ref y,width);
        p.UpperTransitionWidth=Slider("Upper transition width / span",p.UpperTransitionWidth,.02f,1.50f,ref y,width);
        p.UpperTransitionBias=Slider("Upper curve bias (- hold / + release)",p.UpperTransitionBias,-2,2,ref y,width);
        p.UpperTransitionJoin=Slider("Upper join / transition width",p.UpperTransitionJoin,.02f,1f,ref y,width);
        p.UpperTransitionActivation=Slider("Upper activation displacement / span",p.UpperTransitionActivation,.001f,.15f,ref y,width);
        GUI.Label(new Rect(0,y,width,24),"Additional restrictions (trial defaults: OFF)");y+=28;
        p.BreastExclusionEnabled=Toggle("Exclude breast-weighted vertices",p.BreastExclusionEnabled,ref y,width);
        p.UpperBoneFilterEnabled=Toggle("Upper abdomen bone-weight filter",p.UpperBoneFilterEnabled,ref y,width);
        p.UpperFieldFadeEnabled=Toggle("Extra shape-field top fade",p.UpperFieldFadeEnabled,ref y,width);
        p.AxisPullLow=Slider("Pull at small angles",p.AxisPullLow,0,1,ref y,width);
        p.AxisPullHigh=Slider("Pull at large angles",p.AxisPullHigh,0,1,ref y,width);
        p.AxisPullAngle=Slider("Full-pull angle (degrees)",p.AxisPullAngle,10,120,ref y,width);
        p.AxisAnchorY=Slider("Anchor height / torso span",p.AxisAnchorY,-.2f,.2f,ref y,width);
        p.AxisAnchorZ=Slider("Anchor forward / torso span",p.AxisAnchorZ,-.2f,.2f,ref y,width);
        p.SkinShadingSmoothing=Slider("Skin lighting smoothing (geometry unchanged)",p.SkinShadingSmoothing,0,1,ref y,width);
        GUI.Label(new Rect(0,y,width,24),"Original navel / local geometry");y+=28;
        GUI.Label(new Rect(0,y,width,24),BellyVertexMorph.GetNavelStatus(_selectedCharaId >= 0 ? _selectedCharaId : 0));y+=28;
        bool fullNavel=GUI.Toggle(new Rect(0,y,width,24),p.NavelPreviewFull,"Preview full navel response at current belly size");y+=28;
        if(fullNavel!=p.NavelPreviewFull){p.NavelPreviewFull=fullNavel;BellyVertexMorph.InvalidateAll();}
        GUI.Label(new Rect(0,y,width,24),$"Navel stage response: {BellyShape.NavelStageResponse(BellyVertexMorph.ForceApplyRate,p)*100:F1}% (geometry only)");y+=28;
        if(GUI.Button(new Rect(0,y,width/2-4,28),"Navel visible preset"))
        {
            p.NavelPreviewFull=true;p.NavelEversion=1;p.NavelHeight=.012f;p.NavelRadius=.06f;p.NavelProportion=.85f;
            BellyVertexMorph.InvalidateAll();
        }
        if(GUI.Button(new Rect(width/2+4,y,width/2-4,28),"Navel off"))
        {p.NavelEversion=0;p.NavelProportion=0;BellyVertexMorph.InvalidateAll();}
        y+=35;
        p.NavelEversion=Slider("Navel eversion (0 = off)",p.NavelEversion,0,2,ref y,width);
        p.NavelStart=Slider("Navel change starts at stage",p.NavelStart,.3f,.95f,ref y,width);
        p.NavelHeight=Slider("Navel height / torso span",p.NavelHeight,0,.03f,ref y,width);
        p.NavelRadius=Slider("Navel patch radius / torso span",p.NavelRadius,.015f,.08f,ref y,width);
        p.NavelProportion=Slider("Navel proportion retention",p.NavelProportion,0,1,ref y,width);
        if (GUI.Button(new Rect(0, y, width / 2 - 4, 28), "Default shape"))
        {
            BellyDeformSettings.SetLive(BellyDeformSettings.StartDay, new VtxSettings());
            BellyVertexMorph.InvalidateAll();
        }
        if (GUI.Button(new Rect(width / 2 + 4, y, width / 2 - 4, 28), "Write diagnostic log")) DumpMeshInfoAll();
        y += 35;
        if (GUI.Button(new Rect(0, y, width / 2 - 4, 28), "Full-term shape"))
        {
            BellyDeformSettings.SetLive(BellyDeformSettings.StartDay, new VtxSettings());
            BellyVertexMorph.ForceApplyRate=1f;
            BellyVertexMorph.ForceApplyCharaId=_selectedCharaId;
            BellyVertexMorph.ForceApplyEnabled=true;
            ApplyBellyAll();
            BellyVertexMorph.InvalidateAll();
        }
        if (GUI.Button(new Rect(width / 2 + 4, y, width / 2 - 4, 28), "Rebuild shape")) BellyVertexMorph.InvalidateAll();

            y += 36;
            _vtxContentHeight = y;
            GUI.EndScrollView();
            GUILayout.Label(BellyVertexMorph.GetStatusLine(_selectedCharaId >= 0 ? _selectedCharaId : 0));

            // SVS scheduling and persistence remain separate from manual AL shape controls.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pregnancy belly start day:", GUILayout.Width(190));
            string day = GUILayout.TextField(_startDayBuf, GUILayout.Width(60));
            if (day != _startDayBuf)
            {
                _startDayBuf = day;
                if (int.TryParse(day, out int value) && value >= 0)
                {
                    BellyDeformSettings.SetLive(value, BellyDeformSettings.Vtx);
                    ApplyBellyAll();
                }
            }
            else _startDayBuf = BellyDeformSettings.StartDay.ToString();
            if (GUILayout.Button("Save to File"))
            {
                BellyDeformSettings.Save(BellyDeformSettings.StartDay, BellyDeformSettings.Vtx);
                _bellyMsg = "Saved!";
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_bellyMsg)) GUILayout.Label(_bellyMsg);
        }

    [HideFromIl2Cpp]
    private float Slider(string label, float value, float min, float max, ref float y, float width)
    {
        GUI.Label(new Rect(0, y, width, 24), $"{label}: {value:F3}");
        float next = GUI.HorizontalSlider(new Rect(0, y + 25, width, 20), value, min, max);
        y += 49;
        if (!Mathf.Approximately(value, next)) BellyVertexMorph.InvalidateAll();
        return next;
    }

    [HideFromIl2Cpp]
    private bool Toggle(string label, bool value, ref float y, float width)
    {
        bool next = GUI.Toggle(new Rect(0, y, width, 24), value, label);
        y += 28;
        if (value != next) BellyVertexMorph.InvalidateAll();
        return next;
    }


        private static string GetName(PregnancyCharaController ctrl)
        {
            try { return ctrl._chara.parameter.lastname + " " + ctrl._chara.parameter.firstname; }
            catch { return $"ID:{ctrl._charaId}"; }
        }

        private static void ApplyBellyAll()
        {
            BellyVertexMorph.Paused = false;
            BellyVertexMorph.InvalidateAll();
            var allHC = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<PregnancyHumanController>());
            foreach (var obj in allHC)
            {
                var hc = obj.TryCast<PregnancyHumanController>();
                if (hc != null) hc.MarkDirty();
            }
        }

        private static void ResetBellyAll()
        {
            // 1. Undo every applied deformation and clear all cached state.
            BellyVertexMorph.ForgetAll();
            // 2. Freeze — LateUpdate hooks will not re-apply until Apply is clicked.
            BellyVertexMorph.Paused = true;
        }

        private static void DumpMeshInfoAll()
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("SVSPregnancy.Dump");
            log.LogInfo("[Dump] ======== DumpMeshInfoAll triggered ========");

            var allHC = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<PregnancyHumanController>());
            log.LogInfo($"[Dump] PregnancyHumanController instances: {allHC.Count}");
            foreach (var obj in allHC)
            {
                var hc = obj.TryCast<PregnancyHumanController>();
                if (hc == null) continue;
                try
                {
                    log.LogInfo($"[Dump] PHC id={hc._charaId} sex={hc.GetSex()} inited={hc._inited} humanPtr={hc._humanPtr}");
                    if (hc._human != null)
                        BellyVertexMorph.DumpInfo(hc._human, hc._charaId);
                }
                catch (Exception e) { log.LogInfo("[Dump] PHC error: " + e.Message); }
            }

            log.LogInfo("[Dump] ======== end ========");
        }

        private static void DumpNormalDetailAll()
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("SVSPregnancy.NRDump");
            log.LogInfo("[NRDump] ======== DumpNormalDetailAll triggered ========");

            var allHC = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<PregnancyHumanController>());
            log.LogInfo($"[NRDump] PregnancyHumanController instances: {allHC.Count}");
            foreach (var obj in allHC)
            {
                var hc = obj.TryCast<PregnancyHumanController>();
                if (hc == null) continue;
                try
                {
                    log.LogInfo($"[NRDump] PHC id={hc._charaId} sex={hc.GetSex()} inited={hc._inited}");
                    if (hc._human != null)
                        BellyVertexMorph.DumpNormalRecomputeDetail(hc._human, hc._charaId);
                }
                catch (Exception e) { log.LogInfo("[NRDump] PHC error: " + e.Message); }
            }

            log.LogInfo("[NRDump] ======== end ========");
        }
    }
}
