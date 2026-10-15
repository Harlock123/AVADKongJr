using AVADKongJr.Engine;

namespace AVADKongJr.Tests;

/// <summary>How the computer Kip gets on, stage by stage (set KONGJR_PROBE to run it, for tuning; KONGJR_PROBE=lazy for one that just climbs about at random).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_plays()
    {
        string? mode = Environment.GetEnvironmentVariable("KONGJR_PROBE");
        if (mode is null) return;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "kongjr-probe-" + Guid.NewGuid().ToString("N")))) { Silent = true };
            g.StartGame();
            g.SetLives(99);
            var deaths = new Dictionary<string, int>();
            var stages = new List<string>();
            var doing = new Dictionary<string, int>();
            var last = g.Phase;
            int stage = 1, t = 0;
            double start = 0, still = 0;
            (double, double) at = (0, 0);
            var rng = new Random(seed);
            var c = default(Controls);
            for (; t < 60 * 900 && g.StageNumber <= 6; t++)
            {
                if (mode == "lazy")
                {
                    if (t % 30 == 0) c = new Controls(rng.Next(3) - 1, rng.Next(3) - 1, rng.Next(4) == 0);
                }
                else c = g.Pilot.Decide(g);
                g.Tick(c);
                if (g.Phase == Phase.Play) doing[g.Pilot.Doing] = doing.GetValueOrDefault(g.Pilot.Doing) + 1;
                if (g.Phase == Phase.Dying && last != Phase.Dying) { string key = $"S{g.StageNumber} {g.Cause} {g.Pilot.Doing}"; deaths[key] = deaths.GetValueOrDefault(key) + 1; }
                if (g.Phase == Phase.Play && g.Kid.Alive && mode != "lazy")
                {
                    var k = g.Kid;
                    if ((k.X, k.Y) == at) still += 1 / 60.0; else { still = 0; at = (k.X, k.Y); }
                    if (still > 12) { output.WriteLine($"   still S{g.StageNumber} {k.State} on {(k.On?.Index.ToString() ?? "-")} vine {(k.A?.Index.ToString() ?? "-")} at ({k.X - Field.Left:0},{k.Y:0}) doing {g.Pilot.Doing}"); still = -999; }
                }
                if (g.StageNumber != stage) { stages.Add($"S{stage}:{(t - start) / 60:0}s"); stage = g.StageNumber; start = t; }
                last = g.Phase;
            }
            output.WriteLine($"seed {seed}: stage {g.StageNumber} after {t / 60:0}s score {g.Score} | {string.Join(" ", stages)}");
            output.WriteLine("   deaths " + string.Join(", ", deaths.Select(d => $"{d.Key} x{d.Value}")));
            output.WriteLine("   doing " + string.Join(", ", doing.OrderByDescending(d => d.Value).Select(d => $"{d.Key} {d.Value / 60}s")));
        }
    }
}
