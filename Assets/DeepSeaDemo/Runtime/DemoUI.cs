using System;
using DeepSeaAI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeepSeaDemo
{
    public sealed class DemoUI : MonoBehaviour
    {
        public DemoFlow flow;
        public DemoConfig config;
        public bool Modal { get; private set; }
        RectTransform panel, hud;
        Text hudText, toast;
        CanvasGroup fade;
        Font font;
        float toastUntil, nextHud;
        bool messagePaused;
        bool subtitles = true;
        string documentTitle, documentBody;
        int documentPage;
        const int PageLength = 720;
        Text hover;
        float hoverUntil;
        public bool SubtitlesEnabled => subtitles;
        readonly Color ink = new(.025f, .06f, .09f, .97f);
        readonly Color accent = new(.3f, .92f, .86f);
        void Awake()
        {
            font = config.chineseFont != null ? config.chineseFont : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 36);
            hud = CanvasRoot("Diver HUD", flow.player.Camera.transform, new Vector3(0, -.31f, .9f), new Vector2(880, 140));
            hudText = TextAt(hud, "HUD", "", new Vector2(0, 20), new Vector2(850, 110), 23, Color.white);
            toast = TextAt(hud, "Notice", "", new Vector2(0, -92), new Vector2(950, 90), 25, accent);
            hover = TextAt(hud, "Interaction", "", new Vector2(0, -160), new Vector2(900, 60), 25, accent);
            var fadeRoot = CanvasRoot("Safety Fade", flow.player.Camera.transform, new Vector3(0, 0, .18f), new Vector2(3000, 3000));
            var canvas = fadeRoot.GetComponent<Canvas>(); canvas.sortingOrder = 30000;
            var image = fadeRoot.gameObject.AddComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
            fade = fadeRoot.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = false;
            var events = FindFirstObjectByType<EventSystem>();
            if (events == null) events = new GameObject("Demo EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var module in events.GetComponents<BaseInputModule>()) if (module is not XRUIInputModule) module.enabled = false;
            var xr = events.GetComponent<XRUIInputModule>() ?? events.gameObject.AddComponent<XRUIInputModule>();
            xr.enabled = true; xr.enableXRInput = true;
            var actions = flow.player.GetComponent<DemoXRInput>();
            xr.enableMouseInput = actions != null && actions.DesktopUI;
            xr.enableTouchInput = false; xr.enableGamepadInput = false; xr.enableJoystickInput = false;
        }
        RectTransform CanvasRoot(string name, Transform parent, Vector3 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.layer = LayerIndex(config.uiMask);
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false); rt.localPosition = position;
            rt.localScale = Vector3.one * .001f; rt.sizeDelta = size;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = flow.player.Camera;
            canvas.sortingOrder = 1000;
            var raycaster = go.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor3DOcclusion = false;
            raycaster.checkFor2DOcclusion = false;
            return rt;
        }
        Text TextAt(Transform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            go.layer = LayerIndex(config.uiMask);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = fontSize;
            text.color = color; text.alignment = TextAnchor.MiddleCenter; text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
            return text;
        }
        void BeginPanel(string heading, string subtitle = "DEEP SEA / FIELD INVESTIGATION")
        {
            ClosePanel(); Modal = true;
            Transform head = flow.player.Camera.transform;
            // Use an HMD-relative world-space canvas. Tracking updates can arrive after
            // Start and during BeforeRender; parenting keeps both position and direction
            // correct without an Update-order race or changing the headset's transform.
            panel = CanvasRoot("Investigation Panel", head, Vector3.forward * 1.25f, new Vector2(1120, 790));
            var bg = panel.gameObject.AddComponent<Image>(); bg.color = ink; bg.raycastTarget = false;
            panel.gameObject.layer = LayerIndex(config.uiMask);
            TextAt(panel, "Section", subtitle, new Vector2(0, 335), new Vector2(1040, 55), 21, accent);
            TextAt(panel, "Title", heading, new Vector2(0, 270), new Vector2(1040, 78), 43, Color.white);
        }
        void Button(string label, string command, float y, float x = 0, float width = 770)
        {
            var go = new GameObject("Button " + command, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(panel, false); var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(width, 70);
            go.GetComponent<Image>().color = new Color(.075f, .19f, .24f, 1); go.layer = LayerIndex(config.uiMask);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.45f, 1f, .9f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(1f, .8f, .35f); colors.fadeDuration = .08f;
            button.colors = colors; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Command(command));
            TextAt(rt, "Label", label, Vector2.zero, new Vector2(width - 18, 68), 28, Color.white);
        }
        public static int LayerIndex(LayerMask mask)
        { for (int i = 0; i < 32; i++) if ((mask.value & (1 << i)) != 0) return i; return 0; }
        void ClosePanel() { if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); } panel = null; Modal = false; }
        public void ShowMain()
        {
            BeginPanel(DemoTextCatalog.Get("runtime.025"));
            Button(DemoTextCatalog.Get("runtime.026"), "new", 130);
            if (flow.HasSave) Button(DemoTextCatalog.Get("runtime.027"), "continue", 35);
            Button(DemoTextCatalog.Get("runtime.028"), "settings", -60); Button(DemoTextCatalog.Get("runtime.029"), "quit", -155);
            TextAt(panel, "Note", DemoTextCatalog.Get("runtime.030"), new Vector2(0, -290), new Vector2(1040, 110), 23, accent);
        }
        public void ShowPause()
        {
            BeginPanel(DemoTextCatalog.Get("runtime.031")); Button(DemoTextCatalog.Get("runtime.032"), "resume", 150); Button(DemoTextCatalog.Get("runtime.028"), "settings", 55);
            Button(DemoTextCatalog.Get("runtime.033"), "safe", -40); Button(DemoTextCatalog.Get("runtime.034"), "retry", -135); Button(DemoTextCatalog.Get("runtime.035"), "main", -230);
        }
        public void ShowSettings()
        {
            BeginPanel(DemoTextCatalog.Get("runtime.036"));
            Button(DemoTextCatalog.Get("runtime.037"), "vol-", 130, -235, 430); Button(DemoTextCatalog.Get("runtime.038"), "vol+", 130, 235, 430);
            Button(DemoTextCatalog.Get("runtime.039"), "turn", 45); Button(DemoTextCatalog.Get("runtime.040"), "speed", -40);
            Button(DemoTextCatalog.Get("runtime.041"), "lightning", -125);
            Button(DemoTextCatalog.Get("runtime.042") + (subtitles ? DemoTextCatalog.Get("runtime.043") : DemoTextCatalog.Get("runtime.044")) + DemoTextCatalog.Get("runtime.045"), "subtitles", -210);
            Button(DemoTextCatalog.Get("runtime.046"), "back", -295);
        }
        public void ShowHUD() { ClosePanel(); hud.gameObject.SetActive(true); }
        public void ShowMessage(string heading, string content, bool canClose)
        {
            if (canClose) { messagePaused = flow.Paused; flow.SetPaused(true); }
            documentTitle = heading; documentBody = content; documentPage = 0;
            RenderDocument(canClose);
        }
        void RenderDocument(bool canClose)
        {
            string heading = documentTitle;
            int pages = Mathf.Max(1, Mathf.CeilToInt(documentBody.Length / (float)PageLength));
            int start = documentPage * PageLength;
            string content = documentBody.Substring(start, Mathf.Min(PageLength, documentBody.Length - start));
            BeginPanel(heading, DemoTextCatalog.Get("runtime.047"));
            var text = TextAt(panel, "Body", content, new Vector2(0, -5), new Vector2(990, 420), 27, Color.white);
            text.alignment = TextAnchor.UpperLeft;
            if (canClose) Button(DemoTextCatalog.Get("runtime.048"), "close", -290);
            if (pages > 1)
            {
                Button("<", "page-", -205, -440, 80); Button(">", "page+", -205, 440, 80);
                TextAt(panel, "Page", (documentPage + 1) + " / " + pages, new Vector2(0, -215), new Vector2(220, 40), 20, accent);
            }
        }
        public void ShowAnalysis()
        {
            flow.SetPaused(true); BeginPanel(DemoTextCatalog.Get("runtime.049"), DemoTextCatalog.Get("runtime.050"));
            string evidence = DemoTextCatalog.Get("runtime.051") +
                (flow.State.Has("sensor") ? DemoTextCatalog.Get("runtime.052") : DemoTextCatalog.Get("runtime.053")) +
                (flow.State.Has("sample") ? DemoTextCatalog.Get("runtime.054") : DemoTextCatalog.Get("runtime.055")) +
                (flow.State.Has("testimony") ? DemoTextCatalog.Get("runtime.056") : DemoTextCatalog.Get("runtime.057")) +
                DemoTextCatalog.Get("runtime.058");
            TextAt(panel, "Evidence", evidence, new Vector2(0, 40), new Vector2(980, 355), 27, Color.white);
            Button(DemoTextCatalog.Get("runtime.059"), "preserve", -200); Button(DemoTextCatalog.Get("runtime.060"), "uploadconfirm", -295);
        }
        public void ShowEnding(bool uploaded)
        {
            BeginPanel(uploaded ? DemoTextCatalog.Get("runtime.061") : DemoTextCatalog.Get("runtime.062"), DemoTextCatalog.Get("runtime.010"));
            string result = uploaded ? DemoTextCatalog.Get("runtime.063") :
                DemoTextCatalog.Get("runtime.064");
            TextAt(panel, "Outcome", result + DemoTextCatalog.Get("runtime.065"), Vector2.zero, new Vector2(990, 350), 30, Color.white);
            Button(DemoTextCatalog.Get("runtime.066"), "new", -215); Button(DemoTextCatalog.Get("runtime.067"), "main", -310);
        }
        public void Command(string command)
        {
            switch (command)
            {
                case "page-": documentPage = Mathf.Max(0, documentPage - 1); RenderDocument(true); break;
                case "page+": documentPage = Mathf.Min(Mathf.Max(0, (documentBody.Length - 1) / PageLength), documentPage + 1); RenderDocument(true); break;
                case "new": flow.NewGame(); break;
                case "continue": flow.ContinueGame(); break;
                case "resume": flow.Resume(); break;
                case "main": flow.MainMenu(); break;
                case "retry": flow.Retry(); break;
                case "safe": flow.ReturnToSafePosition(); flow.Resume(); break;
                case "settings": ShowSettings(); break;
                case "back": if (flow.Running) ShowPause(); else ShowMain(); break;
                case "close": ClosePanel(); flow.SetPaused(messagePaused); if (!messagePaused) ShowHUD(); break;
                case "preserve": flow.ChooseEnding(false); break;
                case "uploadconfirm":
                    BeginPanel(DemoTextCatalog.Get("runtime.068"));
                    TextAt(panel, "Confirm", DemoTextCatalog.Get("runtime.069"), Vector2.zero, new Vector2(980, 260), 32, Color.white);
                    Button(DemoTextCatalog.Get("runtime.070"), "upload", -160); Button(DemoTextCatalog.Get("runtime.071"), "analysis", -260); break;
                case "analysis": ShowAnalysis(); break;
                case "upload": flow.ChooseEnding(true); break;
                case "vol-": AudioListener.volume = Mathf.Clamp01(AudioListener.volume - .1f); Toast(DemoTextCatalog.Get("runtime.072") + Mathf.RoundToInt(AudioListener.volume * 100) + "%"); break;
                case "vol+": AudioListener.volume = Mathf.Clamp01(AudioListener.volume + .1f); Toast(DemoTextCatalog.Get("runtime.072") + Mathf.RoundToInt(AudioListener.volume * 100) + "%"); break;
                case "turn": flow.player.GetComponent<QuestLeftStickLocomotion>().ToggleTurnMode(); Toast(DemoTextCatalog.Get("runtime.073")); break;
                case "speed": flow.player.GetComponent<QuestLeftStickLocomotion>().ToggleComfortSpeed(); Toast(DemoTextCatalog.Get("runtime.074")); break;
                case "lightning": flow.weather?.ToggleSoftLightning(); Toast(DemoTextCatalog.Get("runtime.075")); break;
                case "subtitles": subtitles = !subtitles; ShowSettings(); break;
                case "quit": Application.Quit(); break;
            }
        }
        public void Toast(string value) { if (toast != null) { toast.text = value; toastUntil = Time.unscaledTime + 6; } }
        public void SetHover(string value) { if (hover != null) { hover.text = value; hoverUntil = Time.unscaledTime + .1f; } }
        public void SetFade(float value) { if (fade != null) fade.alpha = Mathf.Clamp01(value); }
        void Update()
        {
            if (toast != null && Time.unscaledTime > toastUntil) toast.text = "";
            if (hover != null && Time.unscaledTime > hoverUntil) hover.text = "";
            if (Time.unscaledTime < nextHud) return; nextHud = Time.unscaledTime + .2f;
            hud.gameObject.SetActive(flow.Running && !Modal);
            if (flow.Running)
            {
                float oxygen = flow.player.GetComponent<PlayerOxygen>().NormalizedOxygen * 100f;
                float exposure = FindFirstObjectByType<DemoAcoustics>()?.Exposure ?? 0;
                hudText.text = flow.Objective + DemoTextCatalog.Get("runtime.076") + oxygen.ToString("0") + DemoTextCatalog.Get("runtime.077") + exposure.ToString("0") + "/100";
            }
        }
    }
}
