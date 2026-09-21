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

        const int CellPx = 8;                 // texture pixels per cell; the last row and column are the gap
        const float StepInterval = 0.24f;     // seconds per generation while growing
        const float WinThreshold = 0.999f;
        const int RevealAfter = 2;            // failed attempts before Reveal is offered
        const float PopSeconds = 0.2f;        // a born cell grows to full size over this long
        const float AfterglowSeconds = 0.4f;  // a dead cell fades out over this long
        const string KeyUnlocked = "po.unlocked";
        const string KeySolved = "po.solved";
        const string KeyMuted = "po.muted";
        const float LanternRadius = 3f;       // cells revealed and lit around the wanderer
        const float WalkSpeed = 11f;          // cells per second
        const float KeyRepeat = 0.1f;         // seconds between steps while a direction key is held

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static readonly Color32 ColBg = Hex("0a0c12");
        static readonly Color32 ColBgCentre = Hex("161c2c");
        static readonly Color32 ColBgEdge = Hex("06070b");
        static readonly Color32 ColGap = Hex("05060a");
        static readonly Color32 ColOpen = Hex("141824");
        static readonly Color32 ColOpenEdge = Hex("181d2b");
        static readonly Color32 ColRock = Hex("343a47");
        static readonly Color32 ColRockEdge = Hex("222733");
        static readonly Color32 ColGhost = Hex("2a6d74");
        static readonly Color32 ColGhostBright = Hex("4fb4bd");
        static readonly Color32 ColSeed = Hex("6fe3ff");
        static readonly Color32 ColSeedEdge = Hex("b9f3ff");
        static readonly Color32 ColWrong = Hex("b8364a");
        static readonly Color32 ColWrongEdge = Hex("e0566a");
        static readonly Color32 ColAfter = Hex("4d3a3c");
        static readonly Color32 ColReveal = Hex("ff5fd2");
        static readonly Color32 ColUnknown = Hex("0b0d14");
        static readonly Color32 ColUnknownEdge = Hex("0d1018");
        static readonly Color32 ColRockDark = Hex("181c25");
        static readonly Color32 ColLamp = Hex("ffd9a0");
        static readonly Color32 ColHero = Hex("fff6dc");
        static readonly Color32 ColHeroEdge = Hex("ffb757");
        static readonly Vector2Int[] Dirs = { Vector2Int.left, Vector2Int.right, new Vector2Int(0, -1), new Vector2Int(0, 1) };
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
            public bool gold;
        }

        LevelSet set;
        Level level;
        int levelIndex;
        string lawText = "";
        Sim sim;
        byte[] rock;
        byte[] target;
        byte[] prevCells;
        float[] bornAt;
        float[] diedAt;
        readonly List<Vector2Int> seeds = new List<Vector2Int>();
        HashSet<Vector2Int> answer = new HashSet<Vector2Int>();

        // the wanderer: the cell it stands on, its drawn position, the walk it is on, and what its lantern has shown
        Vector2Int hero;
        Vector2 heroPos;
        readonly List<Vector2Int> path = new List<Vector2Int>();
        bool[] seen;
        bool allSeen;
        float keyTimer;
        int stepParity;

        Texture2D tex;
        Color32[] px;
        SpriteRenderer sr;
        SpriteRenderer bg;
        Camera cam;
        Vector3 camBase;
        Sfx sfx;

        Phase phase = Phase.Title;
        float stepTimer;
        float resultTime;
        float match;
        bool won;
        int attempts;
        bool revealed;
        int missing;
        int extra;
        int solved;
        int skipped;
        float winAt = -10f;
        float shake;
        int unlocked;
        int solvedMask;
        bool muted;
        float demoTimer;
        int demoStage;
        Vector2Int hover = new Vector2Int(-1, -1);
        string fatal;

        readonly List<Button> buttons = new List<Button>();
        GUIStyle stTitle, stH1, stH1Right, stSmall, stSmallRight, stSmallCentre, stHint, stButton, stBanner, stBody;
        Texture2D panelTex;
        int styledHeight;

        bool CanReveal => attempts >= RevealAfter && !revealed && (phase == Phase.Place || phase == Phase.Result);
        bool Solved(int i) => (solvedMask & (1 << i)) != 0;

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
            camBase = new Vector3(0f, -0.8f, -10f);
            cam.transform.position = camBase;
            var extraData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extraData != null) extraData.renderPostProcessing = false;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");

            var back = new GameObject("Background");
            bg = back.AddComponent<SpriteRenderer>();
            bg.sortingOrder = -10;
            bg.sprite = Sprite.Create(MakeVignette(128), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            if (shader != null) bg.sharedMaterial = new Material(shader);

            var grid = new GameObject("Grid");
            sr = grid.AddComponent<SpriteRenderer>();
            if (shader != null) sr.sharedMaterial = new Material(shader);

            unlocked = PlayerPrefs.GetInt(KeyUnlocked, 0);
            solvedMask = PlayerPrefs.GetInt(KeySolved, 0);
            muted = PlayerPrefs.GetInt(KeyMuted, 0) != 0;
            sfx.SetMuted(muted);
            sfx.StartAmbient();

            try
            {
                Debug.Log($"Point of Origin: origin_sim.dll version {Sim.NativeVersion}");
                set = Levels.Load();
                unlocked = Mathf.Clamp(unlocked, 0, set.levels.Length - 1);
                EnterTitle();
            }
            catch (Exception e)
            {
                fatal = e.Message;
                Debug.LogException(e);
            }
        }

        void OnDestroy()
        {
            sim?.Dispose();
            sim = null;
        }

        static Texture2D MakeVignette(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.25f);
                    float k = d * d * (3f - 2f * d);
                    pixels[y * size + x] = Color32.Lerp(ColBgCentre, ColBgEdge, k);
                }
            }
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, set.levels.Length - 1);
            level = set.levels[levelIndex];
            lawText = Levels.Describe(level.birth, level.survive);
            sim?.Dispose();
            sim = new Sim(level.w, level.h);
            sim.SetRule((uint)level.birth, (uint)level.survive);
            rock = level.RockCells();
            target = level.TargetCells();
            sim.Load(rock);
            int n = level.w * level.h;
            prevCells = new byte[n];
            bornAt = new float[n];
            diedAt = new float[n];
            for (int i = 0; i < n; i++) { bornAt[i] = -10f; diedAt[i] = -10f; }
            seen = new bool[n];
            allSeen = false;
            path.Clear();
            hero = StartCell();
            heroPos = hero;
            RevealAround(hero);
            seeds.Clear();
            answer = new HashSet<Vector2Int>(level.OriginCells());
            attempts = 0;
            revealed = false;
            won = false;
            match = 0f;
            missing = 0;
            extra = 0;
            stepTimer = 0f;
            winAt = -10f;

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
            TrackChanges();
            FitCamera();
            Paint();
        }

        void FitCamera()
        {
            if (level == null || cam == null) return;
            // The grid takes about three quarters of the shorter screen axis, leaving the HUD bands clear;
            // on the title screen it sits smaller behind the text.
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float size = Mathf.Max((level.h / 2f + 1.5f) / 0.74f, (level.w / 2f + 1.5f) / 0.74f / aspect);
            if (phase == Phase.Title) size *= 1.45f;
            cam.orthographicSize = size;
            if (bg != null)
            {
                bg.transform.position = new Vector3(camBase.x, camBase.y, 5f);
                bg.transform.localScale = new Vector3(2f * cam.orthographicSize * aspect * 1.04f, 2f * cam.orthographicSize * 1.04f, 1f);
            }
        }

        int FirstUnsolved()
        {
            for (int i = 0; i < set.levels.Length; i++)
                if (i <= unlocked && !Solved(i)) return i;
            return Mathf.Min(unlocked, set.levels.Length - 1);
        }

        int DemoLevel()
        {
            int best = 0;
            for (int i = 0; i < set.levels.Length; i++)
                if (Solved(i)) best = i;
            return best;
        }

        void SavePrefs()
        {
            PlayerPrefs.SetInt(KeyUnlocked, unlocked);
            PlayerPrefs.SetInt(KeySolved, solvedMask);
            PlayerPrefs.SetInt(KeyMuted, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (fatal != null || sim == null) return;
            FitCamera();

            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                var o = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, shake) * 0.6f;
                cam.transform.position = camBase + new Vector3(o.x, o.y, 0f);
            }
            else
            {
                cam.transform.position = camBase;
            }

            var mouse = InputBridge.MousePosition;
            var gui = new Vector2(mouse.x, Screen.height - mouse.y);
            bool inWorld = phase == Phase.Place || phase == Phase.Growing || phase == Phase.Result;
            hover = inWorld ? CellAt(mouse) : new Vector2Int(-1, -1);
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
                    var c = hover;
                    switch (phase)
                    {
                        case Phase.Title: StartLevel(FirstUnsolved()); break;
                        case Phase.Finished: EnterTitle(); break;
                        case Phase.Place:
                            if (c.x >= 0) { if (c == hero && path.Count == 0) ToggleSeed(hero); else WalkTo(c); }
                            break;
                        case Phase.Growing:
                            if (c.x >= 0) WalkTo(c);
                            break;
                        case Phase.Result:
                            if (c.x >= 0) WalkTo(c);
                            else if (won) Next();
                            else Rewind();
                            break;
                    }
                }
            }
            if (InputBridge.RightClicked && phase == Phase.Place && hover.x >= 0 && seeds.Contains(hover))
                ToggleSeed(hover);

            if (InputBridge.Pressed(Key.Space, KeyCode.Space))
            {
                if (phase == Phase.Place) { if (path.Count == 0) ToggleSeed(hero); }
                else Primary();
            }
            if (InputBridge.Pressed(Key.Enter, KeyCode.Return) || InputBridge.Pressed(Key.G, KeyCode.G)) Primary();
            if (InputBridge.Pressed(Key.R, KeyCode.R) && (phase == Phase.Place || phase == Phase.Growing || phase == Phase.Result)) Rewind();
            if (InputBridge.Pressed(Key.N, KeyCode.N) && (phase == Phase.Place || phase == Phase.Result)) Skip();
            if (InputBridge.Pressed(Key.V, KeyCode.V) && CanReveal) Reveal();
            if (InputBridge.Pressed(Key.M, KeyCode.M)) ToggleMute();
            if (InputBridge.Pressed(Key.Escape, KeyCode.Escape))
            {
                if (phase == Phase.Title) { if (!Application.isEditor) Application.Quit(); }
                else EnterTitle();
            }

            if (inWorld)
            {
                var dir = Vector2Int.zero;
                if (InputBridge.Held(Key.A, KeyCode.A) || InputBridge.Held(Key.LeftArrow, KeyCode.LeftArrow)) dir = Vector2Int.left;
                else if (InputBridge.Held(Key.D, KeyCode.D) || InputBridge.Held(Key.RightArrow, KeyCode.RightArrow)) dir = Vector2Int.right;
                else if (InputBridge.Held(Key.W, KeyCode.W) || InputBridge.Held(Key.UpArrow, KeyCode.UpArrow)) dir = new Vector2Int(0, -1);
                else if (InputBridge.Held(Key.S, KeyCode.S) || InputBridge.Held(Key.DownArrow, KeyCode.DownArrow)) dir = new Vector2Int(0, 1);
                if (dir != Vector2Int.zero)
                {
                    keyTimer -= Time.deltaTime;
                    if (keyTimer <= 0f)
                    {
                        keyTimer = KeyRepeat;
                        TryStep(dir);
                    }
                }
                else
                {
                    keyTimer = 0f;
                }
                MoveHero();
            }

            if (phase == Phase.Growing)
            {
                stepTimer += Time.deltaTime;
                while (stepTimer >= StepInterval && phase == Phase.Growing)
                {
                    stepTimer -= StepInterval;
                    Advance();
                }
            }
            else if (phase == Phase.Title)
            {
                Demo();
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

        // ------------------------------------------------------------------ the wanderer

        bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < level.w && c.y < level.h;
        bool Open(Vector2Int c) => InBounds(c) && rock[c.y * level.w + c.x] != Sim.Rock;

        /// <summary>The level's start cell, or the open cell nearest the centre.</summary>
        Vector2Int StartCell()
        {
            var s = level.StartCell();
            if (s.HasValue && Open(s.Value)) return s.Value;
            var centre = new Vector2Int(level.w / 2, level.h / 2);
            for (int r = 0; r < Mathf.Max(level.w, level.h); r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = centre + new Vector2Int(dx, dy);
                        if (Open(c)) return c;
                    }
            return centre;
        }

        void RevealAround(Vector2Int c)
        {
            int r = Mathf.CeilToInt(LanternRadius);
            float r2 = LanternRadius * LanternRadius + 0.5f;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    var n = c + new Vector2Int(dx, dy);
                    if (InBounds(n)) seen[n.y * level.w + n.x] = true;
                }
        }

        /// <summary>Shortest four-way walk between two cells around rock, excluding the start. Empty when unreachable.</summary>
        List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            var result = new List<Vector2Int>();
            if (!Open(to) || from == to) return result;
            int w = level.w, n = w * level.h;
            var prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = -1;
            int fromIndex = from.y * w + from.x;
            prev[fromIndex] = fromIndex;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) break;
                foreach (var d in Dirs)
                {
                    var m = c + d;
                    if (!Open(m)) continue;
                    int mi = m.y * w + m.x;
                    if (prev[mi] >= 0) continue;
                    prev[mi] = c.y * w + c.x;
                    queue.Enqueue(m);
                }
            }
            int ti = to.y * w + to.x;
            if (prev[ti] < 0) return result;
            for (int i = ti; i != fromIndex; i = prev[i]) result.Add(new Vector2Int(i % w, i / w));
            result.Reverse();
            return result;
        }

        /// <summary>Walk to a cell. Mid-step, the walk continues from the cell being entered so the figure never snaps back.</summary>
        void WalkTo(Vector2Int c)
        {
            var from = path.Count > 0 ? path[0] : hero;
            if (c == from) return;
            var p = FindPath(from, c);
            if (p.Count == 0)
            {
                sfx.Blocked();
                return;
            }
            var first = path.Count > 0 ? path[0] : (Vector2Int?)null;
            path.Clear();
            if (first.HasValue) path.Add(first.Value);
            path.AddRange(p);
        }

        void TryStep(Vector2Int dir)
        {
            if (path.Count > 0) return;
            var n = hero + dir;
            if (Open(n)) path.Add(n);
        }

        void MoveHero()
        {
            if (path.Count == 0) return;
            var next = path[0];
            Vector2 goal = next;
            heroPos = Vector2.MoveTowards(heroPos, goal, WalkSpeed * Time.deltaTime);
            if ((heroPos - goal).sqrMagnitude < 1e-5f)
            {
                heroPos = goal;
                hero = next;
                path.RemoveAt(0);
                RevealAround(hero);
                stepParity ^= 1;
                sfx.Step(stepParity);
            }
        }

        /// <summary>The title screen replays the last solved level's origins growing, over and over.</summary>
        void Demo()
        {
            demoTimer += Time.deltaTime;
            switch (demoStage)
            {
                case 0:
                    if (demoTimer > 1.0f) { demoStage = 1; demoTimer = 0f; }
                    break;
                case 1:
                    if (demoTimer > 0.5f)
                    {
                        demoTimer = 0f;
                        sim.Step(1);
                        TrackChanges();
                        if (sim.Generation >= level.steps) demoStage = 2;
                    }
                    break;
                default:
                    if (demoTimer > 2.2f)
                    {
                        demoTimer = 0f;
                        DemoReset();
                        demoStage = 0;
                    }
                    break;
            }
        }

        void DemoReset()
        {
            sim.ClearLife();
            foreach (var a in answer) sim.Set(a.x, a.y, Sim.Alive);
            TrackChanges();
        }

        // ------------------------------------------------------------------ actions

        void Primary()
        {
            switch (phase)
            {
                case Phase.Title: StartLevel(FirstUnsolved()); break;
                case Phase.Place: Grow(); break;
                case Phase.Growing: while (phase == Phase.Growing) Advance(); break;
                case Phase.Result: if (won) Next(); else Rewind(); break;
                case Phase.Finished: EnterTitle(); break;
            }
        }

        void EnterTitle()
        {
            LoadLevel(DemoLevel());
            phase = Phase.Title;
            allSeen = true;
            demoStage = 0;
            demoTimer = 0f;
            DemoReset();
        }

        void StartLevel(int index)
        {
            LoadLevel(index);
            phase = Phase.Place;
            sfx.Select();
        }

        void ToggleSeed(Vector2Int c)
        {
            int i = c.y * level.w + c.x;
            if (rock[i] == Sim.Rock) return;
            if (seeds.Remove(c))
            {
                sim.Set(c.x, c.y, Sim.Dead);
                TrackChanges();
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
            TrackChanges();
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
            TrackChanges();
            match = sim.Compare(target);
            if (gen >= level.steps) Finish();
        }

        void Finish()
        {
            match = sim.Compare(target);
            won = match >= WinThreshold;
            missing = 0;
            extra = 0;
            sim.Refresh();
            for (int i = 0; i < target.Length; i++)
            {
                bool a = sim.Cells[i] == Sim.Alive, t = target[i] == Sim.Alive;
                if (t && !a) missing++;
                if (a && !t) extra++;
            }
            phase = Phase.Result;
            resultTime = 0f;
            if (won)
            {
                winAt = Time.time;
                solved++;
                solvedMask |= 1 << levelIndex;
                unlocked = Mathf.Max(unlocked, Mathf.Min(levelIndex + 1, set.levels.Length - 1));
                SavePrefs();
                sfx.Success();
            }
            else
            {
                shake = 0.3f;
                sfx.Fail();
            }
        }

        void Rewind()
        {
            bool hadGrown = sim.Generation > 0;
            sim.ClearLife();
            foreach (var s in seeds) sim.Set(s.x, s.y, Sim.Alive);
            TrackChanges();
            stepTimer = 0f;
            phase = Phase.Place;
            if (hadGrown) sfx.Rewind();
        }

        void Skip()
        {
            skipped++;
            unlocked = Mathf.Max(unlocked, Mathf.Min(levelIndex + 1, set.levels.Length - 1));
            SavePrefs();
            Next();
        }

        void Reveal()
        {
            revealed = true;
            allSeen = true;
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
            phase = Phase.Place;
        }

        void ToggleMute()
        {
            muted = !muted;
            sfx.SetMuted(muted);
            SavePrefs();
        }

        // ------------------------------------------------------------------ painting

        /// <summary>Remember when each cell was born or died so Paint can animate it.</summary>
        void TrackChanges()
        {
            sim.Refresh();
            float now = Time.time;
            for (int i = 0; i < prevCells.Length; i++)
            {
                byte c = sim.Cells[i], p = prevCells[i];
                if (c == Sim.Alive && p != Sim.Alive) bornAt[i] = now;
                else if (c != Sim.Alive && p == Sim.Alive) diedAt[i] = now;
                prevCells[i] = c;
            }
        }

        static Color32 AgeColor(int age) => AgeRamp[Mathf.Clamp(age - 1, 0, AgeRamp.Length - 1)];

        static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, 255), t);

        void Paint()
        {
            if (sim == null || tex == null) return;
            sim.Refresh();
            for (int i = 0; i < px.Length; i++) px[i] = ColGap;

            float now = Time.time;
            bool placing = phase == Phase.Place || (phase == Phase.Title && sim.Generation == 0);
            bool lamp = phase != Phase.Title && phase != Phase.Finished;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
            Color32 ghost = Color32.Lerp(ColGhost, ColGhostBright, pulse * 0.6f);
            float flash = 1f - Mathf.Clamp01((now - winAt) / 0.5f);
            float reach = LanternRadius + 1.3f;

            for (int y = 0; y < level.h; y++)
            {
                for (int x = 0; x < level.w; x++)
                {
                    int i = y * level.w + x;
                    byte c = sim.Cells[i];
                    bool inTarget = target[i] == Sim.Alive;
                    bool isRock = rock[i] == Sim.Rock;
                    bool alive = c == Sim.Alive;
                    bool visible = allSeen || seen[i] || alive;
                    bool hot = !isRock && visible && hover.x == x && hover.y == y;
                    float light = 0f;
                    if (lamp)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), heroPos);
                        light = Mathf.Clamp01(1f - dist / reach);
                        light *= light;
                    }

                    Color32 baseFill, baseEdge;
                    if (!visible)
                    {
                        baseFill = isRock ? ColRockDark : ColUnknown;
                        baseEdge = isRock ? ColRockDark : ColUnknownEdge;
                    }
                    else
                    {
                        baseFill = isRock ? ColRock : ColOpen;
                        baseEdge = isRock ? ColRockEdge : (inTarget ? ghost : ColOpenEdge);
                    }
                    if (light > 0f)
                    {
                        baseFill = Color32.Lerp(baseFill, ColLamp, light * 0.28f);
                        baseEdge = Color32.Lerp(baseEdge, ColLamp, light * 0.38f);
                    }
                    if (hot)
                    {
                        baseFill = Lighten(baseFill, 0.18f);
                        baseEdge = Lighten(baseEdge, 0.25f);
                    }
                    PaintCell(x, y, baseFill, baseEdge, 0);
                    if (isRock) continue;

                    if (alive)
                    {
                        Color32 fill, edge;
                        if (placing) { fill = ColSeed; edge = ColSeedEdge; }
                        else if (inTarget) { fill = AgeColor(sim.Ages[i]); edge = Lighten(fill, 0.35f); }
                        else { fill = ColWrong; edge = ColWrongEdge; }
                        if (flash > 0f && inTarget && !placing) fill = Lighten(fill, flash * 0.85f);
                        if (light > 0f) fill = Color32.Lerp(fill, ColLamp, light * 0.12f);
                        if (hot) fill = Lighten(fill, 0.15f);
                        float a = (now - bornAt[i]) / PopSeconds;
                        int inset = a >= 1f ? 0 : Mathf.Clamp((int)Mathf.Lerp(3.99f, 0f, Mathf.Clamp01(a)), 0, 3);
                        PaintCell(x, y, fill, inset == 0 ? edge : fill, inset);
                    }
                    else if (visible && now - diedAt[i] < AfterglowSeconds)
                    {
                        float d = (now - diedAt[i]) / AfterglowSeconds;
                        int inset = Mathf.Clamp((int)Mathf.Lerp(0f, 3.99f, d), 0, 3);
                        PaintCell(x, y, ColAfter, ColAfter, inset);
                    }
                    if (revealed && answer.Contains(new Vector2Int(x, y))) PaintDot(x, y, ColReveal);
                }
            }
            if (lamp) PaintHero(now);
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>The wanderer: a small warm figure with a rounded outline, drawn at its interpolated position.</summary>
        void PaintHero(float now)
        {
            int tw = tex.width, th = tex.height;
            float bob = Mathf.Sin(now * 5f) * 0.6f;
            int cx = Mathf.RoundToInt(heroPos.x * CellPx) + 3;
            int cy = Mathf.RoundToInt((level.h - 1 - heroPos.y) * CellPx + 3 + bob);
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (Mathf.Abs(dx) == 2 && Mathf.Abs(dy) == 2) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= tw || y >= th) continue;
                    bool rim = Mathf.Abs(dx) == 2 || Mathf.Abs(dy) == 2;
                    px[y * tw + x] = rim ? ColHeroEdge : ColHero;
                }
            }
        }

        /// <summary>Fill a cell's 7x7 square, shrunk by <paramref name="inset"/> pixels on every side, with a 1px border.</summary>
        void PaintCell(int x, int y, Color32 fill, Color32 edge, int inset)
        {
            int tw = tex.width;
            int x0 = x * CellPx;
            int y0 = (level.h - 1 - y) * CellPx;
            int inner = CellPx - 1;
            int lo = inset, hi = inner - inset;
            for (int dy = lo; dy < hi; dy++)
            {
                int row = (y0 + dy) * tw + x0;
                for (int dx = lo; dx < hi; dx++)
                {
                    bool border = dx == lo || dy == lo || dx == hi - 1 || dy == hi - 1;
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

            if (phase == Phase.Title)
            {
                int n = set.levels.Length;
                float b = 44f * s, gap = 10f * s;
                float total = n * b + (n - 1) * gap;
                float x0 = (Screen.width - total) / 2f;
                float y = Screen.height * 0.74f;
                for (int i = 0; i < n; i++)
                {
                    int index = i;
                    buttons.Add(new Button
                    {
                        rect = new Rect(x0 + i * (b + gap), y, b, b),
                        label = (i + 1).ToString(),
                        act = () => StartLevel(index),
                        enabled = i <= unlocked,
                        gold = Solved(i),
                    });
                }
                return;
            }

            float w = 168f * s, h = 46f * s, spacing = 12f * s;
            float bx = Screen.width - 24f * s - w;
            float by = Screen.height - 24f * s - h;

            void Add(string label, Action act, bool enabled = true)
            {
                buttons.Add(new Button { rect = new Rect(bx, by, w, h), label = label, act = act, enabled = enabled });
                bx -= w + spacing;
            }

            switch (phase)
            {
                case Phase.Place:
                    Add("Grow  [Enter]", Grow, seeds.Count > 0);
                    Add("Skip  [N]", Skip);
                    if (CanReveal) Add("Reveal  [V]", Reveal);
                    break;
                case Phase.Growing:
                    Add("Finish  [Enter]", Primary);
                    Add("Rewind  [R]", Rewind);
                    break;
                case Phase.Result:
                    if (won)
                    {
                        Add("Next  [Enter]", Next);
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
            stSmallCentre = Make(15, FontStyle.Normal, TextAnchor.UpperCenter, "5f6880");
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

        void DrawButtons(Vector2 gui)
        {
            foreach (var b in buttons)
            {
                bool hot = b.enabled && b.rect.Contains(gui);
                Color back = !b.enabled ? new Color(0.10f, 0.11f, 0.15f, 0.6f)
                    : hot ? new Color(0.30f, 0.36f, 0.48f, 0.95f)
                    : b.gold ? new Color(0.30f, 0.26f, 0.14f, 0.92f)
                    : new Color(0.18f, 0.21f, 0.29f, 0.92f);
                Panel(b.rect, back);
                var old = GUI.color;
                GUI.color = !b.enabled ? new Color(1f, 1f, 1f, 0.3f) : b.gold ? new Color(1f, 0.85f, 0.45f, 1f) : Color.white;
                GUI.Label(b.rect, b.label, stButton);
                GUI.color = old;
            }
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

            if (phase != Phase.Title && phase != Phase.Finished)
            {
                // top left: level, then the law in plain words
                GUI.Label(new Rect(24f * s, 16f * s, Screen.width * 0.6f, 40f * s),
                    $"{levelIndex + 1:00} / {set.levels.Length:00}    {level.name}", stH1);
                GUI.Label(new Rect(24f * s, 54f * s, Screen.width * 0.55f, 48f * s),
                    $"{lawText}   ({level.rule}, {level.steps} generation{(level.steps == 1 ? "" : "s")})", stSmall);

                // top right: seeds or generation, then progress
                bool placingHud = phase == Phase.Place;
                string right = placingHud ? $"Seeds {seeds.Count} / {level.seeds}" : $"Generation {sim.Generation} / {level.steps}";
                GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 16f * s, Screen.width * 0.6f, 40f * s), right, stH1Right);
                string sub = placingHud
                    ? (attempts > 0 ? $"attempt {attempts + 1}" : "walk to where it began, then press Space")
                    : $"match {Mathf.FloorToInt(match * 100f)}%   attempt {attempts}";
                GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 54f * s, Screen.width * 0.6f, 30f * s), sub, stSmallRight);

                // bottom left: the hint, or the tip once an attempt has failed
                string hint = attempts > 0 && !string.IsNullOrEmpty(level.tip) ? "Tip: " + level.tip : level.hint;
                float buttonsWidth = buttons.Count * 180f * s + 40f * s;
                GUI.Label(new Rect(24f * s, Screen.height - 120f * s, Screen.width - buttonsWidth - 48f * s, 88f * s), hint, stHint);

                GUI.Label(new Rect(0, Screen.height - 26f * s, Screen.width, 22f * s),
                    (muted ? "sound off   " : "") + "click or WASD walk   Space plant   Enter grow   R rewind   N skip   M sound   Esc menu", stSmallCentre);
            }

            DrawButtons(gui);

            switch (phase)
            {
                case Phase.Title:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 0.66f));
                    float cy = Screen.height * 0.24f;
                    GUI.Label(new Rect(0, cy - 60f * s, Screen.width, 100f * s), "POINT OF ORIGIN", stTitle);
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 36f * s, Screen.width * 0.7f, 60f * s),
                        "You are shown how it ended. Find where it began.", stBody);
                    GUI.Label(new Rect(Screen.width * 0.1f, cy + 92f * s, Screen.width * 0.8f, 100f * s),
                        "You wake in the dark where something ended. Walk, and its ruins show themselves.\n" +
                        "Stand where it began, plant the seed, and grow it back exactly.", stBody);
                    int done = 0;
                    for (int i = 0; i < set.levels.Length; i++) if (Solved(i)) done++;
                    GUI.Label(new Rect(0, Screen.height * 0.74f - 30f * s, Screen.width, 24f * s),
                        done == 0 ? "levels" : $"levels   ({done} of {set.levels.Length} found)", stSmallCentre);
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(0, Screen.height * 0.74f + 62f * s, Screen.width, 40f * s),
                        done == 0 ? "click or press Space to begin" : "click or press Space to continue", stBody);
                    GUI.color = old;
                    GUI.Label(new Rect(24f * s, Screen.height - 60f * s, Screen.width - 48f * s, 30f * s),
                        "made for CPGD's World's First Game Jam, theme ORIGIN   |   Odin + Nexium + Unity", stSmallRight);
                    GUI.Label(new Rect(24f * s, Screen.height - 60f * s, Screen.width * 0.5f, 30f * s),
                        (muted ? "sound off (M)" : "M sound") + "   Esc quit", stSmall);
                    break;
                }
                case Phase.Result:
                {
                    // a failed result fades away after a moment so the mismatch underneath can be studied
                    float a = Mathf.Clamp01(resultTime * 4f);
                    if (!won) a *= 1f - Mathf.Clamp01((resultTime - 2.4f) / 0.6f);
                    if (a <= 0f) break;
                    Panel(new Rect(0, Screen.height * 0.40f, Screen.width, 140f * s), new Color(0.04f, 0.05f, 0.07f, 0.8f * a));
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, a);
                    string text = won ? "ORIGIN FOUND" : $"{Mathf.FloorToInt(match * 100f)}% match";
                    GUI.Label(new Rect(0, Screen.height * 0.40f, Screen.width, 76f * s), text, stBanner);
                    string detail = won
                        ? (attempts == 1 ? "first try" : $"on attempt {attempts}") + "   -   Space or click for the next one"
                        : $"{missing} missing, {extra} astray   -   R to rewind, then move your seeds";
                    GUI.Label(new Rect(0, Screen.height * 0.40f + 72f * s, Screen.width, 40f * s), detail, stBody);
                    GUI.color = old;
                    break;
                }
                case Phase.Finished:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 0.9f));
                    float cy = Screen.height * 0.36f;
                    GUI.Label(new Rect(0, cy - 60f * s, Screen.width, 100f * s), "EVERY ORIGIN FOUND", stTitle);
                    int done = 0;
                    for (int i = 0; i < set.levels.Length; i++) if (Solved(i)) done++;
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 50f * s, Screen.width * 0.7f, 80f * s),
                        $"{done} of {set.levels.Length} origins found" + (skipped > 0 ? $", {skipped} skipped this run." : "."), stBody);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Space for the menu", stBody);
                    break;
                }
            }
        }
    }
}
