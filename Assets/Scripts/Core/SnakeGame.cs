using System.Collections.Generic;
using UnityEngine;

namespace NeonSnake
{
    public enum FoodKind { Berry, Star, Ghost, Clock, Shrink }

    /// <summary>
    /// Owns the rules: grid, snake body, food, power-ups, combos, levels and the game state machine.
    /// Visuals, UI, audio and input are created and driven from here.
    /// </summary>
    public class SnakeGame : MonoBehaviour
    {
        enum State { Ready, Playing, Paused, Resuming, Dying, GameOver }

        class Food
        {
            public FoodKind kind;
            public Vector2Int cell;
            public float life, maxLife;
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
        const float GhostDuration = 7f;
        const float SlowDuration = 6f;
        const float SlowFactor = 1.6f;
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
        float ghostTimer, slowTimer, playTime, resumeTimer, popTimer, sparkleTimer;
        int popped, lastCountdown;
        bool firstReady = true, soundOn, hapticsOn, won;
        Food berry, special;
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
            PostFx.Setup(cam, transform);

            best = PlayerPrefs.GetInt(BestKey, 0);
            soundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            hapticsOn = PlayerPrefs.GetInt(HapticsKey, 1) == 1;

            arena = new Arena(transform, cam);
            fx = new Fx(transform, cam);
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
            RemoveFood(ref special);
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
            ghostTimer = slowTimer = playTime = 0f;
            pendingGrowth = 0;
            graceUsed = false;
            won = false;
            popped = 0;
            interval = IntervalFor(level);
            stepTimer = 0f;
            interp = 0f;

            // First berry sits straight ahead so the first bite is easy.
            berry = CreateFood(FoodKind.Berry, new Vector2Int(hx, Mathf.Min(rows - 2, hy + Mathf.Max(4, rows / 3))));

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

            TickFood(dt);
            Theme theme = arena.Current;
            for (int i = 0; i < blocks.Count; i++) blocks[i].Tick(dt, theme);
            arena.Tick(dt, slowTimer > 0f ? Mathf.Clamp01(slowTimer / 0.6f) : 0f);
            RenderSnake(dt);
            fx.Tick(dt, arena.Current.frame);
            cam.transform.position = arena.CameraBase + fx.ShakeOffset;

            ui.SetAccent(arena.Current.head);
            ui.SetBest(Mathf.Max(best, score));
            ui.SetCombo(combo >= 2 ? Mathf.Min(combo, MaxMultiplier) : 0, ComboFraction());
            ui.SetPowers(ghostTimer / GhostDuration, slowTimer / SlowDuration);
            ui.Tick(dt);
        }

