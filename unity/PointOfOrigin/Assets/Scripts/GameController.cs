using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointOfOrigin
{
    /// <summary>
    /// The whole game. Loads levels.json, drives the Odin simulation through
    /// Sim, paints the grid into one point-filtered texture on a sprite, and
    /// draws the HUD with IMGUI. Clicks are resolved in Update so a button
    /// never fires twice across IMGUI's layout and repaint passes.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        enum Phase { Title, Place, Growing, Result, Finished }

        const int CellPx = 8;              // texture pixels per cell, the last row/column is the gap
        const float StepInterval = 0.22f;  // seconds per generation while growing
        const float WinThreshold = 0.999f;
        const int RevealAfter = 2;         // failed attempts before Reveal is offered

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static readonly Color32 ColBg = Hex("0a0c12");
        static readonly Color32 ColGap = Hex("05060a");
        static readonly Color32 ColOpen = Hex("141824");
        static readonly Color32 ColOpenEdge = Hex("181d2b");
        static readonly Color32 ColRock = Hex("343a47");
        static readonly Color32 ColRockEdge = Hex("222733");
        static readonly Color32 ColGhost = Hex("2f7d84");
        static readonly Color32 ColSeed = Hex("6fe3ff");
        static readonly Color32 ColSeedEdge = Hex("b9f3ff");
        static readonly Color32 ColWrong = Hex("b8364a");
        static readonly Color32 ColWrongEdge = Hex("e0566a");
        static readonly Color32 ColReveal = Hex("ff5fd2");
        static readonly Color32[] AgeRamp =
        {
            Hex("fff7de"), Hex("ffd166"), Hex("f4a259"), Hex("e76f51"), Hex("c0503f"), Hex("9a3f3a"),
        };

        struct Button
        {
            public Rect rect;
            public string label;
            public Action act;
            public bool enabled;
        }

        LevelSet set;
        Level level;
        int levelIndex;
        Sim sim;
        byte[] rock;
        byte[] target;
        readonly List<Vector2Int> seeds = new List<Vector2Int>();
        HashSet<Vector2Int> answer = new HashSet<Vector2Int>();

        Texture2D tex;
        Color32[] px;
        SpriteRenderer sr;
        Camera cam;
        Sfx sfx;

        Phase phase = Phase.Title;
        float stepTimer;
        float resultTime;
        float match;
        bool won;
        int attempts;
        bool revealed;
        int solved;
        int skipped;
        Vector2Int hover = new Vector2Int(-1, -1);
        string fatal;

        readonly List<Button> buttons = new List<Button>();
        GUIStyle stTitle, stH1, stH1Right, stSmall, stSmallRight, stHint, stButton, stBanner, stBody;
        Texture2D panelTex;
        int styledHeight;

        bool CanReveal => attempts >= RevealAfter && !revealed && (phase == Phase.Place || phase == Phase.Result);

        // ------------------------------------------------------------------ setup

        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            sfx = GetComponent<Sfx>() ?? gameObject.AddComponent<Sfx>();

            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColBg;
            cam.transform.position = new Vector3(0f, -0.4f, -10f);
            // The template's volume profile asks for bloom; the game is flat colour and the player build strips the shader.
            var extra = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extra != null) extra.renderPostProcessing = false;

            var grid = new GameObject("Grid");
            sr = grid.AddComponent<SpriteRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader != null) sr.sharedMaterial = new Material(shader);

            try
            {
                Debug.Log($"Point of Origin: origin_sim.dll version {Sim.NativeVersion}");
                set = Levels.Load();
                LoadLevel(0);
            }
            catch (Exception e)
            {
                fatal = e.Message;
                Debug.LogException(e);
                return;
            }
            phase = Phase.Title;
        }

        void OnDestroy()
        {
            sim?.Dispose();
            sim = null;
        }

        void LoadLevel(int index)
        {
            levelIndex = index;
            level = set.levels[index];
            sim?.Dispose();
            sim = new Sim(level.w, level.h);
            sim.SetRule((uint)level.birth, (uint)level.survive);
            rock = level.RockCells();
            target = level.TargetCells();
            sim.Load(rock);
            seeds.Clear();
            answer = new HashSet<Vector2Int>(level.OriginCells());
            attempts = 0;
            revealed = false;
            won = false;
            match = 0f;
            stepTimer = 0f;

            int tw = level.w * CellPx, th = level.h * CellPx;
            if (tex == null || tex.width != tw || tex.height != th)
            {
                if (sr.sprite != null) Destroy(sr.sprite);
                if (tex != null) Destroy(tex);
                tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                px = new Color32[tw * th];
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), CellPx);
            }
            FitCamera();
            phase = Phase.Place;
            Paint();
        }

        void FitCamera()
        {
            if (level == null || cam == null) return;
            float margin = 2.6f;
            float need = Mathf.Max(level.h / 2f + margin, (level.w / 2f + margin) / Mathf.Max(0.1f, cam.aspect));
            cam.orthographicSize = need;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (fatal != null || sim == null) return;
            FitCamera();

            var mouse = InputBridge.MousePosition;
            var gui = new Vector2(mouse.x, Screen.height - mouse.y);
            hover = phase == Phase.Place ? CellAt(mouse) : new Vector2Int(-1, -1);
            Layout();

            if (InputBridge.Clicked)
            {
                bool hit = false;
                foreach (var b in buttons)
                {
                    if (!b.enabled || !b.rect.Contains(gui)) continue;
                    b.act();
                    hit = true;
                    break;
                }
                if (!hit)
                {
                    switch (phase)
                    {
                        case Phase.Title: Begin(); break;
                        case Phase.Place: if (hover.x >= 0) ToggleSeed(hover); break;
                        case Phase.Result: if (won) Next(); else Rewind(); break;
                        case Phase.Finished: Restart(); break;
                    }
                }
            }
            if (InputBridge.RightClicked && phase == Phase.Place && hover.x >= 0 && seeds.Contains(hover))
                ToggleSeed(hover);

            if (InputBridge.Pressed(Key.Space, KeyCode.Space) || InputBridge.Pressed(Key.Enter, KeyCode.Return)) Primary();
            if (InputBridge.Pressed(Key.R, KeyCode.R) && phase != Phase.Title && phase != Phase.Finished) Rewind();
            if (InputBridge.Pressed(Key.N, KeyCode.N) && (phase == Phase.Place || phase == Phase.Result)) Skip();
            if (InputBridge.Pressed(Key.V, KeyCode.V) && CanReveal) Reveal();
            if (InputBridge.Pressed(Key.Escape, KeyCode.Escape) && !Application.isEditor) Application.Quit();

            if (phase == Phase.Growing)
            {
                stepTimer += Time.deltaTime;
                while (stepTimer >= StepInterval && phase == Phase.Growing)
                {
                    stepTimer -= StepInterval;
                    Advance();
                }
            }
            if (phase == Phase.Result) resultTime += Time.deltaTime;
            Paint();
        }

        Vector2Int CellAt(Vector2 screen)
        {
            var wp = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            int x = Mathf.FloorToInt(wp.x + level.w / 2f);
            int fromBottom = Mathf.FloorToInt(wp.y + level.h / 2f);
            int y = level.h - 1 - fromBottom;
            if (x < 0 || y < 0 || x >= level.w || y >= level.h) return new Vector2Int(-1, -1);
            return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------ actions

        void Primary()
        {
            switch (phase)
            {
                case Phase.Title: Begin(); break;
                case Phase.Place: Grow(); break;
                case Phase.Growing: while (phase == Phase.Growing) Advance(); break;
                case Phase.Result: if (won) Next(); else Rewind(); break;
                case Phase.Finished: Restart(); break;
            }
        }

        void Begin()
        {
            phase = Phase.Place;
        }

        void ToggleSeed(Vector2Int c)
        {
            int i = c.y * level.w + c.x;
            if (rock[i] == Sim.Rock) return;
            if (seeds.Remove(c))
            {
                sim.Set(c.x, c.y, Sim.Dead);
                sfx.Remove();
                return;
            }
            if (seeds.Count >= level.seeds)
            {
                var oldest = seeds[0];
                seeds.RemoveAt(0);
                sim.Set(oldest.x, oldest.y, Sim.Dead);
            }
            seeds.Add(c);
            sim.Set(c.x, c.y, Sim.Alive);
            sfx.Place();
        }

        void Grow()
        {
            if (seeds.Count == 0) return;
            attempts++;
            stepTimer = 0f;
            phase = Phase.Growing;
        }

        void Advance()
        {
            int gen = sim.Step(1);
            sfx.Tick(gen);
            if (gen >= level.steps) Finish();
        }

        void Finish()
        {
            match = sim.Compare(target);
            won = match >= WinThreshold;
            phase = Phase.Result;
            resultTime = 0f;
            if (won)
            {
                solved++;
                sfx.Success();
            }
            else
            {
                sfx.Fail();
            }
        }

        void Rewind()
        {
            sim.ClearLife();
            foreach (var s in seeds) sim.Set(s.x, s.y, Sim.Alive);
            stepTimer = 0f;
            phase = Phase.Place;
        }

        void Skip()
        {
            skipped++;
            Next();
        }

        void Reveal()
        {
            revealed = true;
            sfx.Reveal();
        }

        void Next()
        {
            if (levelIndex + 1 >= set.levels.Length)
            {
                phase = Phase.Finished;
                return;
            }
            LoadLevel(levelIndex + 1);
        }

        void Restart()
        {
            solved = 0;
            skipped = 0;
            LoadLevel(0);
        }

        // ------------------------------------------------------------------ painting

        static Color32 AgeColor(int age)
        {
            return AgeRamp[Mathf.Clamp(age - 1, 0, AgeRamp.Length - 1)];
        }

        static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, 255), t);

        void Paint()
        {
            if (sim == null || tex == null) return;
            sim.Refresh();
            for (int i = 0; i < px.Length; i++) px[i] = ColGap;

            bool placing = phase == Phase.Place || phase == Phase.Title;
            for (int y = 0; y < level.h; y++)
            {
                for (int x = 0; x < level.w; x++)
                {
                    int i = y * level.w + x;
                    byte c = sim.Cells[i];
                    bool inTarget = target[i] == Sim.Alive;
                    bool isRock = rock[i] == Sim.Rock;
                    Color32 fill, edge;
                    if (isRock)
                    {
                        fill = ColRock;
                        edge = ColRockEdge;
                    }
                    else if (c == Sim.Alive)
                    {
                        if (placing)
                        {
                            fill = ColSeed;
                            edge = ColSeedEdge;
                        }
                        else if (inTarget)
                        {
                            fill = AgeColor(sim.Ages[i]);
                            edge = Lighten(fill, 0.35f);
                        }
                        else
                        {
                            fill = ColWrong;
                            edge = ColWrongEdge;
                        }
                    }
                    else if (inTarget)
                    {
                        fill = ColOpen;
                        edge = ColGhost;
                    }
                    else
                    {
                        fill = ColOpen;
                        edge = ColOpenEdge;
                    }
                    if (!isRock && hover.x == x && hover.y == y)
                    {
                        fill = Lighten(fill, 0.18f);
                        edge = Lighten(edge, 0.25f);
                    }
                    PaintCell(x, y, fill, edge);
                    if (revealed && answer.Contains(new Vector2Int(x, y))) PaintDot(x, y, ColReveal);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        void PaintCell(int x, int y, Color32 fill, Color32 edge)
        {
            int tw = tex.width;
            int x0 = x * CellPx;
            int y0 = (level.h - 1 - y) * CellPx;
            int inner = CellPx - 1; // the last row and column stay the gap colour
            for (int dy = 0; dy < inner; dy++)
            {
                int row = (y0 + dy) * tw + x0;
                for (int dx = 0; dx < inner; dx++)
                {
                    bool border = dx == 0 || dy == 0 || dx == inner - 1 || dy == inner - 1;
                    px[row + dx] = border ? edge : fill;
                }
            }
        }

        void PaintDot(int x, int y, Color32 color)
        {
            int tw = tex.width;
            int x0 = x * CellPx + 2;
            int y0 = (level.h - 1 - y) * CellPx + 2;
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 3; dx++)
                    px[(y0 + dy) * tw + x0 + dx] = color;
        }

        // ------------------------------------------------------------------ HUD

        float S => Screen.height / 800f;

        void Layout()
        {
            buttons.Clear();
            float s = S;
            float w = 168f * s, h = 46f * s, gap = 12f * s;
            float x = Screen.width - 24f * s - w;
            float y = Screen.height - 24f * s - h;

            void Add(string label, Action act, bool enabled = true)
            {
                buttons.Add(new Button { rect = new Rect(x, y, w, h), label = label, act = act, enabled = enabled });
                x -= w + gap;
            }

            switch (phase)
            {
                case Phase.Place:
                    Add("Grow  [Space]", Grow, seeds.Count > 0);
                    Add("Skip  [N]", Skip);
                    if (CanReveal) Add("Reveal  [V]", Reveal);
                    break;
                case Phase.Growing:
                    Add("Finish  [Space]", Primary);
                    Add("Rewind  [R]", Rewind);
                    break;
                case Phase.Result:
                    if (won)
                    {
                        Add("Next  [Space]", Next);
                    }
                    else
                    {
                        Add("Rewind  [R]", Rewind);
                        Add("Skip  [N]", Skip);
                        if (CanReveal) Add("Reveal  [V]", Reveal);
                    }
                    break;
            }
        }

        void EnsureStyles()
        {
            if (stTitle != null && styledHeight == Screen.height) return;
            styledHeight = Screen.height;
            float s = S;

            if (panelTex == null)
            {
                panelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                panelTex.SetPixel(0, 0, Color.white);
                panelTex.Apply();
            }

            GUIStyle Make(int size, FontStyle style, TextAnchor anchor, string color)
            {
                var st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, (int)(size * s)),
                    fontStyle = style,
                    alignment = anchor,
                    wordWrap = true,
                    richText = false,
                };
                st.normal.textColor = Hex(color);
                return st;
            }

            stTitle = Make(64, FontStyle.Bold, TextAnchor.MiddleCenter, "fff7de");
            stBanner = Make(44, FontStyle.Bold, TextAnchor.MiddleCenter, "fff7de");
            stH1 = Make(26, FontStyle.Bold, TextAnchor.UpperLeft, "e8ecf5");
            stH1Right = Make(26, FontStyle.Bold, TextAnchor.UpperRight, "e8ecf5");
            stSmall = Make(16, FontStyle.Normal, TextAnchor.UpperLeft, "8a93a8");
            stSmallRight = Make(16, FontStyle.Normal, TextAnchor.UpperRight, "8a93a8");
            stHint = Make(18, FontStyle.Italic, TextAnchor.LowerLeft, "b7bfd1");
            stBody = Make(20, FontStyle.Normal, TextAnchor.MiddleCenter, "b7bfd1");
            stButton = Make(19, FontStyle.Bold, TextAnchor.MiddleCenter, "e8ecf5");
        }

        void Panel(Rect r, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, panelTex);
            GUI.color = old;
        }

        void OnGUI()
        {
            EnsureStyles();
            float s = S;
            var mouse = InputBridge.MousePosition;
            var gui = new Vector2(mouse.x, Screen.height - mouse.y);

            if (fatal != null)
            {
                Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 1f));
                GUI.Label(new Rect(40f * s, 40f * s, Screen.width - 80f * s, Screen.height - 80f * s),
                    "Point of Origin could not start.\n\n" + fatal, stBody);
                return;
            }
            if (level == null) return;

            // top left: level and law
            GUI.Label(new Rect(24f * s, 16f * s, Screen.width * 0.6f, 40f * s),
                $"{levelIndex + 1:00} / {set.levels.Length:00}    {level.name}", stH1);
            GUI.Label(new Rect(24f * s, 54f * s, Screen.width * 0.6f, 30f * s),
                $"Law {level.rule}     {level.steps} generation{(level.steps == 1 ? "" : "s")}", stSmall);

            // top right: seeds or generation
            string right = phase == Phase.Place || phase == Phase.Title
                ? $"Seeds {seeds.Count} / {level.seeds}"
                : $"Generation {sim.Generation} / {level.steps}";
            GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 16f * s, Screen.width * 0.6f, 40f * s), right, stH1Right);
            string sub = attempts > 0 ? $"attempt {attempts}" : "click a cell to plant a seed";
            GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 54f * s, Screen.width * 0.6f, 30f * s), sub, stSmallRight);

            // bottom left: the hint
            float buttonsWidth = buttons.Count * 180f * s + 40f * s;
            GUI.Label(new Rect(24f * s, Screen.height - 110f * s, Screen.width - buttonsWidth - 48f * s, 90f * s), level.hint, stHint);

            foreach (var b in buttons)
            {
                bool hot = b.enabled && b.rect.Contains(gui);
                Panel(b.rect, b.enabled ? (hot ? new Color(0.30f, 0.36f, 0.48f, 0.95f) : new Color(0.18f, 0.21f, 0.29f, 0.92f)) : new Color(0.12f, 0.13f, 0.17f, 0.7f));
                var old = GUI.color;
                GUI.color = b.enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                GUI.Label(b.rect, b.label, stButton);
                GUI.color = old;
            }

            switch (phase)
            {
                case Phase.Title:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 0.86f));
                    float cy = Screen.height * 0.36f;
                    GUI.Label(new Rect(0, cy - 60f * s, Screen.width, 100f * s), "POINT OF ORIGIN", stTitle);
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 40f * s, Screen.width * 0.7f, 80f * s),
                        "You are shown how it ended. Find where it began.", stBody);
                    GUI.Label(new Rect(Screen.width * 0.1f, cy + 120f * s, Screen.width * 0.8f, 120f * s),
                        "Every pattern grew from one or two seeds under a simple law.\n" +
                        "Plant your seeds, press Grow, and match the ghost outline exactly.", stBody);
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Space to begin", stBody);
                    GUI.color = old;
                    GUI.Label(new Rect(24f * s, Screen.height - 60f * s, Screen.width - 48f * s, 30f * s),
                        "made for CPGD's World's First Game Jam, theme ORIGIN   |   Odin + Nexium + Unity", stSmallRight);
                    break;
                }
                case Phase.Result:
                {
                    float a = Mathf.Clamp01(resultTime * 4f);
                    Panel(new Rect(0, Screen.height * 0.42f, Screen.width, 120f * s), new Color(0.04f, 0.05f, 0.07f, 0.8f * a));
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, a);
                    string text = won ? "ORIGIN FOUND" : $"{Mathf.FloorToInt(match * 100f)}% match";
                    GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, 76f * s), text, stBanner);
                    GUI.Label(new Rect(0, Screen.height * 0.42f + 72f * s, Screen.width, 40f * s),
                        won ? "Space or click for the next one" : "R to rewind, then move your seeds", stBody);
                    GUI.color = old;
                    break;
                }
                case Phase.Finished:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 0.9f));
                    float cy = Screen.height * 0.36f;
                    GUI.Label(new Rect(0, cy - 60f * s, Screen.width, 100f * s), "EVERY ORIGIN FOUND", stTitle);
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 50f * s, Screen.width * 0.7f, 80f * s),
                        $"{solved} of {set.levels.Length} found, {skipped} skipped.", stBody);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Space to play again", stBody);
                    break;
                }
            }
        }
    }
}
