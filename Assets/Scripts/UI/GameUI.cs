using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonSnake
{
    /// <summary>
    /// Builds the whole uGUI layer in code: HUD, ready screen, pause menu, game over,
    /// floating score popups, banners and the countdown. Driven by SnakeGame via Tick().
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public const float TopBarHeight = 190f;
        const float RefWidth = 1080f, RefHeight = 1920f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Color Ink = new Color(0.04f, 0.05f, 0.1f, 1f);

        public event Action PauseClicked, ResumeClicked, RestartClicked, SoundClicked, HapticsClicked;

        class Overlay
        {
            public GameObject go;
            public CanvasGroup group;
            public bool visible;
        }

        class PopupItem
        {
            public RectTransform rect;
            public Text text;
            public Vector2 start;
            public float age = 99f, scale = 1f;
        }

        class Pill
        {
            public GameObject go;
            public RectTransform fill;
        }

        Font font;
        Camera cam;
        CanvasScaler scaler;
        RectTransform canvasRect, safeRect, popupLayer;
        readonly List<Graphic> accentGraphics = new List<Graphic>();
        readonly List<Outline> accentOutlines = new List<Outline>();
        readonly List<PopupItem> popups = new List<PopupItem>();
        Color accent = new Color(-1f, 0f, 0f, 0f);

        Text scoreText, bestText, levelText, comboText;
        GameObject comboBar;
        RectTransform comboFill;
        Pill ghostPill, slowPill;
        int targetScore, shownBest = -1, shownCombo = -1, shownLevel = -1;
        float shownScore, scorePunch;

        Overlay ready, pause, over;
        Text titleText, readyBestText, hintText;
        GameObject legend;
        Text soundText, hapticsText;
        Text overTitle, overScore, overBest, overStats, newBestText;
        RectTransform againButton;
        Text bannerText, countdownText;
        float bannerAge = 99f, countdownAge = 99f, flashAlpha, clock;
        Image flash;

        /// <summary>Canvas units to screen pixels (matches the CanvasScaler).</summary>
        public float CanvasScale => Screen.width > Screen.height ? Screen.height / RefHeight : Screen.width / RefWidth;

        public static GameUI Create(Camera cam)
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var go = new GameObject("Game UI", typeof(RectTransform));
            var ui = go.AddComponent<GameUI>();
            ui.Build(cam);
            return ui;
        }

        void Build(Camera camera)
        {
            cam = camera;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)transform;

            safeRect = NewRect("SafeArea", canvasRect);
            Stretch(safeRect);
            BuildHud();

            popupLayer = NewRect("Popups", canvasRect);
            Stretch(popupLayer);

            bannerText = Label(canvasRect, "", 96, TextAnchor.MiddleCenter, Color.white);
            Place(bannerText.rectTransform, new Vector2(0.5f, 0.62f), Center, Vector2.zero, new Vector2(1000f, 140f));
            AddShadow(bannerText);
            bannerText.enabled = false;

            countdownText = Label(canvasRect, "", 240, TextAnchor.MiddleCenter, Color.white, true);
            Place(countdownText.rectTransform, Center, Center, Vector2.zero, new Vector2(600f, 300f));
            AddGlow(countdownText, 0.35f);
            countdownText.enabled = false;

            BuildReady();
            BuildPause();
            BuildGameOver();

            flash = Img(canvasRect, null, new Color(1f, 1f, 1f, 0f));
            Stretch(flash.rectTransform);

            ApplyLayout();
        }

        // ------------------------------------------------------------------ layout

        public void ApplyLayout()
        {
            scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            Rect s = Screen.safeArea;
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            safeRect.anchorMin = new Vector2(s.xMin / w, s.yMin / h);
            safeRect.anchorMax = new Vector2(s.xMax / w, s.yMax / h);
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
        }

        void BuildHud()
        {
            var bar = NewRect("TopBar", safeRect);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, TopBarHeight);

            var tl = new Vector2(0f, 1f);
            var tc = new Vector2(0.5f, 1f);
            var tr = new Vector2(1f, 1f);
            var left = new Vector2(0f, 0.5f);
            var right = new Vector2(1f, 0.5f);

            var scoreLabel = Label(bar, "SCORE", 30, TextAnchor.MiddleLeft, Faded(0.55f));
            Place(scoreLabel.rectTransform, tl, left, new Vector2(44f, -42f), new Vector2(300f, 40f));
            scoreText = Label(bar, "0", 88, TextAnchor.MiddleLeft, Color.white);
            Place(scoreText.rectTransform, tl, left, new Vector2(40f, -104f), new Vector2(460f, 100f));

            levelText = Label(bar, "LEVEL 1", 34, TextAnchor.MiddleCenter, Color.white, true);
            Place(levelText.rectTransform, tc, Center, new Vector2(0f, -42f), new Vector2(320f, 44f));
            comboText = Label(bar, "", 48, TextAnchor.MiddleCenter, Color.white, true);
            Place(comboText.rectTransform, tc, Center, new Vector2(0f, -98f), new Vector2(360f, 60f));

            var cb = NewRect("ComboBar", bar);
            Place(cb, tc, Center, new Vector2(0f, -138f), new Vector2(240f, 10f));
            var cbBack = Img(cb, Art.Pixel, Faded(0.15f));
            Stretch(cbBack.rectTransform);
            var cbFill = Img(cb, Art.Pixel, Color.white, true);
            Stretch(cbFill.rectTransform);
            comboFill = cbFill.rectTransform;
            comboBar = cb.gameObject;
            comboBar.SetActive(false);

            var bestLabel = Label(bar, "BEST", 30, TextAnchor.MiddleRight, Faded(0.55f));
            Place(bestLabel.rectTransform, tr, right, new Vector2(-180f, -42f), new Vector2(300f, 40f));
            bestText = Label(bar, "0", 52, TextAnchor.MiddleRight, Color.white);
            Place(bestText.rectTransform, tr, right, new Vector2(-180f, -100f), new Vector2(320f, 70f));

            var pauseBg = Img(bar, Art.UIPanel, Faded(0.1f));
            pauseBg.raycastTarget = true;
            Place(pauseBg.rectTransform, tr, right, new Vector2(-36f, -86f), new Vector2(112f, 112f));
            var pauseButton = pauseBg.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseBg;
            pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            var barL = Img(pauseBg.transform, Art.Pixel, Color.white);
            Place(barL.rectTransform, Center, Center, new Vector2(-13f, 0f), new Vector2(14f, 44f));
            var barR = Img(pauseBg.transform, Art.Pixel, Color.white);
            Place(barR.rectTransform, Center, Center, new Vector2(13f, 0f), new Vector2(14f, 44f));

            ghostPill = MakePill(bar, Art.Diamond, Palette.Ghost, new Vector2(-140f, -172f));
            slowPill = MakePill(bar, Art.Ring, Palette.Clock, new Vector2(140f, -172f));
        }

        Pill MakePill(RectTransform parent, Sprite icon, Color color, Vector2 pos)
        {
            var r = NewRect("Pill", parent);
            Place(r, new Vector2(0.5f, 1f), Center, pos, new Vector2(230f, 36f));
            var left = new Vector2(0f, 0.5f);
            var i = Img(r, icon, color);
            Place(i.rectTransform, left, left, Vector2.zero, new Vector2(34f, 34f));
            var back = Img(r, Art.Pixel, Faded(0.15f));
            Place(back.rectTransform, left, left, new Vector2(48f, 0f), new Vector2(180f, 10f));
            var fill = Img(back.transform, Art.Pixel, color);
            Stretch(fill.rectTransform);
            r.gameObject.SetActive(false);
            return new Pill { go = r.gameObject, fill = fill.rectTransform };
        }

        void BuildReady()
        {
            ready = NewOverlay("Ready", 0.5f, false);
            var root = ready.go.transform;

            titleText = Label(root, "NEON\nSNAKE", 170, TextAnchor.MiddleCenter, Color.white, true);
            titleText.lineSpacing = 0.85f;
            AddGlow(titleText, 0.35f);
            Place(titleText.rectTransform, new Vector2(0.5f, 0.77f), Center, Vector2.zero, new Vector2(1000f, 420f));

            readyBestText = Label(root, "", 40, TextAnchor.MiddleCenter, Faded(0.7f));
            Place(readyBestText.rectTransform, new Vector2(0.5f, 0.63f), Center, Vector2.zero, new Vector2(800f, 60f));

            var lg = NewRect("Legend", root);
            Place(lg, new Vector2(0.5f, 0.41f), Center, Vector2.zero, new Vector2(820f, 520f));
            legend = lg.gameObject;
            LegendRow(lg, 200f, Art.Circle, Palette.Berry, "Berry", "grow and score");
            LegendRow(lg, 115f, Art.Star, Palette.Star, "Star", "big bonus, grab it fast");
            LegendRow(lg, 30f, Art.Diamond, Palette.Ghost, "Ghost", "slip through your own tail");
            LegendRow(lg, -55f, Art.Ring, Palette.Clock, "Clock", "slow motion");
            LegendRow(lg, -140f, Art.Triangle, Palette.Shrink, "Shrink", "lose some tail");
            var tip = Label(lg, "Eat quickly to chain combos up to x5", 34, TextAnchor.MiddleCenter, Faded(0.65f));
            Place(tip.rectTransform, Center, Center, new Vector2(0f, -235f), new Vector2(820f, 50f));

            hintText = Label(root, "SWIPE TO SLITHER", 56, TextAnchor.MiddleCenter, Color.white);
            Place(hintText.rectTransform, new Vector2(0.5f, 0.13f), Center, Vector2.zero, new Vector2(900f, 80f));
        }

        void LegendRow(RectTransform parent, float y, Sprite icon, Color color, string name, string desc)
        {
            var img = Img(parent, icon, color);
            Place(img.rectTransform, Center, Center, new Vector2(-330f, y), new Vector2(60f, 60f));
            var t = Label(parent, $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}</color>   {desc}", 36,
                TextAnchor.MiddleLeft, Faded(0.9f));
            t.supportRichText = true;
            Place(t.rectTransform, Center, new Vector2(0f, 0.5f), new Vector2(-270f, y), new Vector2(660f, 60f));
        }

        void BuildPause()
        {
            pause = NewOverlay("Pause", 0.8f, true);
            var root = pause.go.transform;

            var title = Label(root, "PAUSED", 120, TextAnchor.MiddleCenter, Color.white, true);
            AddGlow(title, 0.3f);
            Place(title.rectTransform, new Vector2(0.5f, 0.72f), Center, Vector2.zero, new Vector2(900f, 160f));

            var anchor = new Vector2(0.5f, 0.45f);
            var size = new Vector2(620f, 130f);
            MakeButton(root, "RESUME", anchor, new Vector2(0f, 160f), size, true, () => ResumeClicked?.Invoke());
            soundText = MakeButton(root, "SOUND: ON", anchor, Vector2.zero, size, false, () => SoundClicked?.Invoke());
            hapticsText = MakeButton(root, "VIBRATION: ON", anchor, new Vector2(0f, -160f), size, false, () => HapticsClicked?.Invoke());
            MakeButton(root, "RESTART", anchor, new Vector2(0f, -320f), size, false, () => RestartClicked?.Invoke());
        }

        void BuildGameOver()
        {
            over = NewOverlay("GameOver", 0.72f, false);
            var root = over.go.transform;

            overTitle = Label(root, "GAME OVER", 112, TextAnchor.MiddleCenter, Palette.Berry);
            Place(overTitle.rectTransform, new Vector2(0.5f, 0.79f), Center, Vector2.zero, new Vector2(1000f, 150f));
            AddShadow(overTitle);

            var scoreLabel = Label(root, "SCORE", 36, TextAnchor.MiddleCenter, Faded(0.6f));
            Place(scoreLabel.rectTransform, new Vector2(0.5f, 0.7f), Center, Vector2.zero, new Vector2(600f, 50f));
            overScore = Label(root, "0", 200, TextAnchor.MiddleCenter, Color.white, true);
            AddGlow(overScore, 0.3f);
            Place(overScore.rectTransform, new Vector2(0.5f, 0.62f), Center, Vector2.zero, new Vector2(1000f, 240f));

            newBestText = Label(root, "NEW BEST!", 62, TextAnchor.MiddleCenter, Palette.Star);
            Place(newBestText.rectTransform, new Vector2(0.5f, 0.535f), Center, Vector2.zero, new Vector2(800f, 80f));

            overBest = Label(root, "", 42, TextAnchor.MiddleCenter, Faded(0.75f));
            Place(overBest.rectTransform, new Vector2(0.5f, 0.48f), Center, Vector2.zero, new Vector2(800f, 60f));
            overStats = Label(root, "", 36, TextAnchor.MiddleCenter, Faded(0.6f));
            Place(overStats.rectTransform, new Vector2(0.5f, 0.435f), Center, Vector2.zero, new Vector2(1000f, 50f));

            // Visual only: a tap anywhere restarts (handled by SwipeInput).
            var again = MakeButton(root, "PLAY AGAIN", new Vector2(0.5f, 0.29f), Vector2.zero, new Vector2(640f, 150f), true, null);
            againButton = (RectTransform)again.transform.parent;
            var tap = Label(root, "tap anywhere", 32, TextAnchor.MiddleCenter, Faded(0.45f));
            Place(tap.rectTransform, new Vector2(0.5f, 0.215f), Center, Vector2.zero, new Vector2(600f, 50f));
        }

        // ------------------------------------------------------------------ public API

        public void ResetHud(int best)
        {
            targetScore = 0;
            shownScore = 0f;
            scorePunch = 0f;
            scoreText.text = "0";
            SetLevel(1);
            SetBest(best);
            SetCombo(0, 0f);
            SetPowers(0f, 0f);
        }

        public void SetScore(int score)
        {
            if (score > targetScore) scorePunch = 1f;
            targetScore = score;
        }

        public void SetBest(int best)
        {
            if (best == shownBest) return;
            shownBest = best;
            bestText.text = best.ToString();
        }

        public void SetLevel(int level)
        {
            if (level == shownLevel) return;
            shownLevel = level;
            levelText.text = $"LEVEL {level}";
        }

        /// <param name="multiplier">0 hides the combo display.</param>
        public void SetCombo(int multiplier, float fraction)
        {
            if (multiplier != shownCombo)
            {
                shownCombo = multiplier;
                comboText.text = multiplier >= 2 ? $"COMBO x{multiplier}" : "";
                comboBar.SetActive(multiplier >= 2);
            }
            if (multiplier >= 2) SetFill(comboFill, fraction);
        }

        public void SetPowers(float ghostFraction, float slowFraction)
        {
            UpdatePill(ghostPill, ghostFraction);
            UpdatePill(slowPill, slowFraction);
        }

        public void SetToggles(bool sound, bool haptics)
        {
            soundText.text = sound ? "SOUND: ON" : "SOUND: OFF";
            hapticsText.text = haptics ? "VIBRATION: ON" : "VIBRATION: OFF";
        }

        public void SetAccent(Color c)
        {
            if (c == accent) return;
            accent = c;
            foreach (var g in accentGraphics) g.color = c.WithAlpha(g.color.a);
            foreach (var o in accentOutlines) o.effectColor = c.WithAlpha(o.effectColor.a);
        }

        public void ShowReady(bool firstTime, int best)
        {
            titleText.text = firstTime ? "NEON\nSNAKE" : "READY?";
            titleText.fontSize = firstTime ? 170 : 130;
            legend.SetActive(firstTime);
            readyBestText.text = best > 0 ? $"BEST  {best}" : "";
            Show(ready, true);
        }

        public void HideReady() => Show(ready, false);
        public void ShowPause() => Show(pause, true);
        public void HidePause() => Show(pause, false);
        public void HideGameOver() => Show(over, false);

        public void ShowGameOver(bool won, int score, int best, bool newBest, int length, int level, float seconds)
        {
            overTitle.text = won ? "BOARD CLEARED!" : "GAME OVER";
            overTitle.color = won ? Palette.Star : Palette.Berry;
            overScore.text = score.ToString();
            newBestText.gameObject.SetActive(newBest);
            overBest.text = $"BEST  {best}";
            int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f);
            overStats.text = $"LENGTH {length}   ·   LEVEL {level}   ·   {m}:{s:00}";
            Show(over, true);
        }

        public void Banner(string text, Color color)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerText.enabled = true;
            bannerAge = 0f;
        }

        /// <param name="n">Number to show, or 0 to hide.</param>
        public void SetCountdown(int n)
        {
            countdownText.enabled = n > 0;
            if (n <= 0) return;
            countdownText.text = n.ToString();
            countdownAge = 0f;
        }

        public void Flash(Color c, float alpha)
        {
            flashAlpha = alpha;
            flash.color = c.WithAlpha(alpha);
        }

        public void Popup(Vector3 world, string text, Color color, float scale = 1f)
        {
            PopupItem p = null;
            foreach (var q in popups)
                if (q.age >= 0.9f) { p = q; break; }
            if (p == null)
            {
                var t = Label(popupLayer, "", 46, TextAnchor.MiddleCenter, Color.white);
                Place(t.rectTransform, Center, Center, Vector2.zero, new Vector2(400f, 70f));
                AddShadow(t);
                p = new PopupItem { rect = t.rectTransform, text = t };
                popups.Add(p);
            }
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screen, null, out p.start);
            p.age = 0f;
            p.scale = scale;
            p.text.text = text;
            p.text.color = color;
            p.text.enabled = true;
            p.rect.anchoredPosition = p.start;
            p.rect.localScale = Vector3.zero;
        }

        public void Tick(float dt)
        {
            clock += dt;

            if (!Mathf.Approximately(shownScore, targetScore))
            {
                float speed = Mathf.Max(30f, Mathf.Abs(targetScore - shownScore) * 8f);
                shownScore = Mathf.MoveTowards(shownScore, targetScore, speed * dt);
                scoreText.text = Mathf.RoundToInt(shownScore).ToString();
            }
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, dt * 4f);
            scoreText.rectTransform.localScale = Vector3.one * (1f + 0.22f * scorePunch * scorePunch);

            TickOverlay(ready, dt);
            TickOverlay(pause, dt);
            TickOverlay(over, dt);

            if (ready.go.activeSelf)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(clock * 4f);
                hintText.color = Faded(0.45f + 0.55f * p);
            }
            if (over.go.activeSelf)
            {
                newBestText.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(clock * 7f));
                againButton.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(clock * 4f));
            }

            if (bannerText.enabled)
            {
                bannerAge += dt;
                float s = bannerAge < 0.22f ? Mathf.Lerp(1.7f, 1f, Ease.OutCubic(bannerAge / 0.22f)) : 1f;
                float a = bannerAge < 1.1f ? 1f : 1f - (bannerAge - 1.1f) / 0.4f;
                if (a <= 0f) bannerText.enabled = false;
                bannerText.rectTransform.localScale = Vector3.one * s;
                bannerText.color = bannerText.color.WithAlpha(Mathf.Clamp01(a));
            }

            if (countdownText.enabled)
            {
                countdownAge += dt;
                float s = countdownAge < 0.2f ? Mathf.Lerp(1.6f, 1f, Ease.OutCubic(countdownAge / 0.2f)) : 1f;
                countdownText.rectTransform.localScale = Vector3.one * s;
                countdownText.color = countdownText.color.WithAlpha(1f - Mathf.Clamp01((countdownAge - 0.3f) / 0.2f));
            }

            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.4f);
                flash.color = flash.color.WithAlpha(flashAlpha);
            }

            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                if (p.age >= 0.9f) continue;
                p.age += dt;
                if (p.age >= 0.9f)
                {
                    p.text.enabled = false;
                    continue;
                }
                float u = p.age / 0.9f;
                p.rect.anchoredPosition = p.start + new Vector2(0f, 150f * Ease.OutCubic(u));
                float pop = p.age < 0.12f
                    ? Mathf.Lerp(0.4f, 1.25f, p.age / 0.12f)
                    : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((p.age - 0.12f) / 0.15f));
                p.rect.localScale = Vector3.one * (pop * p.scale);
                p.text.color = p.text.color.WithAlpha(u < 0.55f ? 1f : 1f - (u - 0.55f) / 0.45f);
            }
        }

        // ------------------------------------------------------------------ helpers

        static Color Faded(float alpha) => new Color(1f, 1f, 1f, alpha);

        static void SetFill(RectTransform fill, float fraction)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        static void UpdatePill(Pill pill, float fraction)
        {
            bool on = fraction > 0f;
            if (pill.go.activeSelf != on) pill.go.SetActive(on);
            if (on) SetFill(pill.fill, fraction);
        }

        Overlay NewOverlay(string name, float dim, bool blocksInput)
        {
            var r = NewRect(name, canvasRect);
            Stretch(r);
            var o = new Overlay { go = r.gameObject, group = r.gameObject.AddComponent<CanvasGroup>() };
            var bg = Img(r, null, new Color(0.01f, 0.02f, 0.05f, dim));
            Stretch(bg.rectTransform);
            bg.raycastTarget = blocksInput;
            o.group.alpha = 0f;
            o.group.blocksRaycasts = false;
            o.group.interactable = false;
            r.gameObject.SetActive(false);
            return o;
        }

        static void Show(Overlay o, bool show)
        {
            o.visible = show;
            o.group.interactable = show;
            o.group.blocksRaycasts = show;
            if (show) o.go.SetActive(true);
        }

        static void TickOverlay(Overlay o, float dt)
        {
            if (!o.go.activeSelf) return;
            o.group.alpha = Mathf.MoveTowards(o.group.alpha, o.visible ? 1f : 0f, dt * 5f);
            if (!o.visible && o.group.alpha <= 0f) o.go.SetActive(false);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = Center;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        Text Label(Transform parent, string text, int size, TextAnchor align, Color color, bool accentColored = false)
        {
            var t = NewRect("Text", parent).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (accentColored) accentGraphics.Add(t);
            return t;
        }

        Image Img(Transform parent, Sprite sprite, Color color, bool accentColored = false)
        {
            var img = NewRect("Image", parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            if (accentColored) accentGraphics.Add(img);
            return img;
        }

        Text MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, bool primary, Action onClick)
        {
            var img = Img(parent, Art.UIPanel, primary ? Color.white : Faded(0.12f), primary);
            Place(img.rectTransform, anchor, Center, pos, size);
            if (onClick != null)
            {
                img.raycastTarget = true;
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => onClick());
            }
            var t = Label(img.transform, label, 48, TextAnchor.MiddleCenter, primary ? Ink : Color.white);
            Stretch(t.rectTransform);
            return t;
        }

        void AddGlow(Text t, float alpha)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectDistance = new Vector2(3f, -3f);
            o.effectColor = Faded(alpha);
            accentOutlines.Add(o);
        }

        static void AddShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectDistance = new Vector2(3f, -3f);
            s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        }
    }
}
