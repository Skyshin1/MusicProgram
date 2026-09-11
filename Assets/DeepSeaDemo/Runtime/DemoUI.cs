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
        RectTransform panel, hud, noticeStrip, interactionStrip;
        Text hudText, toast;
        CanvasGroup fade;
        Font font;
        float toastUntil;
        bool messagePaused;
        bool subtitles = true;
        string documentTitle, documentBody;
        int documentPage;
        const int PageLength = 720;
        Text hover;
        float hoverUntil;
        public bool SubtitlesEnabled => subtitles;
        readonly Color ink = new(.012f, .045f, .065f, .95f);
        readonly Color panelInk = new(.022f, .085f, .115f, .94f);
        readonly Color accent = new(.22f, .9f, .91f);
        readonly Color amber = new(1f, .67f, .18f);
        void Awake()
        {
            font = config.chineseFont != null ? config.chineseFont : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 36);
            hud = CanvasRoot("Diver HUD", flow.player.Camera.transform, new Vector3(0, -.31f, .9f), new Vector2(880, 140));
            hudText = TextAt(hud, "HUD", "", new Vector2(0, 9), new Vector2(820, 104), 22, Color.white);
            // The lower-gaze objective/oxygen block was visually persistent and
            // distracting in VR. Keep only temporary notices and hover prompts.
            hudText.gameObject.SetActive(false);
            noticeStrip = Strip(hud, "Notice Strip", new Vector2(0, -22), new Vector2(940, 72), new Color(.02f, .08f, .1f, .9f));
            toast = TextAt(noticeStrip, "Notice", "", Vector2.zero, new Vector2(900, 64), 24, amber);
            interactionStrip = Strip(hud, "Interaction Strip", new Vector2(0, -95), new Vector2(820, 58), new Color(.02f, .12f, .14f, .92f));
            hover = TextAt(interactionStrip, "Interaction", "", Vector2.zero, new Vector2(790, 52), 24, accent);
            noticeStrip.gameObject.SetActive(false);
            interactionStrip.gameObject.SetActive(false);
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
        RectTransform Strip(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false); go.layer = LayerIndex(config.uiMask);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false;
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(accent.r, accent.g, accent.b, .55f); outline.effectDistance = new Vector2(2, -2);
            return rt;
        }
        void Frame(RectTransform root, Color fill, Color border)
        {
            var bg = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            bg.color = fill; bg.raycastTarget = false;
            var shadow = root.gameObject.GetComponent<Shadow>() ?? root.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .72f); shadow.effectDistance = new Vector2(10, -10);
            var outline = root.gameObject.GetComponent<Outline>() ?? root.gameObject.AddComponent<Outline>();
            outline.effectColor = border; outline.effectDistance = new Vector2(3, -3); outline.useGraphicAlpha = false;
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
            Frame(panel, ink, new Color(accent.r, accent.g, accent.b, .88f));
            panel.gameObject.layer = LayerIndex(config.uiMask);
            TextAt(panel, "Section", subtitle, new Vector2(0, 335), new Vector2(1040, 55), 21, accent);
            TextAt(panel, "Title", heading, new Vector2(0, 270), new Vector2(1040, 78), 43, Color.white);
            var header = Strip(panel, "Header Divider", new Vector2(0, 226), new Vector2(1010, 4), accent);
            header.GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, .72f);
            var body = Strip(panel, "Content Well", new Vector2(0, -28), new Vector2(1020, 470), new Color(panelInk.r, panelInk.g, panelInk.b, .72f));
            body.SetAsFirstSibling();
        }
        void Button(string label, string command, float y, float x = 0, float width = 770)
        {
            var go = new GameObject("Button " + command, typeof(RectTransform), typeof(Image), typeof(DemoUIButton));
            go.transform.SetParent(panel, false); var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(width, 70);
            // Selectable tints multiply the Image color; use white here so the
            // highlighted fill can actually become bright instead of darker.
            go.GetComponent<Image>().color = Color.white; go.layer = LayerIndex(config.uiMask);
            var button = go.GetComponent<DemoUIButton>();
            button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.normalColor = new Color(.045f, .16f, .2f, 1);
            colors.highlightedColor = accent;
            colors.selectedColor = colors.normalColor; colors.pressedColor = amber; colors.fadeDuration = .06f;
            button.colors = colors; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Command(command));
            button.label = TextAt(rt, "Label", label, Vector2.zero, new Vector2(width - 18, 68), 28, Color.white);
            button.hoverOutline = go.AddComponent<Outline>();
            button.hoverOutline.effectColor = Color.white;
            button.hoverOutline.effectDistance = new Vector2(3, -3);
            button.hoverOutline.useGraphicAlpha = false;
            var marker = new GameObject("Hover Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(rt, false); marker.layer = LayerIndex(config.uiMask);
            var markerRect = (RectTransform)marker.transform; markerRect.anchorMin = new Vector2(0, 0); markerRect.anchorMax = new Vector2(0, 1);
            markerRect.pivot = new Vector2(0, .5f); markerRect.anchoredPosition = new Vector2(9, 0); markerRect.sizeDelta = new Vector2(9, -12);
            marker.GetComponent<Image>().color = amber; marker.GetComponent<Image>().raycastTarget = false;
            button.hoverMarker = marker.GetComponent<Image>(); marker.SetActive(false);
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .55f); shadow.effectDistance = new Vector2(5, -5);
            button.RefreshVisual();
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
        public void Toast(string value) { if (toast != null) { toast.text = value; toastUntil = Time.unscaledTime + 6; noticeStrip.gameObject.SetActive(!string.IsNullOrEmpty(value)); } }
        public void SetHover(string value) { if (hover != null) { hover.text = value; hoverUntil = Time.unscaledTime + .1f; interactionStrip.gameObject.SetActive(!string.IsNullOrEmpty(value)); } }
        public void SetFade(float value) { if (fade != null) fade.alpha = Mathf.Clamp01(value); }
        void Update()
        {
            if (toast != null && Time.unscaledTime > toastUntil) { toast.text = ""; noticeStrip.gameObject.SetActive(false); }
            if (hover != null && Time.unscaledTime > hoverUntil) { hover.text = ""; interactionStrip.gameObject.SetActive(false); }
            hud.gameObject.SetActive(flow.Running && !Modal);
        }
    }
}
