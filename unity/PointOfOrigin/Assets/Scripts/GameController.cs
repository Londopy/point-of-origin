using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointOfOrigin
{
    /// <summary>
    /// The whole game as a side-scrolling platformer. Loads levels.json, drives
    /// the Odin simulation through Sim, paints the world into one point-filtered
    /// texture on a sprite, moves the wanderer with tile collision, and draws the
    /// HUD with IMGUI. Living growth is solid ground and the door opens only when
    /// the growth is exact.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        enum Phase { Title, Story, Play, Growing, Result, Dead, GameOver, Finished }

        /// <summary>A page drawn over whatever phase is running; the world pauses while one is open.</summary>
        enum Overlay { None, Menu, Help, Achievements, Settings, Controls, Credits, Wall, Wanderer }

        /// <summary>
        /// The first chapter walks the player through the loop, one prompt at a time, keyed to what they
        /// have done: plant somewhere, see the red and the gold, rewind, take the seed back, reason out the
        /// origin, plant there, grow, cross.
        /// </summary>
        enum Tutorial { Off, Move, Jump, FindOutline, PlantAnywhere, GetClear, GrowFirst, SeeResult, RewindIt, TakeBack, Reason, PlantRight, GetClear2, GrowRight, Cross }

        class Ember
        {
            public Vector2Int cell;
            public Vector2Int dir;
            public Vector2 pos;      // grid coordinates
            public SpriteRenderer sr;
            public bool hunter;      // turns toward the wanderer whenever they share its row
        }

        const int CellPx = 8;
        const float StepInterval = 0.26f;     // seconds per generation while growing
        const float WinThreshold = 0.999f;
        const int RevealAfter = 2;            // failed attempts before Reveal is offered
        const float PopSeconds = 0.2f;
        const float AfterglowSeconds = 0.4f;
        const float LanternRadius = 6f;
        const int StartLives = 3;
        const float DeathSeconds = 1.4f;
        const float EmberSpeed = 2.9f;        // cells per second
        const float Gravity = 30f;
        const float JumpVelocity = 11.5f;
        const float RunSpeed = 7f;
        const float CoyoteTime = 0.1f;
        const float JumpBufferTime = 0.12f;
        const float PlayerW = 0.66f;
        const float PlayerH = 0.92f;
        const float MaxDt = 1f / 30f;
        const string KeyUnlocked = "po.unlocked";
        const string KeySolved = "po.solved";
        const string KeyMuted = "po.muted";
        const string KeyVolume = "po.volume";
        const string KeyMusic = "po.music";
        const string KeyShake = "po.shake";
        const int VolumeSteps = 10;
        const string Epilogue =
            "Every origin found. The world grows again, and you, who were its last seed, walk on in the light.";

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static readonly Color32 ColBg = Hex("0a0c12");
        static readonly Color32 ColBgCentre = Hex("161c2c");
        static readonly Color32 ColBgEdge = Hex("06070b");
        static readonly Color32 ColGap = Hex("05060a");
        static readonly Color32 ColOpen = Hex("10131c");
        static readonly Color32 ColOpenEdge = Hex("141826");
        static readonly Color32 ColRock = Hex("3a4150");
        static readonly Color32 ColRockEdge = Hex("262b37");
        static readonly Color32 ColRockLight = Hex("4d5566");
        static readonly Color32 ColGhost = Hex("2a6d74");
        static readonly Color32 ColGhostBright = Hex("4fb4bd");
        static readonly Color32 ColSeed = Hex("6fe3ff");
        static readonly Color32 ColSeedEdge = Hex("b9f3ff");
        static readonly Color32 ColWrong = Hex("b8364a");
        static readonly Color32 ColWrongEdge = Hex("e0566a");
        static readonly Color32 ColAfter = Hex("4d3a3c");
        static readonly Color32 ColReveal = Hex("ff5fd2");
        static readonly Color32 ColUnknown = Hex("090b11");
        static readonly Color32 ColUnknownEdge = Hex("0b0d14");
        static readonly Color32 ColRockDark = Hex("161a22");
        static readonly Color32 ColLamp = Hex("ffd9a0");
        static readonly Color32 ColHero = Hex("fff6dc");
        static readonly Color32 ColHeroEdge = Hex("ffb757");
        static readonly Color32 ColEmber = Hex("ff5a1f");
        static readonly Color32 ColEmberCore = Hex("ffe2a8");
        static readonly Color32 ColDoorClosed = Hex("2c4a3f");
        static readonly Color32 ColDoorOpen = Hex("7cf5b0");
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
        readonly List<Vector2Int> pickupsLeft = new List<Vector2Int>();
        readonly List<SpriteRenderer> pickupSprites = new List<SpriteRenderer>();
        int carried;
        bool fetching;
        readonly List<Ember> embers = new List<Ember>();
        Vector2Int exitCell = new Vector2Int(-1, -1);
        bool[] seen;
        float[] seenAt;
        bool allSeen;

        // the wanderer: bottom-centre position and velocity in world units (one cell = one unit, y up)
        Vector2 pPos;
        Vector2 pVel;
        bool grounded;
        float coyote;
        float jumpBuffer;
        bool facingRight = true;
        Vector2Int lastRevealCell = new Vector2Int(-1, -1);
        int lives = StartLives;
        float deathAt;
        string deathText = "";

        Texture2D tex;
        Color32[] px;
        SpriteRenderer worldSr;
        SpriteRenderer bgSr;
        SpriteRenderer farSr;    // Houdini-generated ruin skylines, parallax
        SpriteRenderer nearSr;
        // the stone slab from the game's first, isometric version, as a far plateau behind the skylines
        MeshRenderer slabMr;
        Vector3 slabOffset = new Vector3(7.5f, 2f, 8f);    // centred on the camera at mid-level, its top face a band above the ground line, behind the skylines
        Vector3 slabEuler = new Vector3(-58f, 0f, 0f);     // tilted so the top face shows, like a mesa seen from the road
        Vector3 slabScale = new Vector3(6f, 1f, 0.7f);     // wide, thin, shallow
        float slabParallax = 0.85f;
        float slabBright = 2.2f;
        Vector3 slabTint = new Vector3(0.9f, 1.0f, 1.35f);
        bool slabFlip = true;                              // Houdini winds the other way round
        Sprite[] heroFrames;     // Blender-rendered wanderer, when present
        float animClock;
        readonly List<Vector2[]> burstFrames = new List<Vector2[]>();   // Houdini-simulated growth burst
        readonly List<(Vector2Int cell, float t0)> bursts = new List<(Vector2Int, float)>();
        SpriteRenderer playerSr;
        SpriteRenderer glowSr;
        Sprite emberSprite;
        Sprite seedSprite;
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
        int volume = VolumeSteps;
        int music = VolumeSteps;
        bool shakeOn = true;
        Overlay overlay = Overlay.None;
        bool overlayFromMenu;      // a page opened from the pause menu goes back there, not to the title
        GameAction? listening;     // the Controls page is waiting for a key for this action
        bool confirmReset;
        float[] grain;             // Houdini-generated stone grain, tileable, replayed onto the rock
        int grainN;
        Tutorial tut = Tutorial.Off;
        float tutTimer;
        int jumps;
        // hazards: cells the automaton sees as rock (acid, spouts, crumble) or air (spikes), with their own physics
        byte[] phys;                                     // what the wanderer collides with: rock minus acid
        readonly HashSet<int> acid = new HashSet<int>();
        readonly HashSet<int> spikes = new HashSet<int>();
        readonly List<Vector2Int> spouts = new List<Vector2Int>();
        readonly List<float> spoutPhase = new List<float>();
        readonly HashSet<int> crumble = new HashSet<int>();
        readonly Dictionary<int, float> crumbleAt = new Dictionary<int, float>();   // cell -> when it was stepped on
        const float SpoutPeriod = 2.8f;
        const float SpoutOn = 1.1f;
        const float SpoutWarn = 0.6f;
        const int SpoutHeight = 2;
        const float CrumbleDelay = 0.45f;
        const float CrumbleGone = 2.8f;
        static readonly Color32 ColAcid = Hex("2f8a3a");
        static readonly Color32 ColAcidBright = Hex("8ff05a");
        static readonly Color32 ColSpike = Hex("9aa3b5");
        static readonly Color32 ColFlame = Hex("ff7a1f");
        static readonly Color32 ColFlameCore = Hex("fff0a8");
        static readonly Color32 ColCrack = Hex("141720");
        // the lantern's voice: a queue of lines shown in a bubble by the wanderer, typed out with blips
        readonly Queue<string> voiceQueue = new Queue<string>();
        string voiceLine = "";
        float voiceAt = -10f;
        int voiceShown;
        const float VoiceCharsPerSecond = 38f;
        const float VoiceHold = 2.6f;
        readonly HashSet<string> voiceSaid = new HashSet<string>();   // triggers already used this chapter
        static readonly string[] DeathQuips =
        {
            "Again. Slower, this time.", "The dark keeps what it takes. Not you, not yet.", "I have the light. You have the legs. Try once more.",
            "That was the wrong way to learn it, but you learned it.", "Everything that grew here died once too.",
        };
        int deathQuip;
        // gamepad: a focused button so a pad can drive every menu
        int padFocus = -1;
        Vector2 lastMouse;
        float spoutHeard;
        float lastBubble;
        // achievements and the secret
        Vector2Int secretCell = new Vector2Int(-1, -1);
        bool secretFound;
        int rewinds;
        float levelStartAt;
        string toastTitle = "";
        float toastAt = -10f;
        const float ToastSeconds = 4.5f;
        const string KeySecret = "po.secret";
        const string KeyReveals = "po.reveals";
        // the wall in the chamber: a word the spark law took once, a key to read it by, and the initials of those who did
        const string WallKeyWord = "ORIGIN";
        const string WallAnswerHash = "67000ce4110bd2f831f87e603af0c1b68b46e89e45e771b44e25e5444f1bdb5f";
        const string WallAnswerLength = "REMEMBER";   // only its length is used on screen; the check is the hash
        string wallTyped = "";
        string wallInitials = "";
        string wallUser = "";
        string wallNote = "";
        int wallStage;                                  // 0 read it, 1 solved: press your initials, 2 pressed: share it
        List<Vector2Int> wallKeyPlain, wallKeySparked, wallCipher;
        int wallKeyW, wallCipherW;
        readonly List<string> fossilLog = new List<string>();   // where the lettered fossils went, for the checks
        // the background: fossils of older growths pressed into the rock, and a few spores drifting in the air
        int[] fossilPx = new int[0];         // pixel index in the world texture
        int[] fossilCell = new int[0];       // the rock cell that pixel belongs to (drawn only once seen)
        Color32[] fossilColor = new Color32[0];
        readonly List<SpriteRenderer> spores = new List<SpriteRenderer>();
        readonly List<Vector3> sporeState = new List<Vector3>();   // x, y, phase
        const int SporeCount = 9;
        static readonly Color32 ColBone = Hex("b8a98a");
        float demoTimer;
        int demoStage;
        string fatal;

        readonly List<Button> buttons = new List<Button>();
        GUIStyle stTitle, stH1, stH1Right, stSmall, stSmallRight, stSmallCentre, stHint, stButton, stBanner, stBody, stRow, stRowValue, stVoice;
        Texture2D panelTex;
        int styledHeight;

        bool CanReveal => attempts >= RevealAfter && !revealed && (phase == Phase.Play || phase == Phase.Result);

        /// <summary>The Reveal button is always there, so nobody wonders where it went; it says what it is waiting for.</summary>
        string RevealLabel()
        {
            if (revealed) return "Revealed";
            if (CanReveal) return $"Reveal  [{L(GameAction.Reveal)}]";
            int left = RevealAfter - attempts;
            return left == 1 ? "Reveal  (1 more try)" : $"Reveal  ({left} tries)";
        }
        bool Solved(int i) => (solvedMask & (1 << i)) != 0;
        bool InWorld => phase == Phase.Play || phase == Phase.Growing || phase == Phase.Result;
        bool Paused => overlay != Overlay.None;
        Vector2 PlayerCentre => new Vector2(pPos.x, pPos.y + PlayerH / 2f);
        static string L(GameAction a) => InputBridge.Label(a);

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
            cam.transform.rotation = Quaternion.identity;
            camBase = new Vector3(0f, 0f, -10f);
            cam.transform.position = camBase;
            var extraData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extraData != null) extraData.renderPostProcessing = false;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            Material Unlit() => shader != null ? new Material(shader) : null;

            var back = new GameObject("Background");
            bgSr = back.AddComponent<SpriteRenderer>();
            bgSr.sortingOrder = -10;
            bgSr.sprite = Sprite.Create(MakeVignette(128), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            if (shader != null) bgSr.sharedMaterial = Unlit();

            var world = new GameObject("World");
            worldSr = world.AddComponent<SpriteRenderer>();
            worldSr.sortingOrder = 0;
            if (shader != null) worldSr.sharedMaterial = Unlit();

            var glow = new GameObject("Lantern");
            glowSr = glow.AddComponent<SpriteRenderer>();
            glowSr.sortingOrder = 5;
            glowSr.sprite = Sprite.Create(MakeGlow(64), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 6f);
            glowSr.color = new Color(1f, 0.85f, 0.6f, 0.28f);
            if (shader != null) glowSr.sharedMaterial = Unlit();

            var player = new GameObject("Wanderer");
            playerSr = player.AddComponent<SpriteRenderer>();
            playerSr.sortingOrder = 10;
            playerSr.sprite = Sprite.Create(MakePlayerTexture(), new Rect(0, 0, 6, 8), new Vector2(0.5f, 0f), CellPx);
            if (shader != null) playerSr.sharedMaterial = Unlit();

            emberSprite = Sprite.Create(MakeDot(6, ColEmber, ColEmberCore), new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), CellPx);
            seedSprite = Sprite.Create(MakeDot(5, ColSeed, ColSeedEdge), new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), CellPx);

            // the ember and the seed rendered in Blender, when the PNGs shipped (sized to 0.8 and 0.95 of a cell)
            var emberTex = Resources.Load<Texture2D>("Sprites/ember");
            if (emberTex != null)
                emberSprite = Sprite.Create(emberTex, new Rect(0, 0, emberTex.width, emberTex.height), new Vector2(0.5f, 0.5f), emberTex.height / 0.8f);
            var seedTex = Resources.Load<Texture2D>("Sprites/seed");
            if (seedTex != null)
                seedSprite = Sprite.Create(seedTex, new Rect(0, 0, seedTex.width, seedTex.height), new Vector2(0.5f, 0.5f), seedTex.height / 0.95f);

            // the stone grain Houdini generated: one tileable square of values in 0..1
            var grainText = Resources.Load<TextAsset>("Backdrop/grain");
            if (grainText != null)
            {
                var values = ParseFloats(grainText.text.Replace("size:", " "));
                int n = values.Count > 2 ? (int)values[0] : 0;
                if (n > 0 && values.Count >= 2 + n * n)
                {
                    grainN = n;
                    grain = new float[n * n];
                    for (int i = 0; i < grain.Length; i++) grain[i] = values[i + 2];
                }
            }

            // the wanderer rendered in Blender, if the frames shipped; otherwise the pixel figure above
            heroBase0 = Resources.Load<Texture2D>("Sprites/wanderer_0");
            heroBase1 = Resources.Load<Texture2D>("Sprites/wanderer_1");
            ApplyLook();

            // the ruin skylines Houdini generated: two parallax layers behind the world
            var sky = Resources.Load<TextAsset>("Backdrop/skyline");
            if (sky != null)
            {
                foreach (var line in sky.text.Split('\n'))
                {
                    var parts = line.Trim().Split(':');
                    if (parts.Length != 2) continue;
                    var heights = ParseFloats(parts[1]);
                    if (heights.Count < 8) continue;
                    bool far = parts[0].Trim() == "far";
                    var sr = MakeSkyline(heights, far ? Hex("0f1322") : Hex("151a2d"), far ? -9 : -8);
                    if (far) farSr = sr; else nearSr = sr;
                }
            }

            MakeSlab();

            // the growth burst Houdini simulated: one frame per line, x y pairs in cells
            var burst = Resources.Load<TextAsset>("Backdrop/burst");
            if (burst != null)
            {
                foreach (var line in burst.text.Split('\n'))
                {
                    var values = ParseFloats(line);
                    if (values.Count < 2) continue;
                    var frame = new Vector2[values.Count / 2];
                    for (int i = 0; i < frame.Length; i++) frame[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
                    burstFrames.Add(frame);
                }
            }

            Wall.Load();
            StartCoroutine(Wall.Fetch());
            unlocked = PlayerPrefs.GetInt(KeyUnlocked, 0);
            solvedMask = PlayerPrefs.GetInt(KeySolved, 0);
            muted = PlayerPrefs.GetInt(KeyMuted, 0) != 0;
            volume = Mathf.Clamp(PlayerPrefs.GetInt(KeyVolume, VolumeSteps), 0, VolumeSteps);
            music = Mathf.Clamp(PlayerPrefs.GetInt(KeyMusic, VolumeSteps), 0, VolumeSteps);
            shakeOn = PlayerPrefs.GetInt(KeyShake, 1) != 0;
            InputBridge.Load();
            sfx.SetMasterVolume(volume / (float)VolumeSteps);
            sfx.SetMusicVolume(music / (float)VolumeSteps);
            sfx.SetMuted(muted);
            sfx.StartAmbient();

            try
            {
                Debug.Log($"Point of Origin: origin_sim.dll version {Sim.NativeVersion}");
                set = Levels.Load();
                unlocked = Mathf.Clamp(unlocked, 0, set.levels.Length - 1);
                ApplyLook();   // now that the save is known, a locked choice from a wiped save falls back
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
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.25f);
                    float k = d * d * (3f - 2f * d);
                    pixels[y * size + x] = Color32.Lerp(ColBgCentre, ColBgEdge, k);
                }
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        static Texture2D MakeGlow(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - d) * (1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        static Texture2D MakePlayerTexture()
        {
            var t = new Texture2D(6, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[6 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            for (int y = 0; y < 5; y++) for (int x = 1; x < 5; x++) pixels[y * 6 + x] = ColHero;      // body
            for (int y = 5; y < 8; y++) for (int x = 1; x < 5; x++) pixels[y * 6 + x] = ColHeroEdge;  // head
            pixels[3 * 6 + 5] = ColLamp;                                                              // the lantern
            pixels[2 * 6 + 5] = ColLamp;
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        static List<float> ParseFloats(string text)
        {
            var list = new List<float>();
            foreach (var tok in text.Split(new[] { ' ', '\t', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            return list;
        }

        /// <summary>A silhouette texture from a height profile (one sample per cell), filled below the profile.</summary>
        /// <summary>
        /// The Houdini diorama base (Resources/Models/DioramaBase.txt, one vertex per line: x y z r g b, three
        /// lines a triangle) as a mesh far behind the skylines: a plateau under the ruins' horizon, drawn
        /// with the flat vertex-colour shader. Its baked colours are very dark, so they are lifted and cooled.
        /// </summary>
        void MakeSlab()
        {
            var text = Resources.Load<TextAsset>("Models/DioramaBase");
            var slabShader = Resources.Load<Shader>("Shaders/VertexColor");
            if (text == null || slabShader == null) return;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            foreach (var line in text.text.Split('\n'))
            {
                var f = ParseFloats(line.Trim());
                if (f.Count < 6) continue;
                verts.Add(new Vector3(f[0], f[1], f[2]));
                cols.Add(new Color(Mathf.Clamp01(f[3] * slabBright * slabTint.x), Mathf.Clamp01(f[4] * slabBright * slabTint.y), Mathf.Clamp01(f[5] * slabBright * slabTint.z), 1f));
            }
            int n = verts.Count - verts.Count % 3;
            if (n < 3) return;
            var tris = new int[n];
            for (int i = 0; i < n; i += 3)
            {
                tris[i] = i;
                tris[i + 1] = slabFlip ? i + 2 : i + 1;
                tris[i + 2] = slabFlip ? i + 1 : i + 2;
            }
            var mesh = new Mesh { name = "Slab", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (slabMr != null) Destroy(slabMr.gameObject);
            var go = new GameObject("Slab");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            slabMr = go.AddComponent<MeshRenderer>();
            slabMr.sharedMaterial = new Material(slabShader);
            slabMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slabMr.receiveShadows = false;
            slabMr.sortingOrder = -9;
        }

        SpriteRenderer MakeSkyline(List<float> heights, Color32 color, int order)
        {
            int w = heights.Count * CellPx;
            float maxH = 0f;
            foreach (var h in heights) maxH = Mathf.Max(maxH, h);
            int hPx = Mathf.CeilToInt(maxH * CellPx) + CellPx;
            var t = new Texture2D(w, hPx, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * hPx];
            var clear = new Color32(0, 0, 0, 0);
            for (int x = 0; x < w; x++)
            {
                int top = Mathf.RoundToInt(heights[x / CellPx] * CellPx);
                for (int y = 0; y < hPx; y++) pixels[y * w + x] = y < top ? color : clear;
            }
            t.SetPixels32(pixels);
            t.Apply(false);
            var go = new GameObject(order == -9 ? "Skyline far" : "Skyline near");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(t, new Rect(0, 0, w, hPx), Vector2.zero, CellPx);
            sr.sortingOrder = order;
            sr.sharedMaterial = worldSr.sharedMaterial;
            return sr;
        }

        static Texture2D MakeDot(int size, Color32 rim, Color32 core)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    pixels[y * size + x] = d > 1f ? new Color32(0, 0, 0, 0) : d < 0.5f ? core : rim;
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
            // hazards: acid is rock to the automaton but nothing to stand on; spikes are air to both
            phys = (byte[])rock.Clone();
            acid.Clear(); spikes.Clear(); spouts.Clear(); spoutPhase.Clear(); crumble.Clear(); crumbleAt.Clear();
            foreach (var c in level.AcidCells()) { int i = c.y * level.w + c.x; acid.Add(i); phys[i] = Sim.Dead; }
            foreach (var c in level.SpikeCells()) spikes.Add(c.y * level.w + c.x);
            foreach (var c in level.CrumbleCells()) crumble.Add(c.y * level.w + c.x);
            var srng = new System.Random(index * 131 + 7);
            foreach (var c in level.SpoutCells()) { spouts.Add(c); spoutPhase.Add((float)srng.NextDouble() * SpoutPeriod); }
            prevCells = new byte[n];
            bornAt = new float[n];
            diedAt = new float[n];
            for (int i = 0; i < n; i++) { bornAt[i] = -10f; diedAt[i] = -10f; }
            seen = new bool[n];
            seenAt = new float[n];
            allSeen = false;
            seeds.Clear();
            answer = new HashSet<Vector2Int>(level.OriginCells());
            pickupsLeft.Clear();
            pickupsLeft.AddRange(level.PickupCells());
            fetching = pickupsLeft.Count > 0;
            carried = fetching ? 0 : level.seeds;
            SpawnEmbers();
            foreach (var s in pickupSprites) if (s != null) Destroy(s.gameObject);
            pickupSprites.Clear();
            foreach (var k in pickupsLeft)
            {
                var go = new GameObject("Seed");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = seedSprite;
                sr.sortingOrder = 7;
                sr.sharedMaterial = playerSr.sharedMaterial;
                pickupSprites.Add(sr);
            }
            var ex = level.ExitCell();
            exitCell = ex ?? new Vector2Int(-1, -1);
            secretCell = level.SecretCell() ?? new Vector2Int(-1, -1);
            secretFound = PlayerPrefs.GetInt(KeySecret, 0) != 0;
            glowSr.color = secretFound ? new Color(0.78f, 1f, 0.72f, 0.3f) : new Color(1f, 0.85f, 0.6f, 0.28f);
            rewinds = 0;
            attempts = 0;
            revealed = false;
            won = false;
            match = 0f;
            missing = 0;
            extra = 0;
            stepTimer = 0f;
            winAt = -10f;

            var start = level.StartCell() ?? new Vector2Int(level.w / 2, level.h / 2);
            pPos = new Vector2(start.x + 0.5f, WorldY(start.y));
            pVel = Vector2.zero;
            grounded = false;
            coyote = 0f;
            jumpBuffer = 0f;
            lastRevealCell = new Vector2Int(-1, -1);
            RevealAround(start);

            int tw = level.w * CellPx, th = level.h * CellPx;
            if (tex == null || tex.width != tw || tex.height != th)
            {
                if (worldSr.sprite != null) Destroy(worldSr.sprite);
                if (tex != null) Destroy(tex);
                tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                px = new Color32[tw * th];
                worldSr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, CellPx);
            }
            TrackChanges();
            MakeFossils();
            SnapCamera();
            Paint();
        }

        // ------------------------------------------------------------------ background

        /// <summary>
        /// Three or four fossils per chapter: growths of the game's own laws, grown
        /// by the same Odin simulation, pressed into the rock at half scale as pale
        /// eroded imprints with a sediment shadow. They show only where the stone
        /// has been seen. The ruins of older origins.
        /// </summary>
        void MakeFossils()
        {
            var rng = new System.Random(levelIndex * 7919 + 17);
            int count = 3 + rng.Next(2);
            fossilLog.Clear();
            // on the second chapter the wall's initials follow the signature, each pressed in clean
            var wallNames = new List<string>();
            if (levelIndex == 1) foreach (var n in Wall.Names) if (n != "LC") wallNames.Add(n);
            count += wallNames.Count;
            // dense laws only: a bloom slab, a coral, a shell ring; sparse echoes just look like scattered tiles
            (uint birth, uint survive, int steps)[] laws =
            {
                (Mask(1, 2, 3, 4), Mask(0, 1, 2, 3, 4, 5, 6, 7, 8), 2 + rng.Next(2)),
                (Mask(1), Mask(1, 2, 3, 4, 5, 6, 7, 8), 4 + rng.Next(3)),
                (Mask(1), Mask(1, 2, 3, 4, 5, 6, 7, 8), 3 + rng.Next(2)),
                (Mask(1), 0u, 1),
                (Mask(1, 2), Mask(0, 1, 2, 3, 4, 5, 6, 7, 8), 3 + rng.Next(2)),
            };
            const int N = 20, Px = 4;   // a fossil cell is four pixels: half a world cell
            int tw = level.w * CellPx;
            var pxList = new List<int>();
            var cellList = new List<int>();
            var colList = new List<Color32>();
            var used = new HashSet<int>();
            bool IsRock(int gx, int gy) => gx >= 0 && gy >= 0 && gx < level.w && gy < level.h && rock[gy * level.w + gx] == Sim.Rock;

            for (int k = 0; k < count; k++)
            {
                // on the second chapter the signature and then everyone's initials are pressed into the rock near the door
                int randomCount = count - wallNames.Count;
                bool signature = levelIndex == 1 && k >= randomCount - 1;
                string letters = signature ? (k == randomCount - 1 ? "LC" : wallNames[k - randomCount]) : null;
                var law = laws[rng.Next(laws.Length)];
                var alive = new List<Vector2Int>();
                if (signature) foreach (var c in PixelFont.Cells(letters)) alive.Add(c + new Vector2Int(2, 2));
                else using (var fs = new Sim(N, N))
                {
                    fs.SetRule(law.birth, law.survive);
                    fs.Set(N / 2, N / 2, Sim.Alive);
                    if (rng.Next(3) == 0) fs.Set(N / 2 + 2 + rng.Next(2), N / 2 + rng.Next(3) - 1, Sim.Alive);
                    fs.Step(law.steps);
                    fs.Refresh();
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                            if (fs.Cells[y * N + x] == Sim.Alive) alive.Add(new Vector2Int(x, y));
                }
                if (alive.Count < 3) continue;
                int minx = N, maxx = 0, miny = N, maxy = 0;
                foreach (var a in alive) { minx = Mathf.Min(minx, a.x); maxx = Mathf.Max(maxx, a.x); miny = Mathf.Min(miny, a.y); maxy = Mathf.Max(maxy, a.y); }
                int cw = (maxx - minx + 2) / 2, ch = (maxy - miny + 2) / 2;   // world cells covered

                // somewhere fully inside the stone: every covered cell rock, a rock roof above, nothing shared with another fossil
                int gx = -1, gy = -1;
                int xLo = 1, xHi = Mathf.Max(1, level.w - cw - 2);
                if (signature) xLo = Mathf.Max(1, level.w - (letters == "LC" ? 28 : 60));
                for (int attempt = 0; attempt < 300 && gx < 0; attempt++)
                {
                    int tx = xLo + rng.Next(Mathf.Max(1, xHi - xLo));
                    int ty = 2 + rng.Next(Mathf.Max(1, level.h - ch - 2));
                    bool ok = true;
                    for (int dy = -1; dy <= ch && ok; dy++)
                        for (int dx = -1; dx <= cw && ok; dx++)
                        {
                            if (!IsRock(tx + dx, ty + dy)) ok = false;
                            else if (used.Contains((ty + dy) * level.w + tx + dx)) ok = false;
                        }
                    if (ok) { gx = tx; gy = ty; }
                }
                if (signature) fossilLog.Add(gx < 0 ? $"{letters}: no room" : $"{letters} at {gx},{gy}");
                if (gx < 0) continue;
                for (int dy = -1; dy <= ch; dy++)
                    for (int dx = -1; dx <= cw; dx++)
                        used.Add((gy + dy) * level.w + gx + dx);

                int x0 = gx * CellPx;
                int yTop = (level.h - gy) * CellPx - 1;   // top pixel row of grid row gy, in bottom-up texture rows
                var kept = new HashSet<Vector2Int>();
                foreach (var a in alive) if (signature || rng.NextDouble() >= 0.15) kept.Add(a);   // the rest eroded away
                void Put(int ix, int iy, float toBone, float toDark)
                {
                    if (ix < 0 || iy < 0 || ix >= tw || iy >= level.h * CellPx) return;
                    float g = grain != null ? grain[(iy % grainN) * grainN + (ix % grainN)] : 0.5f;
                    var stone = Color32.Lerp(ColRockEdge, ColRockLight, g);
                    var col = toDark > 0f ? Color32.Lerp(stone, new Color32(6, 7, 10, 255), toDark) : Color32.Lerp(stone, ColBone, toBone);
                    pxList.Add(iy * tw + ix);
                    cellList.Add((level.h - 1 - iy / CellPx) * level.w + ix / CellPx);
                    colList.Add(col);
                }
                // the sediment bed: a dark rim one pixel outside every open edge of the shape
                foreach (var a in kept)
                {
                    int fx = (a.x - minx) * Px, fy = (a.y - miny) * Px;
                    if (!kept.Contains(a + Vector2Int.left)) for (int d = -1; d <= Px; d++) Put(x0 + fx - 1, yTop - (fy + d), 0f, 0.45f);
                    if (!kept.Contains(a + Vector2Int.right)) for (int d = -1; d <= Px; d++) Put(x0 + fx + Px, yTop - (fy + d), 0f, 0.45f);
                    if (!kept.Contains(a + Vector2Int.down)) for (int d = -1; d <= Px; d++) Put(x0 + fx + d, yTop - (fy - 1), 0f, 0.45f);
                    if (!kept.Contains(a + Vector2Int.up)) for (int d = -1; d <= Px; d++) Put(x0 + fx + d, yTop - (fy + Px), 0f, 0.45f);
                }
                // the imprint itself: pale bone, lighter at the top-left, a shadow along the bottom and right, chipped here and there
                foreach (var a in kept)
                {
                    int fx = (a.x - minx) * Px, fy = (a.y - miny) * Px;
                    for (int dy = 0; dy < Px; dy++)
                        for (int dx = 0; dx < Px; dx++)
                        {
                            bool shadow = dx == Px - 1 || dy == Px - 1;
                            if (shadow && !(kept.Contains(a + Vector2Int.right) && dx == Px - 1 && dy < Px - 1) && !(kept.Contains(a + Vector2Int.up) && dy == Px - 1 && dx < Px - 1))
                            {
                                Put(x0 + fx + dx, yTop - (fy + dy), 0f, 0.3f);
                                continue;
                            }
                            if (!signature && rng.NextDouble() < 0.1) continue;   // chipped
                            float pale = dx == 0 || dy == 0 ? 0.7f : 0.55f;
                            Put(x0 + fx + dx, yTop - (fy + dy), pale, 0f);
                        }
                }
                // lettering joins at the corners too (the bowl of a B, the top of a C): bridge each corner-only touch with a pale 2x2
                if (signature)
                    foreach (var a in kept)
                        foreach (var d in new[] { new Vector2Int(1, 1), new Vector2Int(1, -1) })
                        {
                            if (!kept.Contains(a + d) || kept.Contains(a + new Vector2Int(d.x, 0)) || kept.Contains(a + new Vector2Int(0, d.y))) continue;
                            int fx = (a.x - minx) * Px, fy = (a.y - miny) * Px;
                            int cx = d.x > 0 ? fx + Px - 1 : fx, cy = d.y > 0 ? fy + Px - 1 : fy;
                            for (int oy = 0; oy <= 1; oy++)
                                for (int ox = 0; ox <= 1; ox++)
                                    Put(x0 + cx + ox * d.x, yTop - (cy + oy * d.y), 0.7f, 0f);
                        }
            }
            fossilPx = pxList.ToArray();
            fossilCell = cellList.ToArray();
            fossilColor = colList.ToArray();
        }

        // ------------------------------------------------------------------ the wall

        /// <summary>What one generation of the spark law makes of some lettering: the cipher and the key, computed by the same simulation.</summary>
        static List<Vector2Int> Sparked(string text, out int width)
        {
            var plain = PixelFont.Cells(text, 3);
            width = PixelFont.Width(text, 3) + 4;
            int h = PixelFont.Height + 4;
            var cells = new List<Vector2Int>();
            using (var fs = new Sim(width, h))
            {
                fs.SetRule(Mask(1), 0u);
                foreach (var c in plain) fs.Set(c.x + 2, c.y + 2, Sim.Alive);
                fs.Step(1);
                fs.Refresh();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < width; x++)
                        if (fs.Cells[y * width + x] == Sim.Alive) cells.Add(new Vector2Int(x, y));
            }
            return cells;
        }

        void OpenWall()
        {
            if (wallCipher == null)
            {
                wallKeyPlain = PixelFont.Cells(WallKeyWord, 3);
                for (int i = 0; i < wallKeyPlain.Count; i++) wallKeyPlain[i] += new Vector2Int(2, 2);
                wallKeySparked = Sparked(WallKeyWord, out wallKeyW);
                wallCipher = Sparked(WallAnswerLength, out wallCipherW);
            }
            wallTyped = "";
            wallInitials = Wall.Mine;
            wallUser = Wall.User;
            wallNote = "";
            wallStage = Wall.Solved && Wall.Word.Length > 0 ? (Wall.Mine.Length > 0 ? 2 : 1) : 0;
            OpenPage(Overlay.Wall);
        }

        void WallSubmitAnswer()
        {
            var answer = wallTyped.Trim().ToLowerInvariant();
            if (answer.Length == 0) return;
            if (Wall.AnswerHash(answer) == WallAnswerHash)
            {
                Wall.MarkSolved(answer);
                wallStage = 1;
                wallNote = "That is what it says. Now press your own letters into it: up to three.";
                sfx.Success();
                sfx.DoorChime();
            }
            else
            {
                wallNote = "The wall does not say that. Read the key again: the spark took every letter the same way.";
                wallTyped = "";
                sfx.Blocked();
            }
        }

        void WallPressInitials()
        {
            var s = Wall.Clean(wallInitials);
            if (s.Length == 0) return;
            Wall.SetMine(s);
            wallStage = 2;
            wallNote = $"{s} is pressed into the rock of the second chapter, in your copy. To put it on everyone's wall, type the GitHub account you will submit from, then share.";
            sfx.Place();
            sfx.Success();
        }

        void WallShare()
        {
            var s = Wall.Clean(wallInitials.Length > 0 ? wallInitials : Wall.Mine);
            var user = Wall.CleanUser(wallUser);
            if (s.Length == 0 || user.Length == 0 || Wall.Word.Length == 0) return;
            Wall.SetUser(user);
            Application.OpenURL(Wall.ShareUrl(s, user));
            wallNote = $"A GitHub issue opens in your browser. Sign in as {user} and submit it as it is: the wall checks the proof against that account, then answers and closes the issue.";
            sfx.Select();
        }

        /// <summary>Typing on the wall's page: letters and digits (and hyphens in an account name), Backspace, Enter to submit.</summary>
        void WallTyping()
        {
            if (!InputBridge.AnyKeyPressed(out var key)) return;
            string current = wallStage == 0 ? wallTyped : wallStage == 1 ? wallInitials : wallUser;
            int max = wallStage == 0 ? 24 : wallStage == 1 ? 3 : 39;
            if (key == Key.Backspace) { if (current.Length > 0) current = current.Substring(0, current.Length - 1); }
            else if (key == Key.Enter || key == Key.NumpadEnter)
            {
                if (wallStage == 0) WallSubmitAnswer(); else if (wallStage == 1) WallPressInitials(); else WallShare();
                return;
            }
            else
            {
                string name = key.ToString();
                char c = '\0';
                if (name.Length == 1 && char.IsLetter(name[0])) c = InputBridge.ShiftHeld ? name[0] : char.ToLowerInvariant(name[0]);
                else if (name.StartsWith("Digit") && name.Length == 6) c = name[5];
                else if (name.StartsWith("Numpad") && name.Length == 7 && char.IsDigit(name[6])) c = name[6];
                else if (wallStage == 2 && key == Key.Minus) c = '-';
                if (c != '\0' && current.Length < max) { current += c; sfx.Select(); }
            }
            if (wallStage == 0) wallTyped = current; else if (wallStage == 1) wallInitials = current; else wallUser = current;
        }

        static uint Mask(params int[] counts)
        {
            uint m = 0;
            foreach (var c in counts) m |= 1u << c;
            return m;
        }

        /// <summary>A few warm motes drifting up through the explored air, wrapping around the view.</summary>
        void UpdateSpores(float dt)
        {
            float halfH = cam.orthographicSize, halfW = halfH * Mathf.Max(0.1f, cam.aspect);
            if (spores.Count == 0)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply(false);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), CellPx);
                for (int i = 0; i < SporeCount; i++)
                {
                    var go = new GameObject("Spore");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = -6;
                    sr.sharedMaterial = worldSr.sharedMaterial;
                    sr.color = new Color(1f, 0.9f, 0.7f, 0.45f);
                    spores.Add(sr);
                    sporeState.Add(new Vector3(camBase.x + UnityEngine.Random.Range(-halfW, halfW), camBase.y + UnityEngine.Random.Range(-halfH, halfH), UnityEngine.Random.Range(0f, 6.28f)));
                }
            }
            float now = Time.time;
            for (int i = 0; i < spores.Count; i++)
            {
                var s = sporeState[i];
                s.y += 0.35f * dt;
                s.x += Mathf.Sin(now * 0.6f + s.z) * 0.4f * dt;
                if (s.y > camBase.y + halfH + 1f || Mathf.Abs(s.x - camBase.x) > halfW + 2f)
                {
                    s.x = camBase.x + UnityEngine.Random.Range(-halfW, halfW);
                    s.y = camBase.y - halfH - 0.5f;
                }
                sporeState[i] = s;
                spores[i].transform.position = new Vector3(s.x, s.y, 2f);
                float tw = 0.22f + 0.16f * Mathf.Sin(now * 2.1f + s.z * 3f);
                spores[i].color = new Color(1f, 0.9f, 0.7f, tw);
            }
        }

        /// <summary>Every ember back on its starting cell: on load, and on every rewind (growth may have quenched some).</summary>
        void SpawnEmbers()
        {
            foreach (var e in embers) if (e.sr != null) Destroy(e.sr.gameObject);
            embers.Clear();
            foreach (var spec in level.EmberSpecs())
            {
                var go = new GameObject("Ember");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = emberSprite;
                sr.sortingOrder = 8;
                sr.sharedMaterial = playerSr.sharedMaterial;
                if (spec.hunter) sr.color = new Color(0.85f, 0.95f, 1f, 1f);   // a hunter burns paler, almost white
                embers.Add(new Ember { cell = spec.cell, pos = spec.cell, dir = spec.vertical ? new Vector2Int(0, 1) : Vector2Int.right, sr = sr, hunter = spec.hunter });
            }
        }

        /// <summary>Growth puts embers out: any ember standing in a cell that just came alive is gone until the next rewind.</summary>
        void QuenchEmbers()
        {
            for (int k = embers.Count - 1; k >= 0; k--)
            {
                var e = embers[k];
                var c = Vector2Int.RoundToInt(e.pos);
                if (!InBounds(c) || sim.Cells[c.y * level.w + c.x] != Sim.Alive) continue;
                if (burstFrames.Count > 0) bursts.Add((c, Time.time));
                if (e.sr != null) Destroy(e.sr.gameObject);
                embers.RemoveAt(k);
                sfx.Remove();
                Achievements.Unlock("quench");
            }
        }

        /// <summary>World y of the bottom edge of a grid row: row 0 is the top of the map.</summary>
        float WorldY(int gy) => level.h - 1 - gy;
        int GridY(float wy) => level.h - 1 - Mathf.FloorToInt(wy);
        Vector2Int CellOf(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), GridY(world.y));
        Vector2 CellCentreWorld(Vector2Int c) => new Vector2(c.x + 0.5f, WorldY(c.y) + 0.5f);
        bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < level.w && c.y < level.h;
        bool Open(Vector2Int c) => InBounds(c) && rock[c.y * level.w + c.x] != Sim.Rock;

        /// <summary>Where an ember may go: not rock, and not living growth once it has grown.</summary>
        bool EmberOpen(Vector2Int c) => Open(c) && !(sim.Generation > 0 && sim.Cells[c.y * level.w + c.x] == Sim.Alive);

        int FirstUnsolved()
        {
            for (int i = 0; i < set.levels.Length; i++)
                if (i <= unlocked && !Solved(i)) return i;
            return Mathf.Min(unlocked, set.levels.Length - 1);
        }

        /// <summary>The chapter the title replays: the last solved one whose map is short enough to sit under the text.</summary>
        int DemoLevel()
        {
            int best = 0;
            for (int i = 0; i < set.levels.Length; i++) if (Solved(i) && set.levels[i].h <= 18) best = i;
            return best;
        }

        void SavePrefs()
        {
            PlayerPrefs.SetInt(KeyUnlocked, unlocked);
            PlayerPrefs.SetInt(KeySolved, solvedMask);
            PlayerPrefs.SetInt(KeyMuted, muted ? 1 : 0);
            PlayerPrefs.SetInt(KeyVolume, volume);
            PlayerPrefs.SetInt(KeyMusic, music);
            PlayerPrefs.SetInt(KeyShake, shakeOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (fatal != null || sim == null) return;
            float dt = Mathf.Min(Time.deltaTime, MaxDt);

            var mouse = InputBridge.MousePosition;
            var gui = new Vector2(mouse.x, Screen.height - mouse.y);
            Layout();
            if (Paused || phase == Phase.Title || phase == Phase.GameOver) UpdatePadMenu();
            else padFocus = -1;

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
                if (!hit && !Paused)
                {
                    switch (phase)
                    {
                        case Phase.Title: StartLevel(FirstUnsolved()); break;
                        case Phase.Story: phase = Phase.Play; Say("wake"); break;
                        case Phase.GameOver: Retry(); break;
                        case Phase.Finished: EnterTitle(); break;
                        case Phase.Result: if (!won) Rewind(); break;
                    }
                }
            }

            if (Paused)
            {
                if (overlay == Overlay.Wall)
                {
                    if (InputBridge.Pressed(GameAction.Menu)) Back();
                    else WallTyping();
                }
                else if (listening.HasValue)
                {
                    // the Controls page: the next key becomes the binding, Esc cancels
                    if (InputBridge.AnyKeyPressed(out var key))
                    {
                        if (key != Key.Escape) RebindTo(listening.Value, key);
                        listening = null;
                        sfx.Select();
                    }
                }
                else if (InputBridge.Pressed(GameAction.Menu) || (overlay == Overlay.Menu && InputBridge.Pressed(GameAction.Confirm)))
                {
                    Back();
                }
            }
            else
            {
                bool primary = InputBridge.Pressed(GameAction.Grow);
                if (InputBridge.Pressed(GameAction.Jump))
                {
                    if (InWorld) jumpBuffer = JumpBufferTime;
                    else primary = true;
                }
                if (!InWorld && InputBridge.Pressed(GameAction.Confirm)) primary = true;
                if (primary) Primary();
                if (InputBridge.Pressed(GameAction.Plant) && phase == Phase.Play) PlantHere();
                if (InputBridge.Pressed(GameAction.Rewind) && InWorld) Rewind();
                if (InputBridge.Pressed(GameAction.Skip) && (phase == Phase.Play || phase == Phase.Result)) Skip();
                if (InputBridge.Pressed(GameAction.Reveal) && CanReveal) Reveal();
                if (InputBridge.Pressed(GameAction.Mute)) ToggleMute();
                if (InputBridge.Pressed(GameAction.Menu))
                {
                    switch (phase)
                    {
                        case Phase.Title: if (CanQuit) QuitGame(); break;
                        case Phase.GameOver: case Phase.Finished: EnterTitle(); break;
                        case Phase.Dead: break;
                        default: OpenMenu(); break;
                    }
                }
            }

            if (InWorld && !Paused)
            {
                float move = 0f;
                if (InputBridge.Held(GameAction.Left)) move -= 1f;
                if (InputBridge.Held(GameAction.Right)) move += 1f;
                bool jumpHeld = InputBridge.Held(GameAction.Jump);
                StepPlayer(dt, move, jumpHeld);
                if (InWorld) CheckHazards();
                if (InWorld)
                {
                    var cell = CellOf(PlayerCentre);
                    if (cell != lastRevealCell) { lastRevealCell = cell; RevealAround(cell); }
                    if (pickupsLeft.Remove(cell)) { carried++; sfx.Success(); Say("seed"); }
                    if (cell == secretCell && !secretFound) FoundSecret();
                    if (won && cell == exitCell) { Next(); return; }
                    UpdateEmbers(dt);
                    if (tut != Tutorial.Off) UpdateTutorial(dt);
                }
            }

            if (phase == Phase.Dead && !Paused && Time.time - deathAt > DeathSeconds)
            {
                if (lives > 0) RestartLevel();
                else phase = Phase.GameOver;
            }

            if (phase == Phase.Growing && !Paused)
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

            UpdateVoice();
            if (toastTitle.Length == 0 && Achievements.TryDequeueToast(out var toast)) { toastTitle = toast.title; toastAt = Time.time; sfx.DoorChime(); }
            else if (toastTitle.Length > 0 && Time.time - toastAt > ToastSeconds) toastTitle = "";
            FollowCamera(dt);
            Paint();
            PlaceSprites();
        }

        // ------------------------------------------------------------------ achievements and the secret

        /// <summary>The cell the road does not lead to. Once.</summary>
        void FoundSecret()
        {
            secretFound = true;
            PlayerPrefs.SetInt(KeySecret, 1);
            PlayerPrefs.Save();
            Achievements.Unlock("secret");
            glowSr.color = new Color(0.78f, 1f, 0.72f, 0.3f);
            allSeen = true;
            shake = shakeOn ? 0.25f : 0f;
            sfx.Success();
            ClearVoice();
            Say("secret");
        }

        /// <summary>Everything a found origin can earn.</summary>
        void AwardForWin()
        {
            if (levelIndex == 0) Achievements.Unlock("first_light");
            if (attempts == 1) Achievements.Unlock("exact");
            if (lives == StartLives) Achievements.Unlock("untouched");
            if (levelIndex == 0 && Time.time - levelStartAt < 60f) Achievements.Unlock("quick");
            if (rewinds >= 10) Achievements.Unlock("patient");
            if (!revealed)
            {
                bool every = true;
                for (int i = 0; i < seen.Length && every; i++)
                    if (rock[i] != Sim.Rock && !seen[i]) every = false;
                if (every) Achievements.Unlock("corners");
            }
            int all = (1 << set.levels.Length) - 1;
            if ((solvedMask & all) == all)
            {
                Achievements.Unlock("every_origin");
                if (PlayerPrefs.GetInt(KeyReveals, 0) == 0) Achievements.Unlock("unaided");
            }
        }

        /// <summary>A pad moves a focus through the buttons on screen and activates it; the mouse takes it back.</summary>
        void UpdatePadMenu()
        {
            if (buttons.Count == 0) { padFocus = -1; return; }
            var mouse = InputBridge.MousePosition;
            if ((mouse - lastMouse).sqrMagnitude > 4f) { padFocus = -1; lastMouse = mouse; }
            int step = InputBridge.PadMenuStep(), side = InputBridge.PadMenuSide();
            bool activate = InputBridge.PadActivate();
            if (step == 0 && side == 0 && !activate) return;
            // order the buttons by row, then column
            var order = new List<int>();
            for (int i = 0; i < buttons.Count; i++) if (buttons[i].enabled) order.Add(i);
            order.Sort((a, b) =>
            {
                float dy = buttons[a].rect.y - buttons[b].rect.y;
                if (Mathf.Abs(dy) > 8f) return dy < 0 ? -1 : 1;
                return buttons[a].rect.x.CompareTo(buttons[b].rect.x);
            });
            if (order.Count == 0) return;
            int pos = order.IndexOf(padFocus);
            if (pos < 0) { padFocus = order[0]; pos = 0; if (!activate) return; }
            if (step != 0 || side != 0)
            {
                // up/down: the nearest button in that direction; left/right: the next in row order
                if (step != 0)
                {
                    var cur = buttons[padFocus].rect;
                    int best = -1; float bestD = float.MaxValue;
                    foreach (var i in order)
                    {
                        var r = buttons[i].rect;
                        float dy = (r.y - cur.y) * step;
                        if (dy <= 8f) continue;
                        float d = dy + Mathf.Abs(r.x - cur.x) * 0.3f;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    if (best >= 0) padFocus = best;
                }
                else padFocus = order[(pos + side + order.Count) % order.Count];
                sfx.Select();
                return;
            }
            if (activate && padFocus >= 0 && padFocus < buttons.Count) buttons[padFocus].act();
        }

        // ------------------------------------------------------------------ platforming

        bool SolidAtWorld(int gx, int wy)
        {
            if (gx < 0 || gx >= level.w) return true;     // the world has walls at its sides
            if (wy < 0 || wy >= level.h) return false;
            int gy = level.h - 1 - wy;
            int i = gy * level.w + gx;
            if (phys[i] == Sim.Rock) return !CrumbleFallen(i);
            return sim.Generation > 0 && sim.Cells[i] == Sim.Alive;
        }

        // ------------------------------------------------------------------ the lantern's voice

        /// <summary>Say the chapter's line for a trigger, once per chapter; deaths draw on the quips when a chapter has none.</summary>
        void Say(string trigger)
        {
            if (level == null || voiceSaid.Contains(trigger)) return;
            string line = null;
            foreach (var (t, l) in level.Lines()) if (t == trigger) { line = l; break; }
            if (line == null && trigger == "die") { line = DeathQuips[deathQuip % DeathQuips.Length]; deathQuip++; }
            if (line == null) return;
            if (trigger != "die") voiceSaid.Add(trigger);
            voiceQueue.Enqueue(line);
        }

        void UpdateVoice()
        {
            float now = Time.time;
            if (voiceLine.Length == 0 && voiceQueue.Count > 0)
            {
                voiceLine = voiceQueue.Dequeue();
                voiceAt = now;
                voiceShown = 0;
            }
            if (voiceLine.Length == 0) return;
            int shown = Mathf.Min(voiceLine.Length, (int)((now - voiceAt) * VoiceCharsPerSecond));
            if (shown > voiceShown)
            {
                if (shown / 3 != voiceShown / 3 && voiceLine[shown - 1] != ' ') sfx.VoiceBlip(shown);
                voiceShown = shown;
            }
            float fullAt = voiceAt + voiceLine.Length / VoiceCharsPerSecond;
            if (now > fullAt + VoiceHold) voiceLine = "";
        }

        void ClearVoice()
        {
            voiceQueue.Clear();
            voiceLine = "";
        }

        // ------------------------------------------------------------------ hazards

        bool CrumbleFallen(int i)
        {
            if (!crumbleAt.TryGetValue(i, out var t0)) return false;
            float age = Time.time - t0;
            return age > CrumbleDelay && age < CrumbleDelay + CrumbleGone;
        }

        /// <summary>0 = quiet, 1 = about to burst (sparks), 2 = burning.</summary>
        int SpoutState(int k, float now)
        {
            float t = (now + spoutPhase[k]) % SpoutPeriod;
            if (t < SpoutOn) return 2;
            if (t > SpoutPeriod - SpoutWarn) return 1;
            return 0;
        }

        bool FlameAt(int gx, int gy, float now)
        {
            for (int k = 0; k < spouts.Count; k++)
            {
                var s = spouts[k];
                if (s.x != gx || gy >= s.y || gy < s.y - SpoutHeight) continue;
                if (SpoutState(k, now) == 2) return true;
            }
            return false;
        }

        /// <summary>Acid, spikes and flame against the wanderer's body; crumble under the feet.</summary>
        void CheckHazards()
        {
            float x0 = pPos.x - PlayerW / 2f + 0.12f, x1 = pPos.x + PlayerW / 2f - 0.12f;
            float y0 = pPos.y + 0.1f, y1 = pPos.y + PlayerH - 0.1f;
            int gx0 = Mathf.FloorToInt(x0), gx1 = Mathf.FloorToInt(x1);
            int gy0 = GridY(y1), gy1 = GridY(y0);
            float now = Time.time;
            for (int gy = gy0; gy <= gy1; gy++)
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    var c = new Vector2Int(gx, gy);
                    if (!InBounds(c)) continue;
                    int i = gy * level.w + gx;
                    if (acid.Contains(i)) { Die("THE ACID TOOK YOU"); return; }
                    if (spikes.Contains(i)) { Die("THE SPIKES TOOK YOU"); return; }
                    if (spouts.Count > 0 && FlameAt(gx, gy, now)) { Die("THE FIRE TOOK YOU"); return; }
                }
            // crumble: the cells right under the feet start their timer
            if (grounded && crumble.Count > 0)
            {
                int under = GridY(pPos.y - 0.05f);
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    if (gx < 0 || gx >= level.w || under < 0 || under >= level.h) continue;
                    int i = under * level.w + gx;
                    if (crumble.Contains(i) && !crumbleAt.ContainsKey(i)) { crumbleAt[i] = now; sfx.Crack(); }
                }
            }
            // fallen cells come back; the moment one falls, a thud
            var restore = new List<int>();
            foreach (var kv in crumbleAt)
            {
                float age = now - kv.Value;
                if (age > CrumbleDelay + CrumbleGone) restore.Add(kv.Key);
                else if (age > CrumbleDelay && age - Time.deltaTime <= CrumbleDelay) sfx.Thud();
            }
            foreach (var i in restore) crumbleAt.Remove(i);
            // nearby spouts igniting, acid bubbling, embers hissing
            var centre = PlayerCentre;
            float emberNear = 0f, acidNear = 0f;
            foreach (var e in embers)
            {
                var world = new Vector2(e.pos.x + 0.5f, level.h - 1 - e.pos.y + 0.5f);
                emberNear = Mathf.Max(emberNear, 1f - Mathf.Clamp01((Vector2.Distance(world, centre) - 1f) / 5f));
            }
            var here = CellOf(centre);
            foreach (int i in acid)
            {
                var c = new Vector2Int(i % level.w, i / level.w);
                float d = Vector2.Distance(CellCentreWorld(c), centre);
                acidNear = Mathf.Max(acidNear, 1f - Mathf.Clamp01((d - 1.5f) / 5f));
            }
            sfx.SetNearby(emberNear, acidNear);
            for (int k = 0; k < spouts.Count; k++)
            {
                float t = (now + spoutPhase[k]) % SpoutPeriod;
                if (t < Time.deltaTime * 1.5f && Vector2.Distance(CellCentreWorld(spouts[k]), centre) < 9f && now - spoutHeard > 0.2f)
                {
                    sfx.Whoosh();
                    spoutHeard = now;
                }
            }
        }

        bool Overlaps(float x, float y, float w, float h)
        {
            int gx0 = Mathf.FloorToInt(x), gx1 = Mathf.FloorToInt(x + w - 1e-4f);
            int wy0 = Mathf.FloorToInt(y), wy1 = Mathf.FloorToInt(y + h - 1e-4f);
            for (int gx = gx0; gx <= gx1; gx++)
                for (int wy = wy0; wy <= wy1; wy++)
                    if (SolidAtWorld(gx, wy)) return true;
            return false;
        }

        void MoveAxis(float dx, float dy)
        {
            pPos.x += dx;
            pPos.y += dy;
            float x = pPos.x - PlayerW / 2f, y = pPos.y;
            if (!Overlaps(x, y, PlayerW, PlayerH)) return;
            if (dx != 0f)
            {
                if (dx > 0f) { int gx = Mathf.FloorToInt(x + PlayerW - 1e-4f); pPos.x = gx - PlayerW / 2f - 1e-3f; }
                else { int gx = Mathf.FloorToInt(x); pPos.x = gx + 1 + PlayerW / 2f + 1e-3f; }
                pVel.x = 0f;
            }
            else if (dy != 0f)
            {
                if (dy < 0f) { int wy = Mathf.FloorToInt(y); pPos.y = wy + 1 + 1e-3f; grounded = true; }
                else { int wy = Mathf.FloorToInt(y + PlayerH - 1e-4f); pPos.y = wy - PlayerH - 1e-3f; }
                pVel.y = 0f;
            }
        }

        void StepPlayer(float dt, float move, bool jumpHeld)
        {
            if (move > 0f) facingRight = true;
            else if (move < 0f) facingRight = false;
            pVel.x = Mathf.MoveTowards(pVel.x, move * RunSpeed, 70f * dt);
            if (grounded) coyote = CoyoteTime; else coyote -= dt;
            jumpBuffer -= dt;
            if (jumpBuffer > 0f && coyote > 0f)
            {
                pVel.y = JumpVelocity;
                jumpBuffer = 0f;
                coyote = 0f;
                grounded = false;
                jumps++;
                sfx.Jump();
            }
            if (!jumpHeld && pVel.y > 4f) pVel.y = 4f;   // let go early for a shorter hop
            pVel.y = Mathf.Max(pVel.y - Gravity * dt, -26f);

            MoveAxis(pVel.x * dt, 0f);
            bool wasGrounded = grounded;
            grounded = false;
            MoveAxis(0f, pVel.y * dt);
            if (!grounded) grounded = pVel.y <= 0f && Overlaps(pPos.x - PlayerW / 2f + 0.03f, pPos.y - 0.05f, PlayerW - 0.06f, 0.05f);
            if (grounded && !wasGrounded && pVel.y <= 0f) sfx.Step(1);

            if (pPos.y < -2f) Die("THE VOID TOOK YOU");
        }

        bool PlayerInsideGrowth()
        {
            return Overlaps(pPos.x - PlayerW / 2f + 0.1f, pPos.y + 0.08f, PlayerW - 0.2f, PlayerH - 0.16f);
        }

        void UpdateEmbers(float dt)
        {
            var centre = PlayerCentre;
            var playerCell = CellOf(centre);
            foreach (var e in embers)
            {
                // a hunter turns toward the wanderer whenever they share its row and the way is open; it still keeps to its run
                if (e.hunter && playerCell.y == e.cell.y && playerCell.x != e.cell.x && (e.pos - (Vector2)e.cell).sqrMagnitude < 1e-5f)
                {
                    var want = playerCell.x > e.cell.x ? Vector2Int.right : Vector2Int.left;
                    if (want != e.dir && EmberOpen(e.cell + want)) e.dir = want;
                }
                var next = e.cell + e.dir;
                if (!EmberOpen(next))
                {
                    e.dir = -e.dir;
                    next = e.cell + e.dir;
                    if (!EmberOpen(next)) continue;
                }
                e.pos = Vector2.MoveTowards(e.pos, next, EmberSpeed * dt);
                if ((e.pos - (Vector2)next).sqrMagnitude < 1e-5f) { e.pos = next; e.cell = next; }
                var world = new Vector2(e.pos.x + 0.5f, level.h - 1 - e.pos.y + 0.5f);
                if (Vector2.Distance(world, centre) < 0.72f)
                {
                    Die("THE EMBER TOOK YOU");
                    return;
                }
            }
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

        // ------------------------------------------------------------------ camera

        float ViewHalfH => Mathf.Min(level.h / 2f, 8.5f);

        Vector3 CameraGoal()
        {
            float halfH = ViewHalfH;
            float halfW = halfH * Mathf.Max(0.1f, cam.aspect);
            Vector2 focus = phase == Phase.Title ? DemoFocus() : new Vector2(pPos.x, pPos.y + 1.5f);
            float x = level.w <= halfW * 2f ? level.w / 2f : Mathf.Clamp(focus.x, halfW, level.w - halfW);
            float y = level.h <= halfH * 2f ? level.h / 2f : Mathf.Clamp(focus.y, halfH, level.h - halfH);
            return new Vector3(x, y, -10f);
        }

        /// <summary>The title looks at the demo growth but keeps it left of centre, clear of the menu rows.</summary>
        Vector2 DemoFocus()
        {
            if (answer.Count == 0) return new Vector2(level.w / 2f, level.h / 2f);
            var sum = Vector2.zero;
            foreach (var a in answer) sum += CellCentreWorld(a);
            var focus = sum / answer.Count;
            focus.x += ViewHalfH * Mathf.Max(0.1f, cam.aspect) * 0.75f;
            return focus;
        }

        void SnapCamera()
        {
            cam.orthographicSize = ViewHalfH;
            camBase = CameraGoal();
            cam.transform.position = camBase;
        }

        void FollowCamera(float dt)
        {
            cam.orthographicSize = ViewHalfH;
            camBase = Vector3.Lerp(camBase, CameraGoal(), 1f - Mathf.Exp(-7f * dt));
            var pos = camBase;
            if (shake > 0f)
            {
                shake -= dt;
                var o = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, shake) * 0.5f;
                pos += new Vector3(o.x, o.y, 0f);
            }
            cam.transform.position = pos;
            bgSr.transform.position = new Vector3(camBase.x, camBase.y, 30f);   // the vignette stays behind the slab's whole depth
            float aspect = Mathf.Max(0.1f, cam.aspect);
            bgSr.transform.localScale = new Vector3(2f * cam.orthographicSize * aspect * 1.04f, 2f * cam.orthographicSize * 1.04f, 1f);
            // parallax: the skylines slide slower than the world and sit just below the ground line
            if (farSr != null) farSr.transform.position = new Vector3(camBase.x * 0.75f - 24f, -1.5f, 4f);
            if (nearSr != null) nearSr.transform.position = new Vector3(camBase.x * 0.5f - 24f, -1.0f, 3f);
            if (slabMr != null)
            {
                slabMr.transform.position = new Vector3(camBase.x * slabParallax + slabOffset.x, slabOffset.y, slabOffset.z);
                slabMr.transform.rotation = Quaternion.Euler(slabEuler);
                slabMr.transform.localScale = slabScale;
            }
            UpdateSpores(dt);
        }

        // ------------------------------------------------------------------ title demo

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
                    if (demoTimer > 2.2f) { demoTimer = 0f; DemoReset(); demoStage = 0; }
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
                case Phase.Story: phase = Phase.Play; Say("wake"); break;
                case Phase.GameOver: Retry(); break;
                case Phase.Play: Grow(); break;
                case Phase.Growing: while (phase == Phase.Growing) Advance(); break;
                case Phase.Result: if (!won) Rewind(); break;
                case Phase.Finished: EnterTitle(); break;
            }
        }

        void EnterTitle()
        {
            overlay = Overlay.None;
            listening = null;
            tut = Tutorial.Off;
            ClearVoice();
            sfx.SetTitleMusic();
            sfx.SetMotifPlaying(true, 0.8f);
            sfx.SetNearby(0f, 0f);
            LoadLevel(DemoLevel());
            phase = Phase.Title;
            allSeen = true;
            demoStage = 0;
            demoTimer = 0f;
            DemoReset();
            SnapCamera();
        }

        void StartLevel(int index)
        {
            LoadLevel(index);
            lives = StartLives;
            BeginLevel();
            sfx.Select();
        }

        void BeginLevel()
        {
            phase = string.IsNullOrEmpty(level.intro) ? Phase.Play : Phase.Story;
            SnapCamera();   // the load happened under the old phase; look at the wanderer, not the title demo
            tut = levelIndex == 0 && !Solved(0) ? Tutorial.Move : Tutorial.Off;
            tutTimer = 0f;
            jumps = 0;
            levelStartAt = Time.time;
            voiceSaid.Clear();
            ClearVoice();
            sfx.SetMotif(levelIndex, brighter: levelIndex % 3 == 1, seed: levelIndex * 17 + 3);
            sfx.SetMotifPlaying(true);
            sfx.StingerStart();
            if (phase == Phase.Play) Say("wake");
        }

        // ------------------------------------------------------------------ tutorial

        bool NearAnswer(float radius)
        {
            var c = PlayerCentre;
            foreach (var a in answer) if (Vector2.Distance(CellCentreWorld(a), c) <= radius) return true;
            return false;
        }

        /// <summary>Standing in or right beside the outline: where the growth will reach.</summary>
        bool InsideTargetArea()
        {
            var c = CellOf(PlayerCentre);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = c + new Vector2Int(dx, dy);
                    if (InBounds(n) && target[n.y * level.w + n.x] == Sim.Alive) return true;
                }
            return false;
        }

        bool SeedIsAnswer()
        {
            if (seeds.Count == 0) return false;
            foreach (var s in seeds) if (!answer.Contains(s)) return false;
            return true;
        }

        /// <summary>Within two cells of any outline cell: down in the chasm with it.</summary>
        bool NearTarget()
        {
            var c = CellOf(PlayerCentre);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var n = c + new Vector2Int(dx, dy);
                    if (InBounds(n) && target[n.y * level.w + n.x] == Sim.Alive) return true;
                }
            return false;
        }

        void UpdateTutorial(float dt)
        {
            bool clear = grounded && !InsideTargetArea();
            switch (tut)
            {
                case Tutorial.Move:
                    if (Mathf.Abs(pVel.x) > 1f) tutTimer += dt;
                    if (tutTimer > 0.5f) { tut = Tutorial.Jump; tutTimer = 0f; }
                    break;
                case Tutorial.Jump:
                    if (jumps > 0) tut = Tutorial.FindOutline;
                    break;
                case Tutorial.FindOutline:
                    if (seeds.Count > 0) tut = SeedIsAnswer() ? Tutorial.GetClear2 : Tutorial.GetClear;
                    else if (NearTarget()) tut = Tutorial.PlantAnywhere;
                    break;
                case Tutorial.PlantAnywhere:
                    if (seeds.Count > 0) tut = SeedIsAnswer() ? Tutorial.GetClear2 : Tutorial.GetClear;
                    break;
                case Tutorial.GetClear:
                    if (phase != Phase.Play) tut = Tutorial.SeeResult;
                    else if (seeds.Count == 0) tut = Tutorial.PlantAnywhere;
                    else if (clear) tut = Tutorial.GrowFirst;
                    break;
                case Tutorial.GrowFirst:
                    if (phase != Phase.Play) tut = Tutorial.SeeResult;
                    else if (seeds.Count == 0) tut = Tutorial.PlantAnywhere;
                    else if (!clear && grounded) tut = Tutorial.GetClear;
                    break;
                case Tutorial.SeeResult:
                    if (phase == Phase.Result && resultTime > 1.2f) tut = won ? Tutorial.Cross : Tutorial.RewindIt;
                    else if (phase == Phase.Play) tut = Tutorial.RewindIt;
                    break;
                case Tutorial.RewindIt:
                    if (phase == Phase.Play) tut = seeds.Count == 0 ? Tutorial.Reason : Tutorial.TakeBack;
                    break;
                case Tutorial.TakeBack:
                    if (seeds.Count == 0) tut = Tutorial.Reason;
                    else if (SeedIsAnswer()) tut = Tutorial.GetClear2;
                    break;
                case Tutorial.Reason:
                    if (seeds.Count > 0) tut = SeedIsAnswer() ? Tutorial.GetClear2 : Tutorial.TakeBack;
                    else if (NearAnswer(1.4f)) tut = Tutorial.PlantRight;
                    break;
                case Tutorial.PlantRight:
                    if (seeds.Count > 0) tut = SeedIsAnswer() ? Tutorial.GetClear2 : Tutorial.TakeBack;
                    else if (!NearAnswer(2.5f)) tut = Tutorial.Reason;
                    break;
                case Tutorial.GetClear2:
                    if (phase != Phase.Play) tut = Tutorial.Cross;
                    else if (seeds.Count == 0) tut = Tutorial.Reason;
                    else if (!SeedIsAnswer()) tut = Tutorial.TakeBack;
                    else if (clear) tut = Tutorial.GrowRight;
                    break;
                case Tutorial.GrowRight:
                    if (phase != Phase.Play) tut = Tutorial.Cross;
                    else if (seeds.Count == 0) tut = Tutorial.Reason;
                    else if (!clear && grounded) tut = Tutorial.GetClear2;
                    break;
                case Tutorial.Cross:
                    if (phase == Phase.Play) tut = seeds.Count == 0 ? Tutorial.Reason : (SeedIsAnswer() ? Tutorial.GetClear2 : Tutorial.TakeBack);
                    break;
            }
        }

        string TutorialText()
        {
            string plant = L(GameAction.Plant), grow = L(GameAction.Grow), rewind = L(GameAction.Rewind);
            switch (tut)
            {
                case Tutorial.Move: return $"Run with {L(GameAction.Left)} and {L(GameAction.Right)}.";
                case Tutorial.Jump: return $"Jump with {L(GameAction.Jump)}. Hold it to jump higher, let go early for a hop.";
                case Tutorial.FindOutline: return "Your lantern shows the outline of something that grew here: the framed cells down in the chasm. That is the ruin you will regrow. Go down to it.";
                case Tutorial.PlantAnywhere: return $"A seed is a beginning: everything that grew here grew from one. Stand anywhere on the chasm floor and press {plant} to plant yours. Anywhere. We will see what grows.";
                case Tutorial.GetClear: return "Growth is solid rock, and it takes whoever stands inside it. Climb back up onto the rock, clear of the outline.";
                case Tutorial.GrowFirst: return $"Press {grow}. The seed grows under this chapter's law for two generations. Match the outline exactly and the door opens.";
                case Tutorial.SeeResult:
                    if (phase == Phase.Growing) return "Watch: gold where the growth matches the outline, red where it grew outside it.";
                    return won ? "Exact, first time. The door is open." : "Red cells grew where the outline is not, and the door stays shut. It has to be exact, and only a seed on the true origin grows the true shape.";
                case Tutorial.RewindIt: return $"Press {rewind} to rewind. The growth clears; your seed stays where you planted it. Rewind costs nothing: use it whenever a growth comes out wrong.";
                case Tutorial.TakeBack: return $"Wrong place, so the wrong shape. Stand on your seed and press {plant} to take it back.";
                case Tutorial.Reason: return "Two generations reach two cells in every direction, so a bloom is five wide, and it began in the middle: the cell on the stone, marked. The origin is always where the shape began. Go there.";
                case Tutorial.PlantRight: return $"Press {plant} here. This is the origin: a seed planted where the ruin began regrows it exactly.";
                case Tutorial.GetClear2: return "Now get clear again: up onto the rock, away from the outline. Never grow while standing in it.";
                case Tutorial.GrowRight: return $"Press {grow}. When the growth is exact, it becomes the ground to the door.";
                case Tutorial.Cross:
                    if (phase == Phase.Growing) return "Gold all the way.";
                    return won ? "Exact. The door is open: cross the bloom and walk to it."
                               : $"Not exact. {rewind} rewinds, {plant} takes the seed back; the origin is the middle of the outline.";
                default: return "";
            }
        }

        void Die(string why)
        {
            if (phase == Phase.Dead || phase == Phase.GameOver) return;
            phase = Phase.Dead;
            deathAt = Time.time;
            deathText = why;
            lives = Mathf.Max(0, lives - 1);
            pVel = Vector2.zero;
            shake = shakeOn ? 0.45f : 0f;
            sfx.StingerDeath();
            ClearVoice();
            if (lives > 0) Say("die");
            Achievements.Died(why);
            PlayerPrefs.SetInt(DeathsKey(levelIndex), PlayerPrefs.GetInt(DeathsKey(levelIndex), 0) + 1);
            PlayerPrefs.Save();
        }

        void RestartLevel()
        {
            var keepSeen = seen;
            var keepSeenAt = seenAt;
            LoadLevel(levelIndex);
            for (int i = 0; i < seen.Length && i < keepSeen.Length; i++)
                if (keepSeen[i]) { seen[i] = true; seenAt[i] = keepSeenAt[i]; }
            phase = Phase.Play;
            if (tut != Tutorial.Off) tut = Tutorial.FindOutline;
        }

        void Retry()
        {
            lives = StartLives;
            RestartLevel();
            sfx.Select();
        }

        /// <summary>Plant or take back a seed in the cell the wanderer stands in; on the found secret, read the wall instead.</summary>
        void PlantHere()
        {
            var c = CellOf(PlayerCentre);
            if (!InBounds(c)) return;
            if (c == secretCell && secretFound) { OpenWall(); return; }
            ToggleSeed(c);
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
            Say("plant");
        }

        void Grow()
        {
            if (seeds.Count == 0) { sfx.Blocked(); return; }
            attempts++;
            stepTimer = 0f;
            phase = Phase.Growing;
            Say("grow");
        }

        void Advance()
        {
            int gen = sim.Step(1);
            sfx.Tick(gen);
            TrackChanges();
            match = sim.Compare(target);
            QuenchEmbers();
            if (PlayerInsideGrowth())
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
                sfx.DoorChime();
                Say("found");
                AwardForWin();
                if (exitCell.x < 0) Next();
            }
            else
            {
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
            won = false;
            phase = Phase.Play;
            if (hadGrown)
            {
                rewinds++;
                SpawnEmbers();
                sfx.Rewind();
            }
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
            PlayerPrefs.SetInt(KeyReveals, PlayerPrefs.GetInt(KeyReveals, 0) + 1);
            PlayerPrefs.Save();
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

        // ------------------------------------------------------------------ menu, settings, controls

        void OpenMenu()
        {
            overlay = Overlay.Menu;
            overlayFromMenu = true;
            listening = null;
            confirmReset = false;
            sfx.Select();
        }

        void OpenPage(Overlay page)
        {
            overlayFromMenu = overlay == Overlay.Menu;
            overlay = page;
            listening = null;
            confirmReset = false;
            sfx.Select();
        }

        /// <summary>One step out: a page returns to the pause menu or the title, the pause menu resumes.</summary>
        void Back()
        {
            listening = null;
            confirmReset = false;
            overlay = overlay != Overlay.Menu && overlayFromMenu ? Overlay.Menu : Overlay.None;
            SavePrefs();
            sfx.Select();
        }

        void CloseOverlay()
        {
            overlay = Overlay.None;
            listening = null;
            confirmReset = false;
            SavePrefs();
        }

        void SetVolume(int v)
        {
            volume = Mathf.Clamp(v, 0, VolumeSteps);
            sfx.SetMasterVolume(volume / (float)VolumeSteps);
            SavePrefs();
            sfx.Place();
        }

        void SetMusic(int v)
        {
            music = Mathf.Clamp(v, 0, VolumeSteps);
            sfx.SetMusicVolume(music / (float)VolumeSteps);
            SavePrefs();
            sfx.Select();
        }

        void ToggleShake()
        {
            shakeOn = !shakeOn;
            SavePrefs();
            if (shakeOn) shake = 0.3f;
            sfx.Select();
        }

        void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            sfx.Select();
        }

        /// <summary>Two clicks: the first arms it, the second wipes the chapter progress.</summary>
        void ResetProgressClicked()
        {
            if (!confirmReset)
            {
                confirmReset = true;
                sfx.Blocked();
                return;
            }
            confirmReset = false;
            unlocked = 0;
            solvedMask = 0;
            solved = 0;
            Achievements.ResetAll();
            Wall.Reset();
            PlayerPrefs.DeleteKey(KeySecret);
            for (int i = 0; i < set.levels.Length; i++) PlayerPrefs.DeleteKey(DeathsKey(i));
            secretFound = false;
            ApplyLook();
            SavePrefs();
            sfx.Rewind();
        }

        /// <summary>Bind a key; if another action already used it, that action takes the old key so nothing is left unbound.</summary>
        void RebindTo(GameAction action, Key key)
        {
            var old = InputBridge.Primary(action);
            foreach (var other in InputBridge.Rebindable)
                if (other != action && InputBridge.Primary(other) == key) InputBridge.Bind(other, old);
            InputBridge.Bind(action, key);
        }

        /// <summary>A browser tab has nothing to quit to.</summary>
        static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;

        // ------------------------------------------------------------------ the wanderer's look

        Texture2D heroBase0, heroBase1;
        int lookRobe, lookHat, lookLantern;      // what the Wanderer page is showing, open or not

        int OriginsFound
        {
            get
            {
                var levels = set != null ? set.levels : null;
                if (levels == null) return 0;
                int n = 0;
                for (int i = 0; i < levels.Length; i++) if (Solved(i)) n++;
                return n;
            }
        }
        bool SecretKnown => PlayerPrefs.GetInt(KeySecret, 0) != 0;

        /// <summary>Rebuild the wanderer's frames from the base renders and the saved look.</summary>
        void ApplyLook()
        {
            if (heroBase0 == null) return;
            try
            {
                if (set != null && set.levels != null) Look.Settle(OriginsFound, SecretKnown);   // before the save is read, wear what was saved
                var t0 = Look.Build(heroBase0, Look.Robe, Look.Hat, Look.Lantern);
                var t1 = Look.Build(heroBase1 != null ? heroBase1 : heroBase0, Look.Robe, Look.Hat, Look.Lantern);
                heroFrames = new[]
                {
                    Sprite.Create(t0, new Rect(0, 0, t0.width, t0.height), new Vector2(0.5f, 0f), t0.height),
                    Sprite.Create(t1, new Rect(0, 0, t1.width, t1.height), new Vector2(0.5f, 0f), t1.height),
                };
                if (playerSr != null) playerSr.sprite = heroFrames[0];
            }
            catch (Exception e)
            {
                // the plain render is always there to fall back on; say what went wrong and carry on
                Debug.LogWarning($"Point of Origin: the wanderer's look could not be applied: {e}");
                if (heroFrames == null || heroFrames.Length == 0)
                {
                    heroFrames = new[]
                    {
                        Sprite.Create(heroBase0, new Rect(0, 0, heroBase0.width, heroBase0.height), new Vector2(0.5f, 0f), heroBase0.height),
                        Sprite.Create(heroBase1 != null ? heroBase1 : heroBase0, new Rect(0, 0, heroBase0.width, heroBase0.height), new Vector2(0.5f, 0f), heroBase0.height),
                    };
                    if (playerSr != null) playerSr.sprite = heroFrames[0];
                }
            }
        }

        /// <summary>Browse one of the three choices; an open choice is worn at once, a locked one only shown.</summary>
        void StepLook(int kind, int d)
        {
            int found = OriginsFound; bool secret = SecretKnown;
            if (kind == 0)
            {
                lookRobe = (lookRobe + d + Look.Robes.Length) % Look.Robes.Length;
                if (Look.Open(Look.Robes[lookRobe], found, secret)) { Look.Robe = lookRobe; ApplyLook(); sfx.Select(); } else sfx.Blocked();
            }
            else if (kind == 1)
            {
                lookHat = (lookHat + d + Look.Hats.Length) % Look.Hats.Length;
                if (Look.Open(Look.Hats[lookHat], found, secret)) { Look.Hat = lookHat; ApplyLook(); sfx.Select(); } else sfx.Blocked();
            }
            else
            {
                lookLantern = (lookLantern + d + Look.Lanterns.Length) % Look.Lanterns.Length;
                if (Look.Open(Look.Lanterns[lookLantern], found, secret)) { Look.Lantern = lookLantern; ApplyLook(); sfx.Select(); } else sfx.Blocked();
            }
        }

        // ------------------------------------------------------------------ deaths per chapter

        const string KeyDeaths = "po.deaths.";
        static string DeathsKey(int level) => KeyDeaths + level;
        GUIStyle stDeaths;

        void QuitGame()
        {
            SavePrefs();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ painting

        void TrackChanges()
        {
            sim.Refresh();
            float now = Time.time;
            for (int i = 0; i < prevCells.Length; i++)
            {
                byte c = sim.Cells[i], p = prevCells[i];
                if (c == Sim.Alive && p != Sim.Alive)
                {
                    bornAt[i] = now;
                    if (phase == Phase.Growing && burstFrames.Count > 0 && bursts.Count < 400)
                        bursts.Add((new Vector2Int(i % level.w, i / level.w), now));
                }
                else if (c != Sim.Alive && p == Sim.Alive) diedAt[i] = now;
                prevCells[i] = c;
            }
        }

        /// <summary>Replay the baked burst around every cell born recently: warm sparks that fade.</summary>
        void PaintBursts(float now)
        {
            if (burstFrames.Count == 0 || bursts.Count == 0) return;
            const float frameSeconds = 1f / 30f;
            int tw = tex.width, th = tex.height;
            for (int b = bursts.Count - 1; b >= 0; b--)
            {
                var (cell, t0) = bursts[b];
                int f = (int)((now - t0) / frameSeconds);
                if (f >= burstFrames.Count) { bursts.RemoveAt(b); continue; }
                float fade = 1f - (float)f / burstFrames.Count;
                var col = Color32.Lerp(ColLamp, ColEmberCore, 0.4f);
                col = Color32.Lerp(new Color32(col.r, col.g, col.b, 0), col, Mathf.Clamp01(fade * 1.6f));
                float cx = cell.x * CellPx + CellPx / 2f;
                float cy = (level.h - 1 - cell.y) * CellPx + CellPx / 2f;
                foreach (var p in burstFrames[f])
                {
                    int x = Mathf.RoundToInt(cx + p.x * CellPx * 0.7f), y = Mathf.RoundToInt(cy + p.y * CellPx * 0.7f);
                    if (x < 0 || y < 0 || x >= tw - 1 || y >= th - 1) continue;
                    px[y * tw + x] = col;
                    px[y * tw + x + 1] = col;
                    px[(y + 1) * tw + x] = col;
                }
            }
        }

        static Color32 AgeColor(int age) => AgeRamp[Mathf.Clamp(age - 1, 0, AgeRamp.Length - 1)];
        static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, 255), t);

        void Paint()
        {
            if (sim == null || tex == null) return;
            sim.Refresh();
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            float now = Time.time;
            bool placing = phase == Phase.Play || (phase == Phase.Title && sim.Generation == 0);
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
            Color32 ghost = Color32.Lerp(ColGhost, ColGhostBright, pulse * 0.6f);
            var ghostFill = new Color32(18, 34, 40, 110);
            float flash = 1f - Mathf.Clamp01((now - winAt) / 0.5f);

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
                    // air is empty sky; the unexplored dark is solid black; rock and the ghost frames are drawn
                    if (!visible)
                    {
                        PaintFull(x, y, isRock ? ColRockDark : ColUnknown);
                        continue;
                    }
                    if (isRock)
                    {
                        PaintRock(x, y);
                        continue;
                    }
                    if (inTarget && !alive) PaintCell(x, y, ghostFill, ghost, 0);

                    if (alive)
                    {
                        Color32 fill, edge;
                        if (placing) { fill = ColSeed; edge = ColSeedEdge; }
                        else if (inTarget) { fill = AgeColor(sim.Ages[i]); edge = Lighten(fill, 0.35f); }
                        else { fill = ColWrong; edge = ColWrongEdge; }
                        if (flash > 0f && inTarget && !placing) fill = Lighten(fill, flash * 0.85f);
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
                    if (exitCell.x == x && exitCell.y == y)
                    {
                        var door = won ? Lighten(ColDoorOpen, pulse * 0.3f) : ColDoorClosed;
                        PaintCell(x, y, won ? ColDoorOpen : ColOpen, door, 0);
                        PaintDot(x, y, door);
                    }
                    if (revealed && answer.Contains(new Vector2Int(x, y))) PaintDot(x, y, ColReveal);
                }
            }
            // fossils pressed into the stone, wherever the stone has been seen
            for (int i = 0; i < fossilPx.Length; i++)
                if (allSeen || seen[fossilCell[i]]) px[fossilPx[i]] = fossilColor[i];
            PaintHazards(now);
            // the secret: a faint pulse in its cell once the lantern has reached it; a steady mark once found
            if (secretCell.x >= 0 && (allSeen || seen[secretCell.y * level.w + secretCell.x]))
                PaintDot(secretCell.x, secretCell.y, secretFound ? Hex("b8ffb0") : Color32.Lerp(ColUnknown, ColSeedEdge, 0.25f + 0.5f * pulse));
            // the tutorial's beacon on the stone, once the player has reasoned it out, drawn even into the unexplored dark
            if (tut == Tutorial.Reason || tut == Tutorial.PlantRight)
                foreach (var a in answer) PaintDot(a.x, a.y, pulse > 0.5f ? ColLamp : ColSeedEdge);
            PaintBursts(now);
            tex.SetPixels32(px);
            tex.Apply(false);
        }

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

        /// <summary>
        /// Rock is one continuous mass: the Houdini grain tiles across cells with no
        /// seams, and a lighter line marks the surface where air sits above it.
        /// </summary>
        void PaintRock(int x, int y)
        {
            int tw = tex.width;
            int x0 = x * CellPx;
            int y0 = (level.h - 1 - y) * CellPx;
            bool surface = y == 0 || rock[(y - 1) * level.w + x] != Sim.Rock;
            for (int dy = 0; dy < CellPx; dy++)
            {
                int row = (y0 + dy) * tw + x0;
                bool top = surface && dy == CellPx - 1;
                for (int dx = 0; dx < CellPx; dx++)
                {
                    Color32 c;
                    if (grain != null)
                    {
                        float g = grain[((y0 + dy) % grainN) * grainN + ((x0 + dx) % grainN)];
                        c = Color32.Lerp(ColRockEdge, ColRockLight, g);
                    }
                    else c = ColRock;
                    if (top) c = Lighten(c, 0.28f);
                    px[row + dx] = c;
                }
            }
        }

        /// <summary>Acid, spikes, spouts and crumble drawn over the cells the main pass painted.</summary>
        void PaintHazards(float now)
        {
            int tw = tex.width;
            var clear = new Color32(0, 0, 0, 0);
            foreach (int i in acid)
            {
                int gx = i % level.w, gy = i / level.w;
                if (!(allSeen || seen[i])) continue;
                int x0 = gx * CellPx, y0 = (level.h - 1 - gy) * CellPx;
                bool surface = gy == 0 || !acid.Contains(i - level.w);
                for (int dy = 0; dy < CellPx; dy++)
                {
                    int row = (y0 + dy) * tw + x0;
                    for (int dx = 0; dx < CellPx; dx++)
                    {
                        // a slow wave along the surface, a few rising bubbles below it
                        float wave = Mathf.Sin(now * 2.2f + (x0 + dx) * 0.55f) * 0.5f + 0.5f;
                        int top = CellPx - 1 - (surface ? (int)(wave * 1.5f) : 0);
                        Color32 c = ColAcid;
                        if (surface && dy >= top - 1) c = ColAcidBright;
                        else if (((x0 + dx) * 7 + (int)(now * 6f) + dy * 3) % 23 == 0) c = ColAcidBright;
                        if (dy > top) c = clear;
                        px[row + dx] = c;
                    }
                }
            }
            foreach (int i in spikes)
            {
                int gx = i % level.w, gy = i / level.w;
                if (!(allSeen || seen[i])) continue;
                int x0 = gx * CellPx, y0 = (level.h - 1 - gy) * CellPx;
                // two spikes per cell: a three-wide base, then a one-pixel shaft up to the point
                for (int s = 0; s < 2; s++)
                {
                    int bx = x0 + s * 4;
                    for (int dx = 0; dx < 3; dx++) { px[y0 * tw + bx + dx] = ColRockEdge; px[(y0 + 1) * tw + bx + dx] = ColSpike; }
                    for (int dy = 2; dy < 7; dy++) px[(y0 + dy) * tw + bx + 1] = dy == 6 ? new Color32(235, 240, 250, 255) : ColSpike;
                }
            }
            for (int k = 0; k < spouts.Count; k++)
            {
                var s = spouts[k];
                int i = s.y * level.w + s.x;
                if (!(allSeen || seen[i])) continue;
                int x0 = s.x * CellPx, y0 = (level.h - 1 - s.y) * CellPx;
                int state = SpoutState(k, now);
                // the vent: a dark slit in the rock's top with a glow that grows as it comes
                for (int dx = 2; dx < CellPx - 2; dx++)
                {
                    px[(y0 + CellPx - 1) * tw + x0 + dx] = state == 0 ? ColCrack : ColFlame;
                    px[(y0 + CellPx - 2) * tw + x0 + dx] = state == 2 ? ColFlameCore : ColCrack;
                }
                if (state == 0) continue;
                int flameH = state == 2 ? SpoutHeight * CellPx : 3;
                for (int h = 0; h < flameH; h++)
                {
                    int y = y0 + CellPx + h;
                    if (y >= tex.height) break;
                    float t = (float)h / (SpoutHeight * CellPx);
                    int flick = (int)(Mathf.Sin(now * 31f + h * 1.7f + s.x) * 1.5f);
                    int half = Mathf.Max(1, (int)((1f - t) * 3.5f) + (state == 1 ? -1 : 0));
                    for (int dx = 4 - half + flick / 2; dx < 4 + half + flick / 2; dx++)
                    {
                        if (dx < 0 || dx >= CellPx) continue;
                        px[y * tw + x0 + dx] = t < 0.35f ? ColFlameCore : (state == 1 ? ColFlameCore : ColFlame);
                    }
                }
            }
            foreach (int i in crumble)
            {
                int gx = i % level.w, gy = i / level.w;
                if (!(allSeen || seen[i])) continue;
                int x0 = gx * CellPx, y0 = (level.h - 1 - gy) * CellPx;
                bool fallen = CrumbleFallen(i);
                bool shaking = crumbleAt.ContainsKey(i) && !fallen && now - crumbleAt[i] <= CrumbleDelay;
                if (fallen)
                {
                    // gone for now: a faint outline where it will come back
                    for (int dy = 0; dy < CellPx; dy++)
                        for (int dx = 0; dx < CellPx; dx++)
                        {
                            bool edge = dx == 0 || dy == 0 || dx == CellPx - 2 || dy == CellPx - 2;
                            px[(y0 + dy) * tw + x0 + dx] = edge && ((dx + dy) & 1) == 0 ? ColRockEdge : clear;
                        }
                    continue;
                }
                int jitter = shaking ? ((int)(now * 40f) & 1) * 2 - 1 : 0;
                // cracks: a diagonal and a fork, drawn dark over the grain
                for (int d = 0; d < CellPx - 1; d++)
                {
                    int cx = Mathf.Clamp(d + jitter, 0, CellPx - 1);
                    px[(y0 + d) * tw + x0 + cx] = ColCrack;
                    if (d >= 3 && d < 6) px[(y0 + d) * tw + x0 + Mathf.Clamp(6 - d + jitter, 0, CellPx - 1)] = ColCrack;
                }
            }
        }

        /// <summary>Fill a whole cell, gap included: the unexplored dark has no seams.</summary>
        void PaintFull(int x, int y, Color32 color)
        {
            int tw = tex.width;
            int x0 = x * CellPx;
            int y0 = (level.h - 1 - y) * CellPx;
            for (int dy = 0; dy < CellPx; dy++)
            {
                int row = (y0 + dy) * tw + x0;
                for (int dx = 0; dx < CellPx; dx++) px[row + dx] = color;
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

        void PlaceSprites()
        {
            bool showHero = phase != Phase.Title && phase != Phase.Finished;
            playerSr.enabled = showHero;
            glowSr.enabled = showHero;
            if (showHero)
            {
                float now = Time.time;
                float sink = phase == Phase.Dead || phase == Phase.GameOver ? Mathf.Clamp01((now - deathAt) / 0.9f) : 0f;
                playerSr.transform.position = new Vector3(pPos.x, pPos.y - sink * 0.8f, -1f);
                playerSr.transform.localScale = new Vector3(1f, 1f - sink * 0.6f, 1f);
                playerSr.flipX = !facingRight;
                if (heroFrames != null)
                {
                    bool running = grounded && Mathf.Abs(pVel.x) > 0.5f;
                    if (running) animClock += Time.deltaTime; else animClock = 0f;
                    int frame = !grounded ? 1 : running && ((int)(animClock / 0.14f) % 2 == 1) ? 1 : 0;
                    playerSr.sprite = heroFrames[frame];
                }
                playerSr.color = Color.Lerp(Color.white, new Color(0.35f, 0.25f, 0.25f, 1f), sink);
                var centre = PlayerCentre;
                glowSr.transform.position = new Vector3(centre.x, centre.y, -0.5f);
                float breathe = 1f + 0.04f * Mathf.Sin(now * 3f);
                glowSr.transform.localScale = new Vector3(breathe, breathe, 1f) * (1f - sink);
            }
            foreach (var e in embers)
            {
                if (e.sr == null) continue;
                e.sr.enabled = InWorld || phase == Phase.Dead || phase == Phase.Story;
                var world = new Vector2(e.pos.x + 0.5f, level.h - 1 - e.pos.y + 0.5f);
                float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 9f + e.cell.y);
                e.sr.transform.position = new Vector3(world.x, world.y, -0.8f);
                e.sr.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            for (int k = 0; k < pickupSprites.Count; k++)
            {
                var sr = pickupSprites[k];
                if (sr == null) continue;
                bool live = k < pickupsLeft.Count;
                sr.enabled = live && (allSeen || seen[pickupsLeft[k].y * level.w + pickupsLeft[k].x]);
                if (!live) continue;
                var world = CellCentreWorld(pickupsLeft[k]);
                float bob = 0.08f * Mathf.Sin(Time.time * 3f + pickupsLeft[k].x);
                sr.transform.position = new Vector3(world.x, world.y + bob, -0.7f);
            }
        }

        // ------------------------------------------------------------------ HUD

        float S => Screen.height / 800f;
        float TitleRowY => Screen.height * 0.64f;
        float PageTop => Screen.height * 0.16f;
        float RowH => 54f * S;

        /// <summary>The column every page lays its rows in.</summary>
        Rect PageRect()
        {
            float w = Mathf.Min(760f * S, Screen.width - 48f * S);
            return new Rect((Screen.width - w) / 2f, PageTop, w, Screen.height - PageTop - 40f * S);
        }

        void Layout()
        {
            buttons.Clear();
            float s = S;

            if (Paused)
            {
                LayoutOverlay();
                return;
            }

            if (phase == Phase.Title)
            {
                int n = set.levels.Length;
                float b = 44f * s, gap = 10f * s;
                float total = n * b + (n - 1) * gap;
                float x0 = (Screen.width - total) / 2f;
                float y = TitleRowY;
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
                // the menu row under the chapters
                var names = new List<string> { "How to play", "Wanderer", "Achievements", "Settings", "Controls", "Credits" };
                var acts = new List<Action> { () => OpenPage(Overlay.Help), () => { lookRobe = Look.Robe; lookHat = Look.Hat; lookLantern = Look.Lantern; OpenPage(Overlay.Wanderer); }, () => OpenPage(Overlay.Achievements), () => OpenPage(Overlay.Settings), () => OpenPage(Overlay.Controls), () => OpenPage(Overlay.Credits) };
                if (CanQuit) { names.Add("Quit"); acts.Add(QuitGame); }
                float mw = 138f * s, mh = 40f * s, mgap = 10f * s;
                float mx = (Screen.width - (names.Count * mw + (names.Count - 1) * mgap)) / 2f;
                float my = TitleRowY + 104f * s;
                for (int i = 0; i < names.Count; i++)
                    buttons.Add(new Button { rect = new Rect(mx + i * (mw + mgap), my, mw, mh), label = names[i], act = acts[i], enabled = true });
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
                case Phase.Play:
                    Add($"Grow  [{L(GameAction.Grow)}]", Grow, seeds.Count > 0);
                    Add($"Skip  [{L(GameAction.Skip)}]", Skip);
                    Add(RevealLabel(), Reveal, CanReveal);
                    Add("Menu  [Esc]", OpenMenu);
                    break;
                case Phase.Growing:
                    Add($"Finish  [{L(GameAction.Grow)}]", Primary);
                    Add($"Rewind  [{L(GameAction.Rewind)}]", Rewind);
                    break;
                case Phase.Result:
                    if (!won)
                    {
                        Add($"Rewind  [{L(GameAction.Rewind)}]", Rewind);
                        Add($"Skip  [{L(GameAction.Skip)}]", Skip);
                        Add(RevealLabel(), Reveal, CanReveal);
                    }
                    else
                    {
                        Add($"Skip to door  [{L(GameAction.Skip)}]", Skip);
                    }
                    break;
                case Phase.GameOver:
                    Add("Try again  [Enter]", Retry);
                    Add("Menu  [Esc]", EnterTitle);
                    break;
            }
        }

        void LayoutOverlay()
        {
            float s = S;
            var page = PageRect();
            float bw = 300f * s, bh = 46f * s, gap = 12f * s;
            float cx = Screen.width / 2f;

            void Wide(ref float y, string label, Action act, bool gold = false)
            {
                buttons.Add(new Button { rect = new Rect(cx - bw / 2f, y, bw, bh), label = label, act = act, enabled = true, gold = gold });
                y += bh + gap;
            }

            switch (overlay)
            {
                case Overlay.Menu:
                {
                    float y = PageTop + 96f * s;
                    Wide(ref y, "Resume  [Esc]", CloseOverlay, true);
                    Wide(ref y, "Restart chapter", () => { CloseOverlay(); Retry(); });
                    Wide(ref y, "Chapter select", () => { CloseOverlay(); EnterTitle(); });
                    Wide(ref y, "How to play", () => OpenPage(Overlay.Help));
                    Wide(ref y, "Achievements", () => OpenPage(Overlay.Achievements));
                    Wide(ref y, "Settings", () => OpenPage(Overlay.Settings));
                    Wide(ref y, "Controls", () => OpenPage(Overlay.Controls));
                    Wide(ref y, "Credits", () => OpenPage(Overlay.Credits));
                    if (CanQuit) Wide(ref y, "Quit to desktop", QuitGame);
                    break;
                }
                case Overlay.Settings:
                {
                    float y = PageTop + 80f * s;
                    float small = 46f * s, toggle = 150f * s, rowBh = 40f * s;
                    void Stepper(Action dec, Action inc, bool canDec, bool canInc)
                    {
                        buttons.Add(new Button { rect = new Rect(page.xMax - small * 2f - gap, y, small, rowBh), label = "-", act = dec, enabled = canDec });
                        buttons.Add(new Button { rect = new Rect(page.xMax - small, y, small, rowBh), label = "+", act = inc, enabled = canInc });
                        y += RowH;
                    }
                    void Toggle(string label, Action act, bool gold = false)
                    {
                        buttons.Add(new Button { rect = new Rect(page.xMax - toggle, y, toggle, rowBh), label = label, act = act, enabled = true, gold = gold });
                        y += RowH;
                    }
                    Stepper(() => SetVolume(volume - 1), () => SetVolume(volume + 1), volume > 0, volume < VolumeSteps);
                    Stepper(() => SetMusic(music - 1), () => SetMusic(music + 1), music > 0, music < VolumeSteps);
                    Toggle(muted ? "Off" : "On", ToggleMute);
                    Toggle(shakeOn ? "On" : "Off", ToggleShake);
                    Toggle(Screen.fullScreen ? "On" : "Off", ToggleFullscreen);
                    Toggle(confirmReset ? "Really reset?" : "Reset", ResetProgressClicked, confirmReset);
                    y += 24f * s;
                    Wide(ref y, "Back  [Esc]", Back);
                    break;
                }
                case Overlay.Controls:
                {
                    float y = PageTop + 80f * s;
                    float rowH = 42f * s, keyW = 190f * s, keyH = 36f * s;
                    foreach (var a in InputBridge.Rebindable)
                    {
                        var action = a;
                        bool waiting = listening == action;
                        buttons.Add(new Button
                        {
                            rect = new Rect(page.xMax - keyW, y, keyW, keyH),
                            label = waiting ? "press a key" : InputBridge.Label(action),
                            act = () => { listening = waiting ? null : action; sfx.Select(); },
                            enabled = true,
                            gold = waiting,
                        });
                        y += rowH;
                    }
                    y += rowH + 88f * s;   // the fixed Menu row and the two note lines (the gamepad one wraps) sit above these
                    buttons.Add(new Button { rect = new Rect(cx - bw - gap / 2f, y, bw, bh), label = "Reset to defaults", act = () => { InputBridge.ResetBindings(); listening = null; sfx.Rewind(); }, enabled = true });
                    buttons.Add(new Button { rect = new Rect(cx + gap / 2f, y, bw, bh), label = "Back  [Esc]", act = Back, enabled = true });
                    break;
                }
                case Overlay.Credits:
                case Overlay.Help:
                case Overlay.Achievements:
                {
                    float y = Screen.height - 110f * s;
                    Wide(ref y, "Back  [Esc]", Back);
                    break;
                }
                case Overlay.Wanderer:
                {
                    float y = PageTop + 80f * s;
                    float small = 46f * s, rowBh = 40f * s;
                    void Arrows(Action prev, Action next)
                    {
                        buttons.Add(new Button { rect = new Rect(page.x + page.width * 0.6f, y, small, rowBh), label = "<", act = prev, enabled = true });
                        buttons.Add(new Button { rect = new Rect(page.x + page.width * 0.6f + small + gap, y, small, rowBh), label = ">", act = next, enabled = true });
                        y += RowH;
                    }
                    Arrows(() => StepLook(0, -1), () => StepLook(0, 1));
                    Arrows(() => StepLook(1, -1), () => StepLook(1, 1));
                    Arrows(() => StepLook(2, -1), () => StepLook(2, 1));
                    y = Screen.height - 110f * s;
                    Wide(ref y, "Back  [Esc]", Back);
                    break;
                }
                case Overlay.Wall:
                {
                    float y = Screen.height - 110f * s;
                    float bw3 = 220f * s;
                    if (wallStage == 0)
                        buttons.Add(new Button { rect = new Rect(cx - bw3 - gap / 2f, y, bw3, bh), label = "Say it  [Enter]", act = WallSubmitAnswer, enabled = wallTyped.Length > 0, gold = true });
                    else if (wallStage == 1)
                        buttons.Add(new Button { rect = new Rect(cx - bw3 - gap / 2f, y, bw3, bh), label = "Press in  [Enter]", act = WallPressInitials, enabled = Wall.Clean(wallInitials).Length > 0, gold = true });
                    else
                        buttons.Add(new Button { rect = new Rect(cx - bw3 - gap / 2f, y, bw3, bh), label = "Share on GitHub  [Enter]", act = WallShare, enabled = Wall.CleanUser(wallUser).Length > 0, gold = true });
                    buttons.Add(new Button { rect = new Rect(cx + gap / 2f, y, bw3, bh), label = "Back  [Esc]", act = Back, enabled = true });
                    break;
                }
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
            stDeaths = Make(14, FontStyle.Normal, TextAnchor.UpperCenter, "b45c52");
            stHint = Make(18, FontStyle.Italic, TextAnchor.LowerLeft, "b7bfd1");
            stBody = Make(20, FontStyle.Normal, TextAnchor.MiddleCenter, "b7bfd1");
            stButton = Make(19, FontStyle.Bold, TextAnchor.MiddleCenter, "e8ecf5");
            stRow = Make(21, FontStyle.Normal, TextAnchor.MiddleLeft, "e8ecf5");
            stRowValue = Make(17, FontStyle.Normal, TextAnchor.MiddleLeft, "8a93a8");
            stVoice = Make(17, FontStyle.Italic, TextAnchor.MiddleLeft, "fff1cc");
        }

        /// <summary>The lantern's line, in a small bubble above the wanderer, typed out.</summary>
        void DrawVoice()
        {
            if (voiceLine.Length == 0 || Paused || phase == Phase.Title || phase == Phase.Finished) return;
            float s = S;
            string shown = voiceLine.Substring(0, Mathf.Min(voiceShown, voiceLine.Length));
            if (shown.Length == 0) return;
            var head = cam.WorldToScreenPoint(new Vector3(pPos.x, pPos.y + PlayerH + 0.4f, 0f));
            float maxW = Mathf.Min(440f * s, Screen.width - 48f * s);
            var content = new GUIContent(voiceLine);
            float textW = Mathf.Min(maxW - 28f * s, stVoice.CalcSize(content).x + 4f * s);
            float textH = stVoice.CalcHeight(content, textW);
            float w = textW + 28f * s, h = textH + 18f * s;
            float x = Mathf.Clamp(head.x - w * 0.3f, 16f * s, Screen.width - w - 16f * s);
            float y = Mathf.Clamp(Screen.height - head.y - h - 10f * s, 100f * s, Screen.height - h - 140f * s);
            Panel(new Rect(x, y, w, h), new Color(0.07f, 0.09f, 0.14f, 0.92f));
            Panel(new Rect(x, y + h - 3f * s, w, 3f * s), new Color(1f, 0.85f, 0.6f, 0.9f));
            // the little tail toward the lantern
            float tx = Mathf.Clamp(head.x, x + 12f * s, x + w - 12f * s);
            Panel(new Rect(tx - 3f * s, y + h, 6f * s, 6f * s), new Color(1f, 0.85f, 0.6f, 0.9f));
            GUI.Label(new Rect(x + 14f * s, y + 9f * s, textW, textH), shown, stVoice);
        }

        /// <summary>Ten segments, lit up to the value.</summary>
        void Meter(float x, float y, int value)
        {
            float s = S, segW = 16f * s, segH = 16f * s, g = 4f * s;
            for (int i = 0; i < VolumeSteps; i++)
                Panel(new Rect(x + i * (segW + g), y, segW, segH), i < value ? new Color(0.98f, 0.85f, 0.45f, 1f) : new Color(1f, 1f, 1f, 0.12f));
        }

        void DrawOverlay()
        {
            float s = S;
            var page = PageRect();
            Panel(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, overlay == Overlay.Menu ? 0.78f : 0.92f));
            switch (overlay)
            {
                case Overlay.Menu:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "PAUSED", stBanner);
                    string where = phase == Phase.Title ? "" : $"chapter {levelIndex + 1} of {set.levels.Length}   {level.name}   lanterns {lives}";
                    GUI.Label(new Rect(0, PageTop + 62f * s, Screen.width, 26f * s), where, stSmallCentre);
                    break;
                }
                case Overlay.Settings:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "SETTINGS", stBanner);
                    float y = PageTop + 80f * s;
                    float rowBh = 40f * s;
                    void Row(string label, string value)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.42f, rowBh), label, stRow);
                        if (value.Length > 0) GUI.Label(new Rect(page.x + page.width * 0.42f, y, page.width * 0.36f, rowBh), value, stRowValue);
                        y += RowH;
                    }
                    int done = 0;
                    for (int i = 0; i < set.levels.Length; i++) if (Solved(i)) done++;
                    Row("Master volume", "");
                    Meter(page.x + page.width * 0.42f, y - RowH + 12f * s, volume);
                    Row("Music", "");
                    Meter(page.x + page.width * 0.42f, y - RowH + 12f * s, music);
                    Row("Sound", muted ? "everything silent (M in game)" : "");
                    Row("Screen shake", "on death");
                    Row("Fullscreen", "");
                    Row("Progress", confirmReset ? "click again to wipe it" : $"{done} of {set.levels.Length} origins found, {unlocked + 1} chapters open");
                    break;
                }
                case Overlay.Controls:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "CONTROLS", stBanner);
                    float y = PageTop + 80f * s;
                    float rowH = 42f * s, keyH = 36f * s;
                    foreach (var a in InputBridge.Rebindable)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.6f, keyH), InputBridge.Describe(a), stRow);
                        y += rowH;
                    }
                    GUI.Label(new Rect(page.x, y, page.width * 0.6f, keyH), "Menu", stRow);
                    GUI.Label(new Rect(page.xMax - 190f * s, y, 190f * s, keyH), "Esc, always", stRowValue);
                    y += rowH;
                    GUI.Label(new Rect(page.x, y, page.width, 26f * s),
                        listening.HasValue ? "press the new key, or Esc to keep the old one" : "click a key to change it. The arrows, W and Enter always work as well.", stRowValue);
                    y += 28f * s;
                    GUI.Label(new Rect(page.x, y, page.width, 52f * s),
                        (InputBridge.PadPresent ? "gamepad connected: " : "gamepad: ") + "stick or d-pad move, A jump, X plant, B grow, Y rewind, LB reveal, RB skip, Start menu", stRowValue);
                    break;
                }
                case Overlay.Help:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "HOW TO PLAY", stBanner);
                    float y = PageTop + 76f * s;
                    void Line(string head, string body)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.22f, 52f * s), head, stRow);
                        GUI.Label(new Rect(page.x + page.width * 0.22f, y, page.width * 0.78f, 52f * s), body, stRowValue);
                        y += 58f * s;
                    }
                    Line("The outline", "Your lantern shows the ghost of something that grew here from one or more seeds, under the law written at the top of the screen. Explore to see all of it.");
                    Line("Seeds", "A seed is a beginning: everything that grew here grew from one. Some chapters hand you the seeds; in others they lie in the dark, so fetch them first. The count at the top right says how many the ruin needs.");
                    Line("Plant", $"Work out where the shape began, stand on that cell, press {L(GameAction.Plant)}. With two or three seeds, plant every one before you grow: each grows its own shape and they merge, as the ruin did.");
                    Line("Get clear", "Growth is solid and takes whoever stands inside it, so step out of the outline before you grow. Embers, acid, spikes and vents kill; the void takes whoever falls. Three lanterns per chapter.");
                    Line("Grow", $"Press {L(GameAction.Grow)}. Gold cells match the outline, red cells do not. An exact match opens the door, and the growth is ground you can walk on: it is your bridge.");
                    Line("Rewind", $"{L(GameAction.Rewind)} clears a wrong growth and keeps your seeds where they are. Stand on a seed and press {L(GameAction.Plant)} to take it back and try another cell. After two failed tries {L(GameAction.Reveal)} shows the origins; {L(GameAction.Skip)} skips a chapter.");
                    Line("Laws", "The same law makes different shapes in different places: rock clips growth, a post casts a shadow through it, and a bloom that reaches an ember puts it out.");
                    break;
                }
                case Overlay.Wall:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "THE WALL", stBanner);
                    float cell = Mathf.Min(9f * s, page.width / (wallCipherW + 4f));
                    void Draw(List<Vector2Int> cells, float x0, float y0, Color col)
                    {
                        foreach (var c in cells) Panel(new Rect(x0 + c.x * cell, y0 + c.y * cell, cell - 1f, cell - 1f), col);
                    }
                    float y = PageTop + 62f * s;
                    GUI.Label(new Rect(page.x, y, page.width, 24f * s), "The spark law took every letter once: born beside exactly one, nothing survives. The key:", stRowValue);
                    y += 30f * s;
                    float keyW = wallKeyW * cell;
                    Draw(wallKeyPlain, page.x, y, new Color(0.72f, 0.66f, 0.54f, 1f));
                    Draw(wallKeySparked, page.x + keyW + 40f * s, y, new Color(0.31f, 0.71f, 0.74f, 1f));
                    GUI.Label(new Rect(page.x + keyW * 2f + 60f * s, y + 2f * cell, page.width * 0.4f, 40f * s), "plain, then sparked", stRowValue);
                    y += 9f * cell + 16f * s;
                    GUI.Label(new Rect(page.x, y, page.width, 24f * s), "What the wall says, sparked:", stRowValue);
                    y += 30f * s;
                    Draw(wallCipher, page.x, y, new Color(0.31f, 0.71f, 0.74f, 1f));
                    y += 9f * cell + 20f * s;
                    string caret = ((int)(Time.time * 2f) & 1) == 0 ? "_" : " ";
                    if (wallStage == 0)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.3f, 36f * s), "It says:", stRow);
                        Panel(new Rect(page.x + page.width * 0.3f, y, page.width * 0.5f, 36f * s), new Color(0.12f, 0.14f, 0.2f, 1f));
                        GUI.Label(new Rect(page.x + page.width * 0.3f + 12f * s, y, page.width * 0.5f, 36f * s), wallTyped.ToUpperInvariant() + caret, stRow);
                        GUI.Label(new Rect(page.x, y + 40f * s, page.width, 24f * s), $"{WallAnswerLength.Length} letters. Type it, Enter to say it. Typing needs a keyboard.", stRowValue);
                    }
                    else
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.3f, 36f * s), wallStage == 1 ? "Your letters:" : "On the wall:", stRow);
                        Panel(new Rect(page.x + page.width * 0.3f, y, page.width * 0.2f, 36f * s), new Color(0.12f, 0.14f, 0.2f, 1f));
                        GUI.Label(new Rect(page.x + page.width * 0.3f + 12f * s, y, page.width * 0.2f, 36f * s), (wallStage == 1 ? wallInitials.ToUpperInvariant() + caret : Wall.Mine), stRow);
                        GUI.Label(new Rect(page.x + page.width * 0.55f, y, page.width * 0.45f, 36f * s), $"{Wall.Names.Count} on the wall" + (Wall.Fetched ? "" : " (the shipped list; the shared one could not be fetched)"), stRowValue);
                        if (wallStage == 2)
                        {
                            y += 44f * s;
                            GUI.Label(new Rect(page.x, y, page.width * 0.3f, 36f * s), "GitHub account:", stRow);
                            Panel(new Rect(page.x + page.width * 0.3f, y, page.width * 0.5f, 36f * s), new Color(0.12f, 0.14f, 0.2f, 1f));
                            GUI.Label(new Rect(page.x + page.width * 0.3f + 12f * s, y, page.width * 0.5f, 36f * s), wallUser + caret, stRow);
                            y += 40f * s;
                            GUI.Label(new Rect(page.x, y, page.width, 48f * s), "The account you will submit from. The proof is made for that account alone.", stRowValue);
                            y -= 84f * s;
                        }
                    }
                    y += 70f * s + (wallStage == 2 ? 84f * s : 0f);
                    if (wallNote.Length > 0) GUI.Label(new Rect(page.x, y, page.width, 48f * s), wallNote, stRowValue);
                    break;
                }
                case Overlay.Wanderer:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "WANDERER", stBanner);
                    int found = OriginsFound; bool secret = SecretKnown;
                    if (heroBase0 != null)
                    {
                        // the preview: the current frame, four times life size, on the right of the page
                        var tex = Look.Build(heroBase0, Look.Robe, Look.Hat, Look.Lantern);
                        float pw = 48f * 4f * s, ph = 64f * 4f * s;
                        var pr = new Rect(page.xMax - pw - 8f * s, PageTop + 76f * s, pw, ph);
                        Panel(new Rect(pr.x - 12f * s, pr.y - 12f * s, pw + 24f * s, ph + 24f * s), new Color(0.08f, 0.1f, 0.15f, 1f));
                        GUI.DrawTexture(pr, tex, ScaleMode.StretchToFill, true);
                    }
                    float y = PageTop + 80f * s;
                    void Row(string label, Look.Option o)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.2f, 40f * s), label, stRow);
                        bool open = Look.Open(o, found, secret);
                        GUI.Label(new Rect(page.x + page.width * 0.2f, y, page.width * 0.4f, 40f * s), open ? o.name : $"{o.name}  ({Look.Requirement(o)})", open ? stRow : stRowValue);
                        y += RowH;
                    }
                    Row("Robe", Look.Robes[lookRobe]);
                    Row("Hat", Look.Hats[lookHat]);
                    Row("Lantern", Look.Lanterns[lookLantern]);
                    y += 12f * s;
                    GUI.Label(new Rect(page.x, y, page.width * 0.62f, 72f * s),
                        $"{found} of {set.levels.Length} origins found. More cloth, hats and lights open as you find them, and one light is only for whoever finds where the road does not lead. Saved with your progress.", stRowValue);
                    break;
                }
                case Overlay.Achievements:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "ACHIEVEMENTS", stBanner);
                    GUI.Label(new Rect(0, PageTop + 62f * s, Screen.width, 26f * s), $"{Achievements.Count()} of {Achievements.All.Length}", stSmallCentre);
                    float y = PageTop + 96f * s;
                    float rowH = Mathf.Min(40f * s, (Screen.height - 130f * s - y) / Achievements.All.Length);
                    foreach (var a in Achievements.All)
                    {
                        bool has = Achievements.Has(a.id);
                        var old = GUI.color;
                        GUI.color = has ? new Color(1f, 0.9f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.45f);
                        GUI.Label(new Rect(page.x, y, page.width * 0.32f, rowH), has || !a.hidden ? a.title : "???", stRow);
                        GUI.Label(new Rect(page.x + page.width * 0.32f, y, page.width * 0.68f, rowH), has || !a.hidden ? a.text : "Something the road does not lead to.", stRowValue);
                        GUI.color = old;
                        y += rowH;
                    }
                    break;
                }
                case Overlay.Credits:
                {
                    GUI.Label(new Rect(0, PageTop - 10f * s, Screen.width, 76f * s), "POINT OF ORIGIN", stBanner);
                    GUI.Label(new Rect(page.x, PageTop + 70f * s, page.width, 30f * s), "made in a weekend for CPGD's World's First Game Jam, theme ORIGIN", stSmallCentre);
                    float y = PageTop + 120f * s;
                    void Line(string who, string what)
                    {
                        GUI.Label(new Rect(page.x, y, page.width * 0.4f, 34f * s), who, stRow);
                        GUI.Label(new Rect(page.x + page.width * 0.4f, y, page.width * 0.6f, 34f * s), what, stRowValue);
                        y += 40f * s;
                    }
                    Line("Design, code, levels", "Londo (Londopy)");
                    Line("Simulation core", "Odin: the cellular automaton, shipped as origin_sim.dll");
                    Line("Build tooling", "Nexium: bindings generator, level compiler, build orchestrator");
                    Line("Engine", "Unity 6, driven from the command line");
                    Line("Effects", "Houdini: ruin skylines, growth burst, stone grain");
                    Line("Character and props", "Blender: the wanderer, the ember, the seed");
                    Line("Sound", "synthesised at start-up, no audio files");
                    Line("Thanks", "Cal Poly Game Development Club");
                    break;
                }
            }
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
            for (int bi = 0; bi < buttons.Count; bi++)
            {
                var b = buttons[bi];
                bool hot = b.enabled && (b.rect.Contains(gui) || bi == padFocus);
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
                GUI.Label(new Rect(24f * s, 16f * s, Screen.width * 0.6f, 40f * s),
                    $"{levelIndex + 1:00} / {set.levels.Length:00}    {level.name}", stH1);
                GUI.Label(new Rect(24f * s, 54f * s, Screen.width * 0.55f, 48f * s),
                    $"{lawText}   ({level.rule}, {level.steps} generation{(level.steps == 1 ? "" : "s")})", stSmall);

                bool playing = phase == Phase.Play;
                string right = playing
                    ? (fetching ? $"Seeds {seeds.Count} / {level.seeds}   in hand {carried}" : $"Seeds {seeds.Count} / {level.seeds}")
                    : $"Generation {sim.Generation} / {level.steps}";
                GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 16f * s, Screen.width * 0.6f, 40f * s), right, stH1Right);
                var here = CellOf(PlayerCentre);
                bool onSeed = InBounds(here) && sim.Cells[here.y * level.w + here.x] == Sim.Alive;
                string sub;
                if (!playing && phase != Phase.Dead) sub = won ? "the door is open: walk to it" : $"match {Mathf.FloorToInt(match * 100f)}%   attempt {attempts}";
                else if (phase == Phase.Dead) sub = "";
                else if (onSeed) sub = "you stand in your seed: move away before you grow";
                else if (fetching && carried == 0 && seeds.Count < level.seeds) sub = pickupsLeft.Count > 0 ? "the seeds lie somewhere in the dark: find them" : "";
                else if (secretFound && here == secretCell) sub = $"{L(GameAction.Plant)} reads the wall";
                else if (attempts > 0) sub = $"attempt {attempts + 1}";
                else sub = $"find where it began, stand there, press {L(GameAction.Plant)}";
                sub += $"   lanterns {lives}";
                GUI.Label(new Rect(Screen.width * 0.4f - 24f * s, 54f * s, Screen.width * 0.6f, 30f * s), sub, stSmallRight);

                string hint = attempts > 0 && !string.IsNullOrEmpty(level.tip) ? "Tip: " + level.tip : level.hint;
                float buttonsWidth = (Paused ? 3 : buttons.Count) * 180f * s + 40f * s;
                if (tut == Tutorial.Off)
                    GUI.Label(new Rect(24f * s, Screen.height - 120f * s, Screen.width - buttonsWidth - 48f * s, 88f * s), hint, stHint);
                else if (!Paused && phase != Phase.Dead)
                {
                    // the tutorial prompt: one instruction at a time, under the chapter header
                    float pw = Mathf.Min(760f * s, Screen.width - 48f * s), ph = 66f * s;
                    var r = new Rect((Screen.width - pw) / 2f, 96f * s, pw, ph);
                    Panel(r, new Color(0.07f, 0.09f, 0.14f, 0.92f));
                    Panel(new Rect(r.x, r.y, 5f * s, ph), new Color(0.98f, 0.85f, 0.45f, 1f));
                    GUI.Label(new Rect(r.x + 20f * s, r.y, pw - 32f * s, ph), TutorialText(), stBody);
                }

                GUI.Label(new Rect(0, Screen.height - 26f * s, Screen.width, 22f * s),
                    (muted ? "sound off   " : "") +
                    $"{L(GameAction.Left)} {L(GameAction.Right)} move   {L(GameAction.Jump)} jump   {L(GameAction.Plant)} plant   {L(GameAction.Grow)} grow   " +
                    $"{L(GameAction.Rewind)} rewind   {L(GameAction.Skip)} skip   {L(GameAction.Mute)} sound   Esc menu", stSmallCentre);
            }

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
                        "You wake in the dark among the ruins of something that grew. Run, jump, and find where each ruin began.\n" +
                        "Plant a seed there, get clear, and grow it back: the growth is the ground that carries you to the door.", stBody);
                    int done = 0;
                    for (int i = 0; i < set.levels.Length; i++) if (Solved(i)) done++;
                    GUI.Label(new Rect(0, TitleRowY - 30f * s, Screen.width, 24f * s),
                        (done == 0 ? "chapters" : $"chapters   ({done} of {set.levels.Length} found)") + (PlayerPrefs.GetInt(KeySecret, 0) != 0 ? "   ·   the first seed is yours" : ""), stSmallCentre);
                    {
                        // how many lanterns each chapter has taken, under its button; nothing until it has taken one
                        int n = set.levels.Length; float b = 44f * s, cg = 10f * s;
                        float x0 = (Screen.width - (n * b + (n - 1) * cg)) / 2f;
                        for (int i = 0; i < n; i++)
                        {
                            int d = PlayerPrefs.GetInt(DeathsKey(i), 0);
                            if (d > 0) GUI.Label(new Rect(x0 + i * (b + cg) - cg / 2f, TitleRowY + b + 3f * s, b + cg, 18f * s), $"†{d}", stDeaths);
                        }
                    }
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(0, TitleRowY + 54f * s, Screen.width, 40f * s),
                        done == 0 ? "click or press Enter to begin" : "click or press Enter to continue", stBody);
                    GUI.color = old;
                    GUI.Label(new Rect(24f * s, Screen.height - 60f * s, Screen.width - 48f * s, 30f * s),
                        "made for CPGD's World's First Game Jam, theme ORIGIN   |   Odin + Nexium + Unity + Houdini", stSmallRight);
                    GUI.Label(new Rect(24f * s, Screen.height - 60f * s, Screen.width * 0.5f, 30f * s),
                        (muted ? "sound off (M)" : "M sound") + (CanQuit ? "   Esc quit" : "") + "   v" + Application.version, stSmall);
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
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s),
                        tut != Tutorial.Off ? "click or press Enter to wake. This first chapter shows you the way." : "click or press Enter to wake", stBody);
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
                    float a = Mathf.Clamp01(resultTime * 4f);
                    a *= 1f - Mathf.Clamp01((resultTime - (won ? 4f : 2.4f)) / 0.6f);
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
                        ? (attempts == 1 ? "first try" : $"on attempt {attempts}") + "   -   the door is open: walk to it"
                        : $"{missing} missing, {extra} astray   -   {L(GameAction.Rewind)} to rewind, then move your seeds";
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
                    GUI.Label(new Rect(0, Screen.height - 150f * s, Screen.width, 40f * s), "click or press Enter for the menu", stBody);
                    break;
                }
            }

            DrawVoice();
            if (Paused) DrawOverlay();
            DrawButtons(gui);
            if (toastTitle.Length > 0)
            {
                // an achievement, sliding in at the top right
                float age = Time.time - toastAt;
                float slide = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(age / 0.35f)) + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - ToastSeconds + 0.4f) / 0.4f));
                float w = 340f * s, h = 58f * s;
                var r = new Rect(Screen.width - w - 24f * s + slide * (w + 40f * s), 96f * s, w, h);
                Panel(r, new Color(0.07f, 0.09f, 0.14f, 0.94f));
                Panel(new Rect(r.x, r.y, 5f * s, h), new Color(1f, 0.85f, 0.45f, 1f));
                GUI.Label(new Rect(r.x + 18f * s, r.y + 6f * s, w - 24f * s, 22f * s), "achievement", stSmall);
                GUI.Label(new Rect(r.x + 18f * s, r.y + 26f * s, w - 24f * s, 28f * s), toastTitle, stRow);
            }
        }
    }
}
