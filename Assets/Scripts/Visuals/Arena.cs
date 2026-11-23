using UnityEngine;

namespace Snake
{
    /// <summary>
    /// The board: checkered floor, crisp frame, theme colours and camera framing.
    /// Board cell (x, y) covers world space [x, x+1] x [y, y+1].
    /// </summary>
    public class Arena
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public Vector3 CameraBase { get; private set; } = new Vector3(0f, 0f, -10f);
        public Theme Current => current;

        readonly Camera cam;
        readonly SpriteRenderer board, frame;
        Texture2D checkerTex;
        Sprite checkerSprite;
        Theme from, to, current;
        float themeT = 1f, pulse;

        public static Vector2 CellCenter(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        public Arena(Transform parent, Camera cam)
        {
            this.cam = cam;
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            board = NewRenderer("Board", root, null, -50);
            frame = NewRenderer("Frame", root, Art.Frame, -47);
            frame.drawMode = SpriteDrawMode.Sliced;

            current = from = to = Theme.All[0];
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Build(int cols, int rows)
        {
            if (cols != Cols || rows != Rows || checkerTex == null)
            {
                Cols = cols;
                Rows = rows;
                if (checkerSprite != null) Object.Destroy(checkerSprite);
                if (checkerTex != null) Object.Destroy(checkerTex);
                checkerTex = Art.Checker(cols, rows);
                checkerSprite = Sprite.Create(checkerTex, new Rect(0, 0, cols, rows), Vector2.zero, 1f, 0,
                    SpriteMeshType.FullRect);
                board.sprite = checkerSprite;
            }

            var center = new Vector3(cols * 0.5f, rows * 0.5f, 0f);
            frame.transform.localPosition = center;
            frame.size = new Vector2(cols + 0.32f, rows + 0.32f);
        }

        /// <summary>Fits the board inside a pixel rect of the screen (safe area minus HUD).</summary>
        public void Fit(Rect px)
        {
            if (Cols == 0 || Rows == 0) return;
            float ppu = Mathf.Max(1f, Mathf.Min(px.width / Cols, px.height / Rows));
            cam.orthographicSize = Screen.height / (2f * ppu);
            var boardCenter = new Vector2(Cols * 0.5f, Rows * 0.5f);
            Vector2 offset = (px.center - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) / ppu;
            CameraBase = new Vector3(boardCenter.x - offset.x, boardCenter.y - offset.y, -10f);
        }

        public void SetTheme(int index, bool instant)
        {
            from = current;
            to = Theme.All[index % Theme.All.Length];
            themeT = instant ? 1f : 0f;
            if (instant) current = to;
        }

        /// <summary>Briefly brightens the frame on level up.</summary>
        public void Pulse(float amount) => pulse = Mathf.Max(pulse, amount);

        public void Tick(float dt)
        {
            if (themeT < 1f)
            {
                themeT = Mathf.Min(1f, themeT + dt / 1.0f);
                current = Theme.Lerp(from, to, Ease.Smooth(themeT));
            }
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 1.5f);

            cam.backgroundColor = current.bg;
            board.color = current.board;
            frame.color = Color.Lerp(current.frame, Color.white, pulse * 0.25f);
        }
    }
}
