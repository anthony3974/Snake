using System.Collections.Generic;
using UnityEngine;

namespace Snake
{
    /// <summary>
    /// Owns the rules: grid, snake body, food, combos, levels and the game state machine.
    /// Visuals, UI, audio and input are created and driven from here.
    /// </summary>
    public class SnakeGame : MonoBehaviour
    {
        enum State { Ready, Playing, Paused, Resuming, Dying, GameOver }

        class Food
        {
            public Vector2Int cell;
            public FoodView view;
        }

        // Tuning ---------------------------------------------------------------
        const int Cols = 13;
        const int MinRows = 12, MaxRows = 30;
        const int StartLength = 4;
        const float BaseInterval = 0.16f;     // seconds per grid step at level 1
        const float MinInterval = 0.07f;
        const float SpeedUpPerLevel = 0.92f;
        const int BerriesPerLevel = 5;
        const int ComboSteps = 18;            // steps you have to reach the next food to keep a combo
        const int MaxMultiplier = 5;
        const int MaxObstacles = 10;
        const int MaxQueuedTurns = 3;

        const string BestKey = "neon_snake_best";
        const string SoundKey = "neon_snake_sound";
        const string HapticsKey = "neon_snake_haptics";

        // Systems --------------------------------------------------------------
        Camera cam;
        Arena arena;
        SnakeView view;
        Fx fx;
        Sfx sfx;
        GameUI ui;
        SwipeInput input;

        // State ----------------------------------------------------------------
        State state;
        float stateTime;
        readonly List<Vector2Int> body = new List<Vector2Int>();  // [0] is the head
        readonly List<Vector2Int> prev = new List<Vector2Int>();  // body before the last step, for interpolation
        readonly List<Vector2Int> turnQueue = new List<Vector2Int>();
        readonly HashSet<Vector2Int> obstacles = new HashSet<Vector2Int>();
        readonly List<BlockView> blocks = new List<BlockView>();
        readonly List<float> bulges = new List<float>();
        readonly List<Vector2Int> scratchCells = new List<Vector2Int>();
        readonly HashSet<Vector2Int> scratchSet = new HashSet<Vector2Int>();

        Vector2Int dir = Vector2Int.up;
        float stepTimer, interval, interp, headAngle = 90f;
        int pendingGrowth;
        bool graceUsed;
        int score, best, level, berries, combo, comboSteps;
        float playTime, resumeTimer, popTimer;
        int popped, lastCountdown;
        bool firstReady = true, soundOn, hapticsOn, won;
        Food berry;
        Vector2Int lastScreen;
        Rect lastSafe;

        // =====================================================================
        // Setup

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;

            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;

            best = PlayerPrefs.GetInt(BestKey, 0);
            soundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            hapticsOn = PlayerPrefs.GetInt(HapticsKey, 1) == 1;

            arena = new Arena(transform, cam);
            fx = new Fx(transform);
            view = new SnakeView(transform);
            sfx = gameObject.AddComponent<Sfx>();
            sfx.Enabled = soundOn;

            ui = GameUI.Create(cam);
            ui.PauseClicked += PauseGame;
            ui.ResumeClicked += ResumeGame;
            ui.RestartClicked += RestartFromPause;
            ui.SoundClicked += ToggleSound;
            ui.HapticsClicked += ToggleHaptics;
            ui.SetToggles(soundOn, hapticsOn);

            input = new SwipeInput();
            input.Swiped += OnSwipe;
            input.Tapped += OnTap;
            input.BackPressed += OnBack;

            ResetRound();
            CheckLayout();
        }

        void ResetRound()
        {
            foreach (var b in blocks) b.Destroy();
            blocks.Clear();
            obstacles.Clear();
            RemoveFood(ref berry);
            bulges.Clear();
            turnQueue.Clear();

            arena.Build(Cols, ComputeRows());
            arena.Fit(BoardRect());
            arena.SetTheme(0, firstReady);

            int rows = arena.Rows;
            int hx = Cols / 2, hy = Mathf.Max(StartLength, rows / 3);
            body.Clear();
            for (int i = 0; i < StartLength; i++) body.Add(new Vector2Int(hx, hy - i));
            prev.Clear();
            prev.AddRange(body);
            dir = Vector2Int.up;
            headAngle = 90f;

            score = 0;
            level = 1;
            berries = 0;
            combo = 0;
            comboSteps = 0;
            playTime = 0f;
            pendingGrowth = 0;
            graceUsed = false;
            won = false;
            popped = 0;
            interval = IntervalFor(level);
            stepTimer = 0f;
            interp = 0f;

            // First berry sits straight ahead so the first bite is easy.
            berry = CreateFood(new Vector2Int(hx, Mathf.Min(rows - 2, hy + Mathf.Max(4, rows / 3))));

            view.Revive();
            ui.ResetHud(best);
            ui.SetCountdown(0);
            ui.ShowReady(firstReady, best);
            input.Reset();
            SetState(State.Ready);
        }

