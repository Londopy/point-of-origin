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
        enum Phase { Title, Story, Place, Growing, Result, Dead, GameOver, Finished }

        class Ember
        {
            public Vector2Int cell;
            public Vector2Int dir;
            public Vector2 pos;
        }

        const int StartLives = 3;             // lanterns per chapter
        const float DeathSeconds = 1.4f;      // the fall, before the chapter restarts
        const float EmberSpeed = 2.4f;        // cells per second

        const string Epilogue =
            "Every origin found. The world grows again, and you, who were its last seed, walk on in the light.";

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
        const float CamPitch = 32f;           // the isometric camera
        const float CamYaw = 45f;
        const float TileSize = 0.90f;         // footprint of a tile inside its 1x1 cell
        const float TileBottom = -0.55f;      // slabs reach this far down into the void
        const float FloorTop = 0.20f;         // top of an ordinary tile
        const float RockTop = 0.95f;
        const float AliveTop = 0.46f;         // a living cell's top, plus a little per generation of age
        const float RiseSeconds = 0.35f;      // a revealed tile rises out of the void over this long

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
        static readonly Color32 ColEmber = Hex("ff5a1f");
        static readonly Color32 ColEmberCore = Hex("ffe2a8");
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
        float[] seenAt;
        bool allSeen;
        float keyTimer;
        int stepParity;
        readonly List<Vector2Int> pickupsLeft = new List<Vector2Int>();  // seeds still lying in the world
        int carried;                                                       // seeds in hand
        bool fetching;                                                     // this level's seeds must be found first
        readonly List<Ember> embers = new List<Ember>();
        int lives = StartLives;
        float deathAt;
        string deathText = "";

        WorldView view;
        GameObject baseObj;
        Bounds baseBounds;   // world bounds of the unscaled base mesh
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
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.transform.rotation = Quaternion.Euler(CamPitch, CamYaw, 0f);
            camBase = -cam.transform.forward * 80f;
            cam.transform.position = camBase;
            var extraData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extraData != null) extraData.renderPostProcessing = false;

            var shader = Shader.Find("PointOfOrigin/VertexColor");
            if (shader == null) Debug.LogError("Point of Origin: the PointOfOrigin/VertexColor shader is missing from Resources");
            var world = new GameObject("World");
            view = world.AddComponent<WorldView>();
            view.Init(new Material(shader));

            // the stone slab under the world: modelled in Houdini, written out as a triangle list with baked vertex colours
            var baseText = Resources.Load<TextAsset>("Models/DioramaBase");
            if (baseText != null && shader != null)
            {
                var baseMesh = MeshText.Load(baseText, "diorama base");
                if (baseMesh.vertexCount > 0)
                {
                    baseObj = new GameObject("Diorama Base");
                    baseObj.AddComponent<MeshFilter>().sharedMesh = baseMesh;
                    var mr = baseObj.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = new Material(shader);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    baseBounds = baseMesh.bounds;
                }
            }

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
            seenAt = new float[n];
            allSeen = false;
            path.Clear();
            hero = StartCell();
            heroPos = hero;
            RevealAround(hero);
            pickupsLeft.Clear();
            pickupsLeft.AddRange(level.PickupCells());
            fetching = pickupsLeft.Count > 0;
            carried = fetching ? 0 : level.seeds;
            embers.Clear();
            foreach (var spec in level.EmberSpecs())
                embers.Add(new Ember { cell = spec.cell, pos = spec.cell, dir = spec.vertical ? new Vector2Int(0, 1) : Vector2Int.right });
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

            TrackChanges();
            FitBase();
            FitCamera();
            BuildWorld();
        }

        /// <summary>Stretch the slab to the level's footprint and tuck its top just under the tiles.</summary>
        void FitBase()
        {
            if (baseObj == null || baseBounds.size.x < 0.01f || baseBounds.size.z < 0.01f) return;
            float sx = (level.w + 2.4f) / baseBounds.size.x;
            float sz = (level.h + 2.4f) / baseBounds.size.z;
            baseObj.transform.localScale = new Vector3(sx, 1f, sz);
            baseObj.transform.position = new Vector3(-baseBounds.center.x * sx, (TileBottom - 0.03f) - baseBounds.max.y, -baseBounds.center.z * sz);
        }

        /// <summary>World position of a cell's centre at the floor plane: row 0 is the far edge.</summary>
        Vector3 CellCentre(float x, float y) =>
            new Vector3(x - level.w / 2f + 0.5f, 0f, (level.h - 1 - y) - level.h / 2f + 0.5f);

        void FitCamera()
        {
            if (level == null || cam == null) return;
            // Fit the grid's footprint box on screen, leaving the HUD bands clear; the title shows it smaller.
            float aspect = Mathf.Max(0.1f, cam.aspect);
            var m = cam.worldToCameraMatrix;
            float hw = level.w / 2f + 0.4f, hh = level.h / 2f + 0.4f;
            float ex = 0f, ey = 0f;
            for (int i = 0; i < 8; i++)
            {
                float low = baseObj != null ? TileBottom - baseBounds.size.y - 0.03f : TileBottom;
                var p = new Vector3((i & 1) == 0 ? -hw : hw, (i & 2) == 0 ? low : RockTop + 0.4f, (i & 4) == 0 ? -hh : hh);
                var v = m.MultiplyPoint(p);
                ex = Mathf.Max(ex, Mathf.Abs(v.x));
                ey = Mathf.Max(ey, Mathf.Abs(v.y));
            }
            float size = Mathf.Max(ey / 0.74f, ex / aspect / 0.84f);
            if (phase == Phase.Title) size *= 1.4f;
            cam.orthographicSize = size;
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
                cam.transform.position = camBase + cam.transform.right * o.x + cam.transform.up * o.y;
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
                        case Phase.Story: phase = Phase.Place; break;
                        case Phase.GameOver: Retry(); break;
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
                UpdateEmbers(Time.deltaTime);
            }

            if (phase == Phase.Dead && Time.time - deathAt > DeathSeconds)
            {
                if (lives > 0) RestartLevel();
                else phase = Phase.GameOver;
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
            BuildWorld();
        }

        /// <summary>The cell under a screen point: the mouse ray meets the plane of the tile tops.</summary>
        Vector2Int CellAt(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, FloorTop + 0.1f, 0f));
            if (!plane.Raycast(ray, out float dist)) return new Vector2Int(-1, -1);
            var wp = ray.GetPoint(dist);
            int x = Mathf.FloorToInt(wp.x + level.w / 2f);
            int row = Mathf.FloorToInt(wp.z + level.h / 2f);
            int y = level.h - 1 - row;
            if (x < 0 || y < 0 || x >= level.w || y >= level.h) return new Vector2Int(-1, -1);
            return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------ the wanderer

        bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < level.w && c.y < level.h;
        bool Open(Vector2Int c) => InBounds(c) && rock[c.y * level.w + c.x] != Sim.Rock;

        /// <summary>Where the wanderer may step: open ground that living growth has not taken (your own seeds at generation 0 are fine).</summary>
        bool Passable(Vector2Int c)
        {
            if (!Open(c)) return false;
            return sim.Generation == 0 || sim.Cells[c.y * level.w + c.x] != Sim.Alive;
        }

        /// <summary>Embers drift along their row or column, turning at rock or the edge; touching one is the end.</summary>
        void UpdateEmbers(float dt)
        {
            foreach (var e in embers)
            {
                var next = e.cell + e.dir;
                if (!Open(next))
                {
                    e.dir = -e.dir;
                    next = e.cell + e.dir;
                    if (!Open(next)) continue;
                }
                e.pos = Vector2.MoveTowards(e.pos, next, EmberSpeed * dt);
                if ((e.pos - (Vector2)next).sqrMagnitude < 1e-5f)
                {
                    e.pos = next;
                    e.cell = next;
                }
                if (Vector2.Distance(e.pos, heroPos) < 0.6f)
                {
                    Die("THE EMBER TOOK YOU");
                    return;
                }
            }
        }

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
                    if (!InBounds(n)) continue;
                    int i = n.y * level.w + n.x;
                    if (seen[i]) continue;
                    seen[i] = true;
                    seenAt[i] = Time.time;
                }
        }

        /// <summary>Shortest four-way walk between two cells around rock, excluding the start. Empty when unreachable.</summary>
        List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            var result = new List<Vector2Int>();
            if (!Passable(to) || from == to) return result;
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
                    if (!Passable(m)) continue;
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
            if (Passable(n)) path.Add(n);
        }

        void MoveHero()
        {
            if (path.Count == 0) return;
            var next = path[0];
            if (!Passable(next))
            {
                // the growth has taken the ground ahead: stop where you are
                path.Clear();
                return;
            }
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
                if (pickupsLeft.Remove(hero))
                {
                    carried++;
                    sfx.Success();
                }
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
                case Phase.Story: phase = Phase.Place; break;
                case Phase.GameOver: Retry(); break;
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
            lives = StartLives;
            BeginLevel();
            sfx.Select();
        }

        /// <summary>A lantern goes out. After the fall the chapter restarts, or the dark takes you.</summary>
        void Die(string why)
        {
            if (phase == Phase.Dead || phase == Phase.GameOver) return;
            phase = Phase.Dead;
            deathAt = Time.time;
            deathText = why;
            lives = Mathf.Max(0, lives - 1);
            path.Clear();
            shake = 0.45f;
            sfx.Fail();
        }

        /// <summary>Restart the chapter, keeping what the lantern has already shown.</summary>
        void RestartLevel()
        {
            var keepSeen = seen;
            var keepSeenAt = seenAt;
            LoadLevel(levelIndex);
            for (int i = 0; i < seen.Length && i < keepSeen.Length; i++)
                if (keepSeen[i]) { seen[i] = true; seenAt[i] = keepSeenAt[i]; }
            phase = Phase.Place;
        }

        void Retry()
        {
            lives = StartLives;
            RestartLevel();
            sfx.Select();
        }

        /// <summary>A level opens on its chapter card when it has one, otherwise straight into play.</summary>
        void BeginLevel()
        {
            phase = string.IsNullOrEmpty(level.intro) ? Phase.Place : Phase.Story;
        }

        void ToggleSeed(Vector2Int c)
        {
            int i = c.y * level.w + c.x;
            if (rock[i] == Sim.Rock) return;
            if (seeds.Remove(c))
            {
                sim.Set(c.x, c.y, Sim.Dead);
                carried++;
                TrackChanges();
                sfx.Remove();
                return;
            }
            if (carried <= 0)
            {
                sfx.Blocked();
                return;
            }
            if (seeds.Count >= level.seeds)
            {
                var oldest = seeds[0];
                seeds.RemoveAt(0);
                sim.Set(oldest.x, oldest.y, Sim.Dead);
                carried++;
            }
            seeds.Add(c);
            carried--;
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
            if (sim.Cells[hero.y * level.w + hero.x] == Sim.Alive)
            {
                Die("OVERGROWN");
                return;
            }
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
            lives = StartLives;
            BeginLevel();
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

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// Rebuild the diorama: every visible cell is a slab whose colour and
        /// height say what it is, revealed ground rises out of the void, living
        /// cells stand taller with age, and the wanderer stands on its cell.
        /// </summary>
        void BuildWorld()
        {
            if (sim == null || view == null) return;
            sim.Refresh();
            view.Begin();

            float now = Time.time;
            bool placing = phase == Phase.Place || (phase == Phase.Title && sim.Generation == 0);
            bool lamp = phase != Phase.Title && phase != Phase.Finished;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
            Color32 ghost = Color32.Lerp(ColGhost, ColGhostBright, pulse * 0.6f);
            float flash = 1f - Mathf.Clamp01((now - winAt) / 0.5f);
            float reach = LanternRadius + 1.3f;
            float half = TileSize / 2f;

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
                    var cell = CellCentre(x, y);
                    if (!visible)
                    {
                        // unseen ground: a sunken, dark slab, so the world's footprint is always there to explore
                        float sunkTop = TileBottom + (isRock ? 0.32f : 0.12f);
                        view.Box(cell.x - half, TileBottom, cell.z - half, cell.x + half, sunkTop, cell.z + half, isRock ? ColRockDark : ColUnknown);
                        continue;
                    }
                    float rise = seen[i] ? EaseOut((now - seenAt[i]) / RiseSeconds) : 1f;
                    bool hot = !isRock && hover.x == x && hover.y == y;
                    float light = 0f;
                    if (lamp)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), heroPos);
                        light = Mathf.Clamp01(1f - dist / reach);
                        light *= light;
                    }

                    Color32 top;
                    float height;
                    if (isRock)
                    {
                        top = ColRock;
                        height = RockTop;
                    }
                    else if (alive)
                    {
                        if (placing) top = ColSeed;
                        else if (inTarget) top = AgeColor(sim.Ages[i]);
                        else top = ColWrong;
                        if (flash > 0f && inTarget && !placing) top = Lighten(top, flash * 0.85f);
                        float grown = EaseOut((now - bornAt[i]) / PopSeconds);
                        float full = AliveTop + Mathf.Min(sim.Ages[i], 5) * 0.04f;
                        height = Mathf.Lerp(FloorTop, full, grown);
                    }
                    else
                    {
                        top = ColOpen;
                        height = FloorTop;
                        float since = now - diedAt[i];
                        if (since < AfterglowSeconds)
                        {
                            float d = since / AfterglowSeconds;
                            top = Color32.Lerp(ColAfter, ColOpen, d);
                            height = Mathf.Lerp(AliveTop, FloorTop, EaseOut(d));
                        }
                    }
                    if (light > 0f) top = Color32.Lerp(top, ColLamp, light * (alive ? 0.12f : 0.28f));
                    if (hot) top = Lighten(top, 0.18f);
                    top = Color32.Lerp(ColUnknown, top, rise);

                    var p = cell;
                    float yTop = Mathf.Lerp(TileBottom + 0.12f, height, rise);
                    view.Box(p.x - half, TileBottom, p.z - half, p.x + half, yTop, p.z + half, top);

                    if (inTarget && !alive && !isRock && rise >= 1f)
                    {
                        Color32 frame = light > 0f ? Color32.Lerp(ghost, ColLamp, light * 0.3f) : ghost;
                        view.Rim(p.x - half, p.z - half, p.x + half, p.z + half, yTop + 0.012f, 0.09f, frame);
                    }
                    if (revealed && answer.Contains(new Vector2Int(x, y)))
                        view.Box(p.x - 0.14f, yTop, p.z - 0.14f, p.x + 0.14f, yTop + 0.16f, p.z + 0.14f, ColReveal);
                }
            }
            // embers: hot drifting remnants, visible even in the dark
            foreach (var e in embers)
            {
                var p = CellCentre(e.pos.x, e.pos.y);
                float under = StandingHeight(e.cell);
                float y0 = under + 0.14f + 0.04f * Mathf.Sin(now * 6f + e.cell.x);
                float g = 0.5f + 0.5f * Mathf.Sin(now * 9f + e.cell.y);
                var col = Color32.Lerp(ColEmber, ColEmberCore, g * 0.45f);
                view.Box(p.x - 0.16f, y0, p.z - 0.16f, p.x + 0.16f, y0 + 0.30f, p.z + 0.16f, col);
                view.Box(p.x - 0.08f, y0 + 0.30f, p.z - 0.08f, p.x + 0.08f, y0 + 0.44f, p.z + 0.08f, ColEmberCore);
            }
            // seeds lying in the world: small crystals that hover and pulse, only where the lantern has been
            foreach (var k in pickupsLeft)
            {
                int i = k.y * level.w + k.x;
                if (!(allSeen || seen[i])) continue;
                var p = CellCentre(k.x, k.y);
                float ybase = FloorTop + 0.16f + 0.05f * Mathf.Sin(now * 3f + k.x);
                float glow = 0.5f + 0.5f * Mathf.Sin(now * 4f + k.y);
                var col = Color32.Lerp(ColSeed, ColSeedEdge, glow);
                view.Box(p.x - 0.11f, ybase, p.z - 0.11f, p.x + 0.11f, ybase + 0.22f, p.z + 0.11f, col);
                view.Box(p.x - 0.06f, ybase + 0.22f, p.z - 0.06f, p.x + 0.06f, ybase + 0.34f, p.z + 0.06f, col);
            }
            if (lamp) BuildHero(now);
            view.Commit();
        }

        /// <summary>The wanderer: a small body and head standing on its cell, bobbing, with a lantern at its side.</summary>
        void BuildHero(float now)
        {
            var p = CellCentre(heroPos.x, heroPos.y);
            var under = new Vector2Int(Mathf.RoundToInt(heroPos.x), Mathf.RoundToInt(heroPos.y));
            float ground = StandingHeight(under);
            float bob = 0.02f * Mathf.Sin(now * 5f) + 0.02f;
            // when a lantern goes out the figure sinks into the ground and darkens
            float sink = phase == Phase.Dead || phase == Phase.GameOver ? EaseOut((now - deathAt) / 0.9f) : 0f;
            float y0 = ground + bob - sink * 0.95f;
            var body = Color32.Lerp(ColHero, ColAfter, sink);
            var head = Color32.Lerp(ColHeroEdge, ColAfter, sink);
            view.Box(p.x - 0.17f, y0, p.z - 0.17f, p.x + 0.17f, y0 + 0.42f, p.z + 0.17f, body);
            view.Box(p.x - 0.12f, y0 + 0.46f, p.z - 0.12f, p.x + 0.12f, y0 + 0.70f, p.z + 0.12f, head);
            if (sink < 0.5f) view.Box(p.x + 0.20f, y0 + 0.18f, p.z - 0.06f, p.x + 0.32f, y0 + 0.34f, p.z + 0.06f, ColLamp);
        }

        float StandingHeight(Vector2Int c)
        {
            if (!InBounds(c)) return FloorTop;
            int i = c.y * level.w + c.x;
            if (sim.Cells[i] == Sim.Alive) return AliveTop + Mathf.Min(sim.Ages[i], 5) * 0.04f;
            return FloorTop;
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
                case Phase.GameOver:
                    Add("Try again  [Enter]", Retry);
                    Add("Menu  [Esc]", EnterTitle);
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

            if (phase != Phase.Title && phase != Phase.Finished && phase != Phase.Story && phase != Phase.GameOver)
            {
                // top left: level, then the law in plain words
                GUI.Label(new Rect(24f * s, 16f * s, Screen.width * 0.6f, 40f * s),
                    $"{levelIndex + 1:00} / {set.levels.Length:00}    {level.name}", stH1);
                GUI.Label(new Rect(24f * s, 54f * s, Screen.width * 0.55f, 48f * s),
                    $"{lawText}   ({level.rule}, {level.steps} generation{(level.steps == 1 ? "" : "s")})", stSmall);

                // top right: seeds or generation, then progress
                bool placingHud = phase == Phase.Place;
                string right = placingHud
                    ? (fetching ? $"Seeds {seeds.Count} / {level.seeds}   in hand {carried}" : $"Seeds {seeds.Count} / {level.seeds}")
                    : $"Generation {sim.Generation} / {level.steps}";
                GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 16f * s, Screen.width * 0.6f, 40f * s), right, stH1Right);
                string sub;
                bool onLiving = sim.Cells[hero.y * level.w + hero.x] == Sim.Alive;
                if (!placingHud) sub = $"match {Mathf.FloorToInt(match * 100f)}%   attempt {attempts}";
                else if (onLiving) sub = "you stand on living ground: step off before you grow";
                else if (fetching && carried == 0 && seeds.Count < level.seeds) sub = pickupsLeft.Count > 0 ? "the seeds lie somewhere in the dark: find them" : "";
                else if (attempts > 0) sub = $"attempt {attempts + 1}";
                else sub = "walk to where it began, then press Space";
                sub += $"   lanterns {lives}";
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
                case Phase.Story:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, 0.82f));
                    float cy = Screen.height * 0.30f;
                    GUI.Label(new Rect(0, cy - 40f * s, Screen.width, 26f * s), $"chapter {levelIndex + 1} of {set.levels.Length}", stSmallCentre);
                    GUI.Label(new Rect(0, cy - 10f * s, Screen.width, 76f * s), level.name, stBanner);
                    GUI.Label(new Rect(Screen.width * 0.2f, cy + 76f * s, Screen.width * 0.6f, 160f * s), level.intro, stBody);
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Space to wake", stBody);
                    GUI.color = old;
                    break;
                }
                case Phase.Dead:
                {
                    float a = Mathf.Clamp01((Time.time - deathAt) * 3f);
                    Panel(new Rect(0, Screen.height * 0.40f, Screen.width, 130f * s), new Color(0.12f, 0.03f, 0.04f, 0.82f * a));
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, a);
                    GUI.Label(new Rect(0, Screen.height * 0.40f, Screen.width, 76f * s), deathText, stBanner);
                    GUI.Label(new Rect(0, Screen.height * 0.40f + 72f * s, Screen.width, 40f * s),
                        lives > 0 ? $"a lantern gutters out: {lives} left" : "your last lantern is out", stBody);
                    GUI.color = old;
                    break;
                }
                case Phase.GameOver:
                {
                    Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.06f, 0.02f, 0.03f, 0.9f));
                    float cy = Screen.height * 0.34f;
                    GUI.Label(new Rect(0, cy - 60f * s, Screen.width, 100f * s), "THE DARK TOOK YOU", stTitle);
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 50f * s, Screen.width * 0.7f, 80f * s),
                        $"Every lantern is out in chapter {levelIndex + 1}, {level.name}. Light them again and try once more.", stBody);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "Enter or click to try again   -   Esc for the menu", stBody);
                    break;
                }
                case Phase.Result:
                {
                    // a failed result fades away after a moment so the mismatch underneath can be studied
                    float a = Mathf.Clamp01(resultTime * 4f);
                    if (!won) a *= 1f - Mathf.Clamp01((resultTime - 2.4f) / 0.6f);
                    if (a <= 0f) break;
                    bool story = won && !string.IsNullOrEmpty(level.outro);
                    float panelH = story ? 190f * s : 140f * s;
                    Panel(new Rect(0, Screen.height * 0.38f, Screen.width, panelH), new Color(0.04f, 0.05f, 0.07f, 0.8f * a));
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, a);
                    string text = won ? "ORIGIN FOUND" : $"{Mathf.FloorToInt(match * 100f)}% match";
                    GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, 76f * s), text, stBanner);
                    float ly = Screen.height * 0.38f + 72f * s;
                    if (story)
                    {
                        GUI.Label(new Rect(Screen.width * 0.15f, ly, Screen.width * 0.7f, 52f * s), level.outro, stBody);
                        ly += 54f * s;
                    }
                    string detail = won
                        ? (attempts == 1 ? "first try" : $"on attempt {attempts}") + "   -   Enter or click outside the world for the next one"
                        : $"{missing} missing, {extra} astray   -   R to rewind, then move your seeds";
                    GUI.Label(new Rect(0, ly, Screen.width, 40f * s), detail, stSmallCentre);
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
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 50f * s, Screen.width * 0.7f, 90f * s), Epilogue, stBody);
                    GUI.Label(new Rect(Screen.width * 0.15f, cy + 150f * s, Screen.width * 0.7f, 40f * s),
                        $"{done} of {set.levels.Length} origins found" + (skipped > 0 ? $", {skipped} skipped this run." : "."), stSmallCentre);
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Space for the menu", stBody);
                    break;
                }
            }
        }
    }
}
