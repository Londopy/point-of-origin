using System;
using PointOfOrigin.Native;

namespace PointOfOrigin
{
    /// <summary>
    /// A grid living inside origin_sim.dll (the Odin core). Cells are Dead, Alive
    /// or Rock; Refresh() copies the native state into Cells and Ages for painting.
    /// </summary>
    public sealed class Sim : IDisposable
    {
        public const byte Dead = 0;
        public const byte Alive = 1;
        public const byte Rock = 2;

        public readonly int W;
        public readonly int H;
        public readonly byte[] Cells;
        public readonly ushort[] Ages;

        IntPtr handle;

        public static int NativeVersion => PoNative.po_version();

        public Sim(int w, int h)
        {
            W = w;
            H = h;
            handle = PoNative.po_create(w, h);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"po_create({w}, {h}) returned null");
            Cells = new byte[w * h];
            Ages = new ushort[w * h];
        }

        public int Generation => PoNative.po_generation(handle);
        public int AliveCount => PoNative.po_alive_count(handle);

        public void SetRule(uint birth, uint survive) => PoNative.po_set_rule(handle, birth, survive);
        public void ClearLife() => PoNative.po_clear_life(handle);
        public void ClearAll() => PoNative.po_clear_all(handle);
        public void Set(int x, int y, byte value) => PoNative.po_set_cell(handle, x, y, value);
        public byte Get(int x, int y) => PoNative.po_get_cell(handle, x, y);
        public void Load(byte[] cells) => PoNative.po_load(handle, cells, cells.Length);
        public int Step(int n = 1) => PoNative.po_step(handle, n);
        public float Compare(byte[] target) => PoNative.po_compare(handle, target, target.Length);

        /// <summary>Pull the native cells and ages into the managed arrays.</summary>
        public void Refresh()
        {
            PoNative.po_copy_cells(handle, Cells, Cells.Length);
            PoNative.po_copy_ages(handle, Ages, Ages.Length);
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            PoNative.po_destroy(handle);
            handle = IntPtr.Zero;
        }
    }
}
