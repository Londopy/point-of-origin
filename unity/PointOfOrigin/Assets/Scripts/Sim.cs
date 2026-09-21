using System;
#if !UNITY_WEBGL
using PointOfOrigin.Native;
#endif

namespace PointOfOrigin
{
    /// <summary>
    /// A grid of Dead, Alive and Rock cells. On desktop it lives inside origin_sim.dll (the Odin core);
    /// in the browser, where no native library can ship, the same automaton runs from SimCore, the C#
    /// copy. Refresh() copies the state into Cells and Ages for painting.
    /// </summary>
    public sealed class Sim : IDisposable
    {
        public const byte Dead = 0;
        public const byte Alive = 1;
        public const byte Rock = 2;

        /// <summary>Use the C# core even where the native one exists: the browser always does, the desktop can, to check the two agree.</summary>
#if UNITY_WEBGL
        public static bool UseManaged { get => true; set { } }
#else
        public static bool UseManaged;
#endif
        public static bool ManagedInUse => UseManaged;

        public readonly int W;
        public readonly int H;
        public readonly byte[] Cells;
        public readonly ushort[] Ages;

        readonly SimCore core;
#if !UNITY_WEBGL
        IntPtr handle;
#endif

        public static int NativeVersion
        {
            get
            {
#if UNITY_WEBGL
                return -1;
#else
                return UseManaged ? -1 : PoNative.po_version();
#endif
            }
        }

        public Sim(int w, int h)
        {
            W = w;
            H = h;
            Cells = new byte[w * h];
            Ages = new ushort[w * h];
            if (UseManaged) core = new SimCore(w, h);
#if !UNITY_WEBGL
            else
            {
                handle = PoNative.po_create(w, h);
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException($"po_create({w}, {h}) returned null");
            }
#endif
        }

#if UNITY_WEBGL
        public int Generation => core.Generation;
        public int AliveCount => core.AliveCount;
        public void SetRule(uint birth, uint survive) => core.SetRule(birth, survive);
        public void ClearLife() => core.ClearLife();
        public void ClearAll() => core.ClearAll();
        public void Set(int x, int y, byte value) => core.Set(x, y, value);
        public byte Get(int x, int y) => core.Get(x, y);
        public void Load(byte[] cells) => core.Load(cells);
        public int Step(int n = 1) => core.Step(n);
        public float Compare(byte[] target) => core.Compare(target);

        public void Refresh()
        {
            core.CopyCells(Cells);
            core.CopyAges(Ages);
        }

        public void Dispose() { }
#else
        public int Generation => core != null ? core.Generation : PoNative.po_generation(handle);
        public int AliveCount => core != null ? core.AliveCount : PoNative.po_alive_count(handle);

        public void SetRule(uint birth, uint survive) { if (core != null) core.SetRule(birth, survive); else PoNative.po_set_rule(handle, birth, survive); }
        public void ClearLife() { if (core != null) core.ClearLife(); else PoNative.po_clear_life(handle); }
        public void ClearAll() { if (core != null) core.ClearAll(); else PoNative.po_clear_all(handle); }
        public void Set(int x, int y, byte value) { if (core != null) core.Set(x, y, value); else PoNative.po_set_cell(handle, x, y, value); }
        public byte Get(int x, int y) => core != null ? core.Get(x, y) : PoNative.po_get_cell(handle, x, y);
        public void Load(byte[] cells) { if (core != null) core.Load(cells); else PoNative.po_load(handle, cells, cells.Length); }
        public int Step(int n = 1) => core != null ? core.Step(n) : PoNative.po_step(handle, n);
        public float Compare(byte[] target) => core != null ? core.Compare(target) : PoNative.po_compare(handle, target, target.Length);

        /// <summary>Pull the core's cells and ages into the managed arrays.</summary>
        public void Refresh()
        {
            if (core != null)
            {
                core.CopyCells(Cells);
                core.CopyAges(Ages);
                return;
            }
            PoNative.po_copy_cells(handle, Cells, Cells.Length);
            PoNative.po_copy_ages(handle, Ages, Ages.Length);
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            PoNative.po_destroy(handle);
            handle = IntPtr.Zero;
        }
#endif
    }
}