        // Pixel rect (inside the safe area, below the HUD) that the board must fit in.
        Rect BoardRect()
        {
            Rect safe = Screen.safeArea;
            float s = ui.CanvasScale;
            float top = (GameUI.TopBarHeight + 16f) * s, bottom = 40f * s, side = 34f * s;
            return new Rect(safe.xMin + side, safe.yMin + bottom,
                Mathf.Max(1f, safe.width - 2f * side), Mathf.Max(1f, safe.height - top - bottom));
        }

        int ComputeRows()
        {
            Rect r = BoardRect();
            float cell = r.width / Cols;
            return Mathf.Clamp(Mathf.FloorToInt(r.height / cell), MinRows, MaxRows);
        }

        void CheckLayout()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == lastScreen && Screen.safeArea == lastSafe) return;
            lastScreen = screen;
            lastSafe = Screen.safeArea;

            ui.ApplyLayout();
            if (state == State.Ready && ComputeRows() != arena.Rows) ResetRound();
            arena.Fit(BoardRect());
            input.IgnoreAboveY = Screen.safeArea.yMax - GameUI.TopBarHeight * ui.CanvasScale;
        }

        // =====================================================================
        // Frame loop

        void Update()
        {
            float dt = Time.deltaTime;
            CheckLayout();
            stateTime += dt;
            input.Update();

            switch (state)
            {
                case State.Playing: TickPlaying(dt); break;
                case State.Resuming: TickResuming(dt); break;
                case State.Dying: TickDying(dt); break;
            }

            berry?.view.Tick(dt);
            Theme theme = arena.Current;
            for (int i = 0; i < blocks.Count; i++) blocks[i].Tick(dt, theme);
            arena.Tick(dt);
            RenderSnake(dt);
            fx.Tick(dt);
            cam.transform.position = arena.CameraBase + fx.ShakeOffset;

            ui.SetAccent(arena.Current.accent);
            ui.SetBest(Mathf.Max(best, score));
            ui.SetCombo(combo >= 2 ? Mathf.Min(combo, MaxMultiplier) : 0, ComboFraction());
            ui.Tick(dt);
        }

        void TickPlaying(float dt)
        {
            playTime += dt;

            stepTimer += dt;
            while (stepTimer >= interval && state == State.Playing)
            {
                stepTimer -= interval;
                Step();
            }
            if (state == State.Playing) interp = Mathf.Clamp01(stepTimer / interval);

            // Swallowed food bulges travel towards the tail.
            float advance = dt / interval * 1.6f;
            for (int i = bulges.Count - 1; i >= 0; i--)
            {
                bulges[i] += advance;
                if (bulges[i] > body.Count + 2) bulges.RemoveAt(i);
            }
        }

        void TickResuming(float dt)
        {
            resumeTimer -= dt;
            int n = Mathf.CeilToInt(resumeTimer / 0.5f);
            if (n != lastCountdown)
            {
                lastCountdown = n;
                if (n > 0)
                {
                    ui.SetCountdown(n);
                    sfx.Play(sfx.Beep, 0.6f);
                }
            }
            if (resumeTimer <= 0f)
            {
                ui.SetCountdown(0);
                sfx.Play(sfx.Go, 0.6f);
                SetState(State.Playing);
            }
        }

        void TickDying(float dt)
        {
            if (stateTime < 0.35f) return;
            popTimer += dt;
            float gap = Mathf.Clamp(0.9f / Mathf.Max(1, body.Count), 0.015f, 0.06f);
            while (popped < body.Count && popTimer >= gap)
            {
                popTimer -= gap;
                PopSegment(popped++);
            }
            if (popped >= body.Count && popTimer >= 0.55f) ShowGameOver();
        }

        void RenderSnake(float dt)
        {
            if (state == State.Ready || state == State.Playing || state == State.Resuming)
            {
                float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                headAngle = Mathf.MoveTowardsAngle(headAngle, target, 1000f * dt);
            }

            Theme th = arena.Current;
            var look = new SnakeView.Look
            {
                head = th.head,
                tail = th.tail,
                time = Time.time,
                angle = headAngle,
                hasLookAt = berry != null,
                lookAt = berry != null ? Arena.CellCenter(berry.cell) : Vector2.zero,
                breathe = state == State.Ready ? 1f : 0f,
                hidden = (state == State.Dying || state == State.GameOver) && !won ? popped : 0,
            };
            view.Render(body, prev, interp, look, bulges, dt);
        }

        // =====================================================================
        // Rules

        float IntervalFor(int lvl) => Mathf.Max(MinInterval, BaseInterval * Mathf.Pow(SpeedUpPerLevel, lvl - 1));

        float ComboFraction()
        {
            if (comboSteps <= 0) return 0f;
            float used = state == State.Playing ? interp : 0f;
            return Mathf.Clamp01((comboSteps - used) / ComboSteps);
        }

        void Step()
        {
            while (turnQueue.Count > 0)
            {
                Vector2Int d = turnQueue[0];
                turnQueue.RemoveAt(0);
                if (d != dir && d + dir != Vector2Int.zero)
                {
                    dir = d;
                    break;
                }
            }

            Vector2Int next = body[0] + dir;
            if (IsLethal(next))
            {
                // Mobile-friendly grace: the first time you would crash, the snake stalls for one
                // step ("bonk") so a slightly late swipe still saves you.
                if (!graceUsed)
                {
                    graceUsed = true;
                    prev.Clear();
                    prev.AddRange(body);
                    view.Bonk();
                    sfx.Play(sfx.Bonk, 0.7f);
                    fx.Shake(0.12f);
                    return;
                }
                Die(next);
                return;
            }

            graceUsed = false;
            prev.Clear();
            prev.AddRange(body);
            body.Insert(0, next);
            if (pendingGrowth > 0)
            {
                pendingGrowth--;
                prev.Add(prev[prev.Count - 1]); // new tail segment appears where the old tail was
            }
            else body.RemoveAt(body.Count - 1);

            if (comboSteps > 0 && --comboSteps == 0) combo = 0;

            if (berry != null && next == berry.cell) EatBerry();
        }

        bool IsLethal(Vector2Int c)
        {
            if (c.x < 0 || c.y < 0 || c.x >= Cols || c.y >= arena.Rows) return true;
            if (obstacles.Contains(c)) return true;
            // The tail moves out of the way this step unless we are growing.
            int count = pendingGrowth > 0 ? body.Count : body.Count - 1;
            for (int i = 1; i < count; i++)
                if (body[i] == c) return true;
            return false;
        }

        void QueueTurn(Vector2Int d)
        {
            Vector2Int last = turnQueue.Count > 0 ? turnQueue[turnQueue.Count - 1] : dir;
            if (d == last || d + last == Vector2Int.zero || turnQueue.Count >= MaxQueuedTurns) return;
            turnQueue.Add(d);
        }

        int BumpCombo()
        {
            combo = comboSteps > 0 ? combo + 1 : 1;
            comboSteps = ComboSteps;
            return Mathf.Min(combo, MaxMultiplier);
        }

        void AddScore(int points, Vector2 at, Color color, int multiplier)
        {
            score += points;
            ui.SetScore(score);
            string text = multiplier > 1 ? $"+{points}  x{multiplier}" : $"+{points}";
            ui.Popup(at, text, color, multiplier > 1 ? 1.15f : 1f);
        }

        void EatBerry()
        {
            Vector2 p = Arena.CellCenter(berry.cell);
            RemoveFood(ref berry);
            pendingGrowth++;
            int mult = BumpCombo();
            AddScore((5 + 5 * level) * mult, p, Color.white, mult);
            bulges.Add(0f);
            fx.Burst(p, Palette.Berry, 10, 4.5f, 0.22f, 0.42f);
            fx.Burst(p, Palette.Leaf, 4, 3f, 0.16f, 0.35f);
            fx.Ring(p, Palette.Berry.WithAlpha(0.6f), 0.55f, 0.3f);
            sfx.Play(sfx.Eat, 0.8f, 1f + 0.08f * (mult - 1));
            view.Chomp();

            berries++;
            if (berries % BerriesPerLevel == 0) LevelUp();

            berry = SpawnFood();
            if (berry == null) Win();
        }

        void LevelUp()
        {
            level++;
            interval = IntervalFor(level);
            arena.SetTheme(level - 1, false);
            arena.Pulse(1f);
            ui.SetLevel(level);
            ui.Banner($"LEVEL {level}", Color.white);
            sfx.Play(sfx.Level, 0.7f);

            if (level >= 3)
            {
                int add = level >= 6 ? 2 : 1;
                for (int i = 0; i < add && obstacles.Count < MaxObstacles; i++) AddObstacle();
            }
        }

        void AddObstacle()
        {
            FillBodySet();
            scratchCells.Clear();
            Vector2Int h = body[0];
            for (int x = 1; x < Cols - 1; x++)
                for (int y = 1; y < arena.Rows - 1; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (scratchSet.Contains(c) || obstacles.Contains(c) || IsFoodCell(c)) continue;
                    if (Manhattan(c, h) < 5 || InFront(h, c, 8) || NextToObstacle(c)) continue;
                    scratchCells.Add(c);
                }
            if (scratchCells.Count == 0) return;

            Vector2Int cell = scratchCells[Random.Range(0, scratchCells.Count)];
            obstacles.Add(cell);
            blocks.Add(new BlockView(transform, Arena.CellCenter(cell)));
            fx.Burst(Arena.CellCenter(cell), arena.Current.block, 8, 3.5f, 0.22f, 0.4f);
        }

        bool InFront(Vector2Int head, Vector2Int c, int range)
        {
            Vector2Int r = c - head;
            int along = r.x * dir.x + r.y * dir.y;
            int side = Mathf.Abs(r.x * dir.y - r.y * dir.x);
            return along > 0 && along <= range && side <= 1;
        }

        bool NextToObstacle(Vector2Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && obstacles.Contains(new Vector2Int(c.x + dx, c.y + dy)))
                        return true;
            return false;
        }

        static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        bool IsFoodCell(Vector2Int c) => berry != null && berry.cell == c;

        void FillBodySet()
        {
            scratchSet.Clear();
            for (int i = 0; i < body.Count; i++) scratchSet.Add(body[i]);
        }

        bool TryFindFreeCell(int minHeadDistance, out Vector2Int cell)
        {
            FillBodySet();
            scratchCells.Clear();
            Vector2Int h = body[0];
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < arena.Rows; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (scratchSet.Contains(c) || obstacles.Contains(c) || IsFoodCell(c)) continue;
                    if (Manhattan(c, h) < minHeadDistance) continue;
                    scratchCells.Add(c);
                }
            if (scratchCells.Count == 0)
            {
                cell = default;
                return false;
            }
            cell = scratchCells[Random.Range(0, scratchCells.Count)];
            return true;
        }

        Food SpawnFood()
        {
            if (!TryFindFreeCell(3, out Vector2Int cell) && !TryFindFreeCell(0, out cell)) return null;
            return CreateFood(cell);
        }

        Food CreateFood(Vector2Int cell) => new Food
        {
            cell = cell,
            view = new FoodView(transform, Arena.CellCenter(cell)),
        };

        static void RemoveFood(ref Food food)
        {
            if (food == null) return;
            food.view.Destroy();
            food = null;
        }

        // =====================================================================
        // State changes

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
        }

        void StartPlaying(Vector2Int d)
        {
            Vector2Int towardsNeck = body[1] - body[0];
            if (d == towardsNeck) d = dir;
            dir = d;
            turnQueue.Clear();
            firstReady = false;
            ui.HideReady();
            SetState(State.Playing);
            sfx.Play(sfx.Go, 0.6f);
            stepTimer = 0f;
            Step(); // start moving immediately rather than after one interval
        }

        void Die(Vector2Int into)
        {
            interp = 1f;
            SetState(State.Dying);
            popped = 0;
            popTimer = 0f;
            turnQueue.Clear();

            Vector2 impact = Vector2.Lerp(Arena.CellCenter(body[0]), Arena.CellCenter(into), 0.5f);
            fx.Shake(0.4f);
            fx.Burst(impact, Color.white, 12, 6f, 0.22f, 0.4f);
            fx.Burst(impact, arena.Current.head, 12, 4.5f, 0.24f, 0.5f);
            fx.Ring(impact, Color.white.WithAlpha(0.7f), 0.7f, 0.35f);
            ui.Flash(new Color(0.9f, 0.25f, 0.25f), 0.25f);
            sfx.Play(sfx.Die, 0.9f);
            view.SetDead();
            if (hapticsOn) Haptics.Vibrate();
        }

        void PopSegment(int i)
        {
            Vector2 p = Arena.CellCenter(body[i]);
            float f = body.Count > 1 ? i / (body.Count - 1f) : 0f;
            fx.Burst(p, Color.Lerp(arena.Current.head, arena.Current.tail, f), 4, 3f, 0.22f, 0.4f);
            if ((i & 1) == 0) sfx.Play(sfx.Pop, 0.3f, Mathf.Min(2.2f, 0.8f + i * 0.025f));
        }

        void Win()
        {
            won = true;
            interp = 1f;
            SetState(State.Dying);
            popped = body.Count;
            popTimer = 0f;
            ui.Banner("PERFECT!", Palette.Gold);
            fx.Confetti(new Vector2(Cols * 0.5f, arena.Rows * 0.5f), new[] { Palette.Gold, Palette.Berry, arena.Current.head }, 80);
            sfx.Play(sfx.Level);
        }

        void ShowGameOver()
        {
            bool newBest = score > best;
            if (newBest)
            {
                best = score;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
            SetState(State.GameOver);
            input.Reset();
            ui.ShowGameOver(won, score, best, newBest, body.Count, level, playTime);
            if (newBest)
            {
                var colors = new[] { Palette.Gold, Palette.Berry, arena.Current.head, arena.Current.tail };
                fx.Confetti(new Vector2(Cols * 0.5f, arena.Rows * 0.55f), colors, 70);
                sfx.Play(sfx.Level, 0.7f);
            }
        }

        void PauseGame()
        {
            if (state != State.Playing && state != State.Resuming) return;
            SetState(State.Paused);
            input.Reset();
            ui.SetCountdown(0);
            ui.ShowPause();
            sfx.Play(sfx.Click, 0.6f);
        }

        void ResumeGame()
        {
            if (state != State.Paused) return;
            ui.HidePause();
            turnQueue.Clear();
            resumeTimer = 1.5f;
            lastCountdown = -1;
            SetState(State.Resuming);
            input.Reset();
            sfx.Play(sfx.Click, 0.6f);
        }

        void RestartFromPause()
        {
            if (state != State.Paused) return;
            ui.HidePause();
            sfx.Play(sfx.Click, 0.6f);
            ResetRound();
        }

        void ToggleSound()
        {
            soundOn = !soundOn;
            sfx.Enabled = soundOn;
            PlayerPrefs.SetInt(SoundKey, soundOn ? 1 : 0);
            ui.SetToggles(soundOn, hapticsOn);
            sfx.Play(sfx.Click, 0.6f);
        }

        void ToggleHaptics()
        {
            hapticsOn = !hapticsOn;
            PlayerPrefs.SetInt(HapticsKey, hapticsOn ? 1 : 0);
            ui.SetToggles(soundOn, hapticsOn);
            sfx.Play(sfx.Click, 0.6f);
            if (hapticsOn) Haptics.Vibrate();
        }

        // =====================================================================
        // Input

        void OnSwipe(Vector2Int d)
        {
            switch (state)
            {
                case State.Ready:
                    if (stateTime >= 0.15f) StartPlaying(d);
                    break;
                case State.Playing:
                case State.Resuming:
                    QueueTurn(d);
                    break;
            }
        }

        void OnTap()
        {
            switch (state)
            {
                case State.Ready:
                    if (stateTime >= 0.15f) StartPlaying(dir);
                    break;
                case State.GameOver:
                    if (stateTime >= 0.7f)
                    {
                        ui.HideGameOver();
                        sfx.Play(sfx.Click, 0.6f);
                        ResetRound();
                    }
                    break;
            }
        }

        void OnBack()
        {
            if (state == State.Playing || state == State.Resuming) PauseGame();
            else if (state == State.Paused) ResumeGame();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && ui != null) PauseGame();
        }
    }
}
