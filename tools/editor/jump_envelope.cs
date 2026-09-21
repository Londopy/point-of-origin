// Run inside the open Editor, from the repository root, with the game in Play mode:
//     unity command eval_file --project-path unity/PointOfOrigin tools/editor/jump_envelope.cs
// Measure the jump envelope: from a one-cell block, which (dx, dy) landing cells can the real physics reach? Writes a table.
var gc = UnityEngine.Object.FindAnyObjectByType<PointOfOrigin.GameController>();
var t = gc.GetType();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Reflection.MethodInfo M(string n) => t.GetMethod(n, F);
object G(string n) => t.GetField(n, F).GetValue(gc);
void S(string n, object v) => t.GetField(n, F).SetValue(gc, v);
var stepPlayer = M("StepPlayer");
M("StartLevel").Invoke(gc, new object[] { 1 });   // chapter 2: 96 x 18, room to move
if (G("phase").ToString() == "Story") M("Primary").Invoke(gc, null);
((System.Collections.IList)G("embers")).Clear();
var lv = (PointOfOrigin.Level)G("level");
int w = lv.w, h = lv.h;
var rock = (byte[])G("rock");
byte ROCK = PointOfOrigin.Sim.Rock;
void Layout(int ax, int ay, int bx, int by)
{
    for (int i = 0; i < rock.Length; i++) rock[i] = 0;
    for (int x = 0; x < w; x++) rock[(h - 1) * w + x] = ROCK;          // a floor along the bottom row
    rock[(ay + 1) * w + ax] = ROCK;                                      // the block you start on
    if (by + 1 < h - 1) rock[(by + 1) * w + bx] = ROCK;                 // the block you land on (unless it is the floor)
}
var sb = new System.Text.StringBuilder();
sb.Append("dy\\dx");
for (int dx = 0; dx <= 7; dx++) sb.Append($"\t{dx}");
sb.Append("\n");
int ax0 = 40, ay0 = 9;
var reach = new System.Collections.Generic.List<string>();
for (int dy = -4; dy <= 6; dy++)
{
    sb.Append($"{dy}");
    for (int dx = 0; dx <= 7; dx++)
    {
        int bx = ax0 + dx, by = ay0 + dy;
        bool ok = false;
        if (by >= h - 1) { sb.Append("\t-"); continue; }
        if (dx == 0 && dy == 0) { sb.Append("\t·"); continue; }
        Layout(ax0, ay0, bx, by);
        float[] preRuns = { 0f, 0.08f, 0.16f, 0.25f, 0.35f, 0.5f, 0.7f };
        float[] holds = { 0.04f, 0.1f, 0.18f, 0.3f, 0.6f };
        foreach (var pre in preRuns) { foreach (var hold in holds) { foreach (var brake in new[] { false, true }) {
            S("pPos", new UnityEngine.Vector2(ax0 + 0.5f, h - 1 - ay0));
            S("pVel", UnityEngine.Vector2.zero); S("grounded", true); S("coyote", 0.1f); S("jumpBuffer", 0f);
            float dt = 1f / 60f, time = 0f; bool jumped = false;
            for (int i = 0; i < 200 && !ok; i++)
            {
                var pos = (UnityEngine.Vector2)G("pPos");
                float move = dx == 0 ? 0f : 1f;
                if (brake && dx != 0 && System.Math.Abs(pos.x - (bx + 0.5f)) < 0.35f) move = 0f;
                if (!jumped && time >= pre) { S("jumpBuffer", 0.12f); jumped = true; }
                bool held = jumped && time < pre + hold;
                stepPlayer.Invoke(gc, new object[] { dt, move, held });
                time += dt;
                pos = (UnityEngine.Vector2)G("pPos");
                int fx = (int)System.Math.Floor(pos.x), fy = h - 1 - (int)System.Math.Floor(pos.y + 0.01f);
                if ((bool)G("grounded") && fx == bx && fy == by && time > pre + 0.05f) ok = true;
            }
            if (ok) break; } if (ok) break; } if (ok) break; }
        sb.Append(ok ? "\tY" : "\t.");
        if (ok) reach.Add($"{dx},{dy}");
    }
    sb.Append("\n");
}
sb.Append("REACH " + string.Join(" ", reach) + "\n");
System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../../nx-out/envelope.txt"), sb.ToString());
return sb.ToString();