        void TickPlaying(float dt)
        {
            playTime += dt;
            if (ghostTimer > 0f)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0f;
                    sfx.Play(sfx.Expire, 0.6f);
                }
            }
            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                if (slowTimer <= 0f)
                {
                    slowTimer = 0f;
                    sfx.Play(sfx.Expire, 0.6f, 0.8f);
                }
            }

            float step = StepInterval();
            stepTimer += dt;
            while (stepTimer >= step && state == State.Playing)
            {
                stepTimer -= step;
                Step();
                step = StepInterval();
            }
            if (state == State.Playing) interp = Mathf.Clamp01(stepTimer / step);

            // Swallowed food bulges travel towards the tail.
            float advance = dt / step * 1.6f;
            for (int i = bulges.Count - 1; i >= 0; i--)
            {
                bulges[i] += advance;
                if (bulges[i] > body.Count + 2) bulges.RemoveAt(i);
            }

            // Rainbow sparkles while on a big combo.
            if (combo >= 3 && state == State.Playing)
            {
                sparkleTimer -= dt;
                if (sparkleTimer <= 0f)
                {
                    sparkleTimer = 0.05f;
                    Color c = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.7f, 1f), 0.55f, 1f);
                    fx.Burst(view.HeadPosition, c, 1, 1.5f, 0.22f, 0.5f);
                }
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

        void TickFood(float dt)
        {
            berry?.view.Tick(dt, -1f);
            if (special == null) return;
            if (state == State.Playing)
            {
                special.life -= dt;
                if (special.life <= 0f)
                {
                    fx.Burst(Arena.CellCenter(special.cell), Palette.For(special.kind), 10, 3f, 0.25f, 0.4f);
                    RemoveFood(ref special);
                    return;
                }
            }
            special.view.Tick(dt, special.life / special.maxLife);
        }

        void RenderSnake(float dt)
        {
            if (state == State.Ready || state == State.Playing || state == State.Resuming)
            {
                float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                headAngle = Mathf.MoveTowardsAngle(headAngle, target, 1000f * dt);
            }

            Theme th = arena.Current;
            Food focus = special ?? berry;
            float ghost = 0f;
            if (ghostTimer > 0f)
                ghost = ghostTimer > 1.5f ? 1f : (Mathf.Sin(Time.time * 28f) > 0f ? 1f : 0.25f); // flicker = about to end

            var look = new SnakeView.Look
            {
                head = th.head,
                tail = th.tail,
                time = Time.time,
                angle = headAngle,
                hasLookAt = focus != null,
                lookAt = focus != null ? Arena.CellCenter(focus.cell) : Vector2.zero,
                ghost = ghost,
                rainbow = combo >= 3 ? 1f : 0f,
                breathe = state == State.Ready ? 1f : 0f,
                hidden = (state == State.Dying || state == State.GameOver) && !won ? popped : 0,
            };
            view.Render(body, prev, interp, look, bulges, dt);
        }

        // =====================================================================
        // Rules

        float IntervalFor(int lvl) => Mathf.Max(MinInterval, BaseInterval * Mathf.Pow(SpeedUpPerLevel, lvl - 1));

        float StepInterval() => interval * (slowTimer > 0f ? SlowFactor : 1f);

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
            else if (special != null && next == special.cell) EatSpecial();
        }

        bool IsLethal(Vector2Int c)
        {
            if (c.x < 0 || c.y < 0 || c.x >= Cols || c.y >= arena.Rows) return true;
            if (obstacles.Contains(c)) return true;
            if (ghostTimer > 0f) return false;
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
            AddScore((5 + 5 * level) * mult, p, Palette.Berry, mult);
            bulges.Add(0f);
            fx.Burst(p, Palette.Berry, 14, 5f, 0.3f, 0.5f);
            fx.Burst(p, arena.Current.head, 6, 3f, 0.22f, 0.4f);
            fx.Ring(p, Palette.Berry, 0.6f, 0.35f);
            sfx.Play(sfx.Eat, 0.8f, 1f + 0.08f * (mult - 1));
            view.Chomp();

            berries++;
            if (berries % BerriesPerLevel == 0) LevelUp();

            berry = SpawnFood(FoodKind.Berry);
            if (berry == null)
            {
                Win();
                return;
            }
            MaybeSpawnSpecial();
        }

        void EatSpecial()
        {
            Vector2 p = Arena.CellCenter(special.cell);
            FoodKind kind = special.kind;
            RemoveFood(ref special);
            Color c = Palette.For(kind);
            fx.Burst(p, c, 22, 6.5f, 0.32f, 0.6f);
            fx.Ring(p, c, 0.8f, 0.45f);
            view.Chomp();

            switch (kind)
            {
                case FoodKind.Star:
                {
                    int mult = BumpCombo();
                    AddScore(50 * mult, p, c, mult);
                    fx.Burst(p, Color.white, 10, 8f, 0.2f, 0.4f);
                    sfx.Play(sfx.Star, 0.8f);
                    break;
                }
                case FoodKind.Ghost:
                    ghostTimer = GhostDuration;
                    AddScore(20, p, c, 1);
                    ui.Banner("GHOST MODE", c);
                    sfx.Play(sfx.Power, 0.8f);
                    break;
                case FoodKind.Clock:
                    slowTimer = SlowDuration;
                    AddScore(20, p, c, 1);
                    ui.Banner("SLOW-MO", c);
                    sfx.Play(sfx.Power, 0.8f, 0.75f);
                    break;
                case FoodKind.Shrink:
                    ShrinkTail(4);
                    AddScore(30, p, c, 1);
                    ui.Banner("SHRINK!", c);
                    sfx.Play(sfx.Shrink, 0.8f);
                    break;
            }
        }

        void ShrinkTail(int amount)
        {
            int remove = Mathf.Min(amount, body.Count - StartLength);
            for (int i = 0; i < remove; i++)
            {
                fx.Burst(Arena.CellCenter(body[body.Count - 1]), arena.Current.tail, 6, 3f, 0.28f, 0.45f);
                body.RemoveAt(body.Count - 1);
                prev.RemoveAt(prev.Count - 1);
            }
            for (int i = bulges.Count - 1; i >= 0; i--)
                if (bulges[i] > body.Count) bulges.RemoveAt(i);
        }

        void LevelUp()
        {
            level++;
            interval = IntervalFor(level);
            arena.SetTheme(level - 1, false);
            arena.Pulse(1f);
            ui.SetLevel(level);
            ui.Banner($"LEVEL {level}", Theme.All[(level - 1) % Theme.All.Length].head);
            sfx.Play(sfx.Level, 0.7f);

            if (level >= 3)
            {
                int add = level >= 6 ? 2 : 1;
                for (int i = 0; i < add && obstacles.Count < MaxObstacles; i++) AddObstacle();
            }
        }

        void MaybeSpawnSpecial()
        {
            if (special != null) return;
            float r = Random.value;
            FoodKind kind;
            if (r < 0.16f) kind = FoodKind.Star;
            else if (level >= 2 && r < 0.24f) kind = FoodKind.Ghost;
            else if (level >= 2 && r < 0.32f) kind = FoodKind.Clock;
            else if (body.Count >= 10 && r < 0.39f) kind = FoodKind.Shrink;
            else return;

            special = SpawnFood(kind);
            if (special == null) return;
            special.life = special.maxLife = kind == FoodKind.Star ? 6f : 8f;
            sfx.Play(sfx.Spawn, 0.6f);
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
            fx.Burst(Arena.CellCenter(cell), arena.Current.frame, 12, 4f, 0.3f, 0.5f);
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

        bool IsFoodCell(Vector2Int c) => (berry != null && berry.cell == c) || (special != null && special.cell == c);

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

        Food SpawnFood(FoodKind kind)
        {
            if (!TryFindFreeCell(3, out Vector2Int cell) && !TryFindFreeCell(0, out cell)) return null;
            return CreateFood(kind, cell);
        }

        Food CreateFood(FoodKind kind, Vector2Int cell) => new Food
        {
            kind = kind,
            cell = cell,
            life = -1f,
            maxLife = -1f,
            view = new FoodView(kind, transform, Arena.CellCenter(cell)),
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
            fx.Shake(0.45f);
            fx.Burst(impact, Color.white, 20, 8f, 0.3f, 0.5f);
            fx.Burst(impact, arena.Current.head, 16, 5f, 0.35f, 0.6f);
            fx.Ring(impact, Color.white, 0.8f, 0.45f);
            ui.Flash(new Color(1f, 0.35f, 0.45f), 0.4f);
            sfx.Play(sfx.Die, 0.9f);
            view.SetDead();
            if (hapticsOn) Haptics.Vibrate();
        }

        void PopSegment(int i)
        {
            Vector2 p = Arena.CellCenter(body[i]);
            float f = body.Count > 1 ? i / (body.Count - 1f) : 0f;
            fx.Burst(p, Color.Lerp(arena.Current.head, arena.Current.tail, f), 5, 3.5f, 0.3f, 0.5f);
            if ((i & 1) == 0) sfx.Play(sfx.Pop, 0.3f, Mathf.Min(2.2f, 0.8f + i * 0.025f));
        }

        void Win()
        {
            won = true;
            interp = 1f;
            SetState(State.Dying);
            popped = body.Count;
            popTimer = 0f;
            ui.Banner("PERFECT!", Palette.Star);
            fx.Confetti(new Vector2(Cols * 0.5f, arena.Rows * 0.5f), new[] { Palette.Star, Palette.Berry, arena.Current.head }, 80);
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
                var colors = new[] { Palette.Star, Palette.Berry, Palette.Ghost, arena.Current.head };
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
