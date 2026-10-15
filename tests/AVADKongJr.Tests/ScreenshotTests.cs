using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVADKongJr.Engine;
using AVADKongJr.Views;

namespace AVADKongJr.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set KONGJR_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avadkongjr-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("KONGJR_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVADKongJr-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer Kip play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 150)
    {
        for (int t = 0; t < 60 * maxSeconds; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("APE", 84250, 4);
        g.HighScores.Insert("AVA", 52800, 3);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void The_jungle()
    {
        var (w, c, g) = Open(2);
        g.StartGame();
        g.SetLives(9);
        g.Idle(60);
        c.Advance(0.5);
        Shot(w, c, "jungle-start");
        Assert.True(PlayUntil(g, c, g => g.Kind == StageKind.Jungle && g.Kid is { State: Hold.Vine } && g.Kid.Y < 340 && g.Enemies.Count(e => e.On != null) >= 2, 200));
        Shot(w, c, "jungle");
        Assert.True(PlayUntil(g, c, g => g.Kind == StageKind.Jungle && g.Phase == Phase.Clear && g.PhaseTime > 0.5, 200));
        Shot(w, c, "the-key");
    }

    [AvaloniaFact]
    public void Fruit_falling()
    {
        var (w, c, g) = Open();
        g.Go(1);
        g.SetLives(9);
        g.PutEnemy(EnemyKind.RedNipper, 2, 330, 1);
        g.PutEnemy(EnemyKind.BlueNipper, 2, 385, -1);
        g.PutKidOnVine(1, 250, two: true);
        for (int t = 0; t < 22; t++) { g.Tick(default); c.Advance(1 / 60.0); }
        Shot(w, c, "fruit");
    }

    [AvaloniaFact]
    public void The_hideout()
    {
        var (w, c, g) = Open(3);
        g.Go(2);
        g.SetLives(9);
        Assert.True(PlayUntil(g, c, g => g.Kid is { State: Hold.Ground, On.Index: 1 } && g.Enemies.Any(e => e.Kind == EnemyKind.Spark && e.On != null) && g.Enemies.Any(e => e.Kind == EnemyKind.Squawker && e.X > Field.Left + 60 && e.X < Field.Right - 60), 200));
        Shot(w, c, "hideout");
        Assert.True(PlayUntil(g, c, g => g.Kid is { State: Hold.Vine } && g.Kid.A!.Index is 1 or 6 && g.Kid.Y < 300, 200));
        Shot(w, c, "wires");
    }

    [AvaloniaFact]
    public void The_keys()
    {
        var (w, c, g) = Open(4);
        g.Go(3);
        g.SetLives(9);
        Assert.True(PlayUntil(g, c, g => g.Stage.Keys.Count(k => k.Locked) >= 2 && g.Kid.State == Hold.Vine && g.Kid.A!.Key is { Locked: false } key && key.Y < 400 && g.Enemies.Any(e => e.Kind == EnemyKind.Squawker && e.X > Field.Left + 40 && e.X < Field.Right - 40), 300));
        Shot(w, c, "keys");
        foreach (var k in g.Stage.Keys.Where(k => !k.Locked).Skip(1)) { k.Locked = true; k.Y = k.LockY; }
        Assert.True(PlayUntil(g, c, g => g.Phase == Phase.Clear && g.PhaseTime > 1.4, 200));
        Shot(w, c, "papa-free");
    }

    [AvaloniaFact]
    public void Kip_lost()
    {
        var (w, c, g) = Open();
        g.Go(1);
        g.SetLives(3);
        g.PutKid(0, 70);
        for (int t = 0; t < 120 && g.Phase == Phase.Play; t++) g.Tick(new AVADKongJr.Engine.Controls(Dx: 1));
        for (int t = 0; t < 14; t++) { g.Tick(default); c.Advance(1 / 60.0); }
        Shot(w, c, "splash");
    }
}
