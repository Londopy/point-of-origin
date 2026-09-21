// Run inside the open Editor, from the repository root, with the game in Play mode:
//     unity command eval_file --project-path unity/PointOfOrigin tools/editor/tutorial_walk.cs
// Walk the tutorial state machine on a fresh chapter 1 by faking what the player does, reporting the prompt at each step.
var gc = UnityEngine.Object.FindAnyObjectByType<PointOfOrigin.GameController>();
var t = gc.GetType();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Reflection.MethodInfo M(string n) => t.GetMethod(n, F);
object G(string n) => t.GetField(n, F).GetValue(gc);
void S(string n, object v) => t.GetField(n, F).SetValue(gc, v);
var sb = new System.Text.StringBuilder();
string Step() => $"{G("tut")}: {M("TutorialText").Invoke(gc, null)}";
void Tick(int n = 1) { for (int i = 0; i < n; i++) M("UpdateTutorial").Invoke(gc, new object[] { 0.1f }); }
UnityEngine.PlayerPrefs.DeleteAll();
S("unlocked", 0); S("solvedMask", 0);
M("StartLevel").Invoke(gc, new object[] { 0 });
sb.Append("story: " + Step() + "\n");
M("Primary").Invoke(gc, null);
S("pVel", new UnityEngine.Vector2(7f, 0f)); Tick(6);
sb.Append("after running: " + Step() + "\n");
S("jumps", 1); Tick();
sb.Append("after a jump: " + Step() + "\n");
var lv = (PointOfOrigin.Level)G("level");
S("pPos", new UnityEngine.Vector2(21.5f, lv.h - 1 - 14)); Tick();
sb.Append("on the stone: " + Step() + "\n");
M("ToggleSeed").Invoke(gc, new object[] { new UnityEngine.Vector2Int(21, 14) }); Tick();
sb.Append("planted: " + Step() + "\n");
S("pPos", new UnityEngine.Vector2(15.5f, lv.h - 1 - 12)); S("grounded", true); Tick();
sb.Append("back on the rim: " + Step() + "\n");
S("pPos", new UnityEngine.Vector2(20.5f, lv.h - 1 - 15)); Tick();
sb.Append("down in the chasm again: " + Step() + "\n");
S("pPos", new UnityEngine.Vector2(15.5f, lv.h - 1 - 12)); Tick();
M("Grow").Invoke(gc, null); Tick();
sb.Append("growing: " + Step() + "\n");
M("Primary").Invoke(gc, null); Tick();
sb.Append($"finished: won={G("won")} " + Step() + "\n");
M("Rewind").Invoke(gc, null); Tick();
sb.Append("rewound: " + Step() + "\n");
M("Grow").Invoke(gc, null); M("Primary").Invoke(gc, null); Tick();
S("pPos", new UnityEngine.Vector2(52.5f, lv.h - 1 - 12));
sb.Append($"solvedMask={G("solvedMask")} tut={G("tut")}");
return sb.ToString();
