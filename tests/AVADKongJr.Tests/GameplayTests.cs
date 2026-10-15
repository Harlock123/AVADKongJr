using AVADKongJr.Engine;

namespace AVADKongJr.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avadkongjr-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>A game in play on a stage with nobody released and the bonus standing still.</summary>
    private static GameEngine Game(int stage = 1)
    {
        var g = new GameEngine(7, HighScoreTable.Load(TempFolder())) { Silent = true, Calm = true };
        g.Go(stage);
        return g;
    }

    private static double L(double x) => Field.Left + x;

    private static Controls Press(int dx = 0, int dy = 0, bool jump = false) => new(dx, dy, jump);

    // ───────────────────────────── the stages ─────────────────────────────

    [Fact]
    public void The_stages_go_jungle_hideout_keys_then_round_again_faster()
    {
        var kinds = Enumerable.Range(1, 4).Select(n => Game(n).Kind).ToList();
        Assert.Equal([StageKind.Jungle, StageKind.Hideout, StageKind.Keys, StageKind.Jungle], kinds);
        var later = Game(4);
        Assert.Equal(2, later.Level);
        Assert.True(later.Pace > Game(1).Pace);
        Assert.True(later.StartBonus > Game(1).StartBonus);
    }

    [Fact]
    public void Every_stage_has_vines_for_the_keeper_to_send_things_down_and_vines_end_where_kip_can_land()
    {
        foreach (var kind in Enum.GetValues<StageKind>())
        {
            var s = Stage.Build(kind);
            Assert.NotEmpty(s.HangingFrom(s.Platforms[s.KeeperPlatform]));
            Assert.All(s.Vines, v => Assert.True(v.X > Field.Left && v.X < Field.Right && v.Top < v.Bottom));
            // A vine ending over a platform lets Kip off above it, not through it.
            foreach (var v in s.Vines)
                foreach (var p in s.Platforms.Where(p => !p.Moving && p.Covers(v.X) && p.Y > v.Bottom && p.Y - v.Bottom < 60))
                    Assert.True(v.Bottom + Kid.Hands + Kid.Feet <= p.Y, $"{kind} vine {v.Index} ends too low over platform {p.Index}");
        }
    }

    // ───────────────────────────── Kip ─────────────────────────────

    [Fact]
    public void Kip_walks_along_and_walking_off_the_island_into_the_water_is_the_end()
    {
        var g = Game();
        g.PutKid(0, 40);
        g.Run(30, Press(dx: 1));
        Assert.Equal(L(40) + GameEngine.WalkSpeed * 0.5, g.Kid.X, 1);
        g.Run(60, Press(dx: 1));
        g.Idle(60);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("SPLASH", g.Cause);
    }

    [Fact]
    public void A_jump_goes_up_and_comes_down_where_it_started()
    {
        var g = Game();
        g.PutKid(0, 40);
        double y = g.Kid.Y;
        g.Run(1, Press(jump: true));
        g.Idle(10);
        Assert.Equal(Hold.Air, g.Kid.State);
        Assert.True(g.Kid.Y < y - 20);
        g.Idle(40);
        Assert.Equal(Hold.Ground, g.Kid.State);
        Assert.Equal(y, g.Kid.Y);
        Assert.True(g.Kid.Alive);
    }

    [Fact]
    public void One_hand_climbs_slowly_and_slides_quickly_and_two_hands_the_other_way_about()
    {
        var g = Game();
        g.PutKidOnVine(0, 600);
        g.Run(60, Press(dy: -1));
        Assert.Equal(600 - GameEngine.OneUp, g.Kid.HandY, 1);
        g.Run(15, Press(dy: 1));
        Assert.Equal(600 - GameEngine.OneUp + GameEngine.OneDown / 4, g.Kid.HandY, 1);
        g.PutKidOnVine(1, 380, two: true);
        Assert.True(g.Kid.TwoHands);
        g.Run(60, Press(dy: -1));
        Assert.Equal(380 - GameEngine.TwoUp, g.Kid.HandY, 1);
        g.Run(30, Press(dy: 1));
        Assert.Equal(380 - GameEngine.TwoUp + GameEngine.TwoDown / 2, g.Kid.HandY, 1);
    }

    [Fact]
    public void Pressing_towards_the_vine_beside_takes_hold_of_both_and_pressing_again_lets_go_of_one()
    {
        var g = Game();
        g.PutKidOnVine(1, 300);
        Assert.Equal(L(130), g.Kid.X);
        g.Run(1, Press(dx: 1));
        Assert.True(g.Kid.TwoHands);
        Assert.Equal(L(143), g.Kid.X);
        g.Idle(1);
        g.Run(1, Press(dx: 1));
        Assert.False(g.Kid.TwoHands);
        Assert.Equal(2, g.Kid.A!.Index);
        g.Idle(1);
        g.Run(1, Press(dx: -1));
        Assert.True(g.Kid.TwoHands);
    }

    [Fact]
    public void Kip_climbs_off_the_top_of_a_vine_onto_the_platform_it_hangs_from()
    {
        var g = Game();
        g.PutKidOnVine(1, 220, two: true);
        g.Run(60, Press(dy: -1));
        Assert.Equal(Hold.Ground, g.Kid.State);
        Assert.Equal(7, g.Kid.On!.Index);
    }

    [Fact]
    public void Kip_grabs_a_vine_from_below_by_jumping_and_leaps_from_one_to_another()
    {
        var g = Game();
        g.PutKid(0, 60);
        g.Run(1, Press(jump: true));
        g.Idle(20);
        Assert.Equal(Hold.Vine, g.Kid.State);
        Assert.Equal(0, g.Kid.A!.Index);
        g.PutKid(5, 112);
        g.Run(1, Press(dx: 1, jump: true));
        g.Idle(20);
        Assert.Equal(Hold.Vine, g.Kid.State);
        Assert.Equal(1, g.Kid.A!.Index);
    }

    [Fact]
    public void Sliding_off_a_vine_lands_kip_below_but_a_long_fall_is_the_end()
    {
        var g = Game();
        g.PutKidOnVine(0, 640);
        g.Run(20, Press(dy: 1));
        g.Idle(30);
        Assert.Equal(Hold.Ground, g.Kid.State);
        Assert.Equal(0, g.Kid.On!.Index);
        Assert.True(g.Kid.Alive);
        g = Game(2);
        g.PutKid(5, 410);
        g.Run(20, Press(dx: -1));
        g.Idle(90);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("FELL", g.Cause);
    }

    [Fact]
    public void Kip_rides_a_sliding_platform()
    {
        var g = Game(2);
        var m = g.Stage.Platforms[1];
        g.PutKid(1, 127);
        double offset = g.Kid.X - m.X0;
        g.Idle(60);
        Assert.Equal(Hold.Ground, g.Kid.State);
        Assert.Equal(offset, g.Kid.X - m.X0, 1);
        Assert.NotEqual(L(127), g.Kid.X);
    }

    // ───────────────────────────── fruit, enemies and points ─────────────────────────────

    [Fact]
    public void Fruit_knocked_off_its_vine_crushes_everything_below_it_for_more_and_more()
    {
        var g = Game();
        var a = g.PutEnemy(EnemyKind.RedNipper, 2, 320, 1);
        var b = g.PutEnemy(EnemyKind.RedNipper, 2, 385, -1);
        g.PutKidOnVine(2, 240);
        g.Idle(60);
        Assert.True(a.Dead && b.Dead);
        Assert.Equal(GameEngine.FruitPoints + 2 * GameEngine.FruitPoints, g.Score);
        Assert.True(g.Kid.Alive);
    }

    [Fact]
    public void Jumping_over_a_nipper_on_a_platform_is_worth_100()
    {
        var g = Game();
        g.PutKid(7, 175);
        var e = g.PutWalker(EnemyKind.RedNipper, 7, 120, 3);
        for (int t = 0; t < 120 && e.X < g.Kid.X - 26; t++) g.Idle(1);
        g.Run(1, Press(jump: true));
        g.Idle(50);
        Assert.True(g.Kid.Alive);
        Assert.Equal(GameEngine.JumpOverPoints, g.Score);
    }

    [Fact]
    public void Red_nippers_go_up_and_down_their_vine_and_blue_ones_drop_off_the_bottom()
    {
        var g = Game();
        g.PutKid(0, 20);
        var red = g.PutEnemy(EnemyKind.RedNipper, 6, 600, 1);
        g.Idle(60);
        Assert.Equal(-1, red.Dir);
        var blue = g.PutEnemy(EnemyKind.BlueNipper, 3, 300, 1);
        g.Idle(120);
        Assert.DoesNotContain(blue, g.Enemies);
    }

    [Fact]
    public void The_keeper_sends_nippers_along_his_platform_to_a_vine()
    {
        var g = Game();
        var e = g.ReleaseForTest(EnemyKind.RedNipper)!;
        Assert.NotNull(e.Walk);
        g.Idle(120);
        Assert.NotNull(e.On);
        Assert.Equal(g.Stage.Platforms[g.Stage.KeeperPlatform], g.Stage.Above(e.On!));
    }

    [Fact]
    public void A_nipper_a_spark_a_bird_or_an_egg_ends_kip_and_the_stage_starts_again()
    {
        var g = Game();
        g.SetLives(5);
        g.PutKidOnVine(0, 600);
        g.PutEnemy(EnemyKind.RedNipper, 0, g.Kid.Y, 1);
        g.Idle(2);
        Assert.Equal("NIPPED", g.Cause);
        Assert.Equal(4, g.Lives);
        g.Idle((int)(60 * GameEngine.DyingSeconds) + 5);
        Assert.Equal(Phase.Start, g.Phase);
        Assert.Equal(L(40), g.Kid.X);
        g.Idle((int)(60 * GameEngine.StartSeconds) + 5);
        g.PutBird(g.Kid.X, g.Kid.Y, 1);
        g.Idle(2);
        Assert.Equal("PECKED", g.Cause);
        g.Idle((int)(60 * (GameEngine.DyingSeconds + GameEngine.StartSeconds)) + 10);
        g.Eggs.Add(new Egg { X = g.Kid.X, Y = g.Kid.Y - 40 });
        g.Idle(30);
        Assert.Equal("EGGED", g.Cause);
    }

    [Fact]
    public void Losing_the_last_life_ends_the_game()
    {
        var g = Game();
        g.SetLives(1);
        g.PutKid(0, 40);
        g.Run(120, Press(dx: 1));
        g.Idle((int)(60 * GameEngine.DyingSeconds) + 10);
        Assert.Equal(GameState.GameOver, g.State);
    }

    // ───────────────────────────── the bonus and the goals ─────────────────────────────

    [Fact]
    public void Reaching_the_key_banks_the_bonus_and_moves_on()
    {
        var g = Game();
        g.PutKid(7, 140);
        g.Run(30, Press(dx: -1));
        Assert.Equal(Phase.Clear, g.Phase);
        Assert.Equal(g.StartBonus, g.Score);
        g.Idle((int)(60 * GameEngine.ClearSeconds) + 5);
        Assert.Equal(2, g.StageNumber);
    }

    [Fact]
    public void The_bonus_counts_down_and_at_nothing_kip_is_lost()
    {
        var g = new GameEngine(7, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.Go(1);
        g.SetLives(5);
        int start = g.Bonus;
        g.Idle((int)(60 * GameEngine.BonusEvery) + 2);
        Assert.Equal(start - 100, g.Bonus);
        g.SetBonus(100);
        g.Idle((int)(60 * GameEngine.BonusEvery) + 2);
        Assert.Equal("TIME", g.Cause);
    }

    [Fact]
    public void Keys_are_pushed_up_their_chains_and_slide_back_if_let_go()
    {
        var g = Game(3);
        var key = g.Stage.Keys[0];
        g.PutKid(0, 60);
        g.Run(1, Press(dy: -1));
        Assert.Equal(Hold.Vine, g.Kid.State);
        g.Run(60 * 3, Press(dy: -1));
        double pushed = key.Y;
        Assert.True(pushed < key.StartY - 100);
        g.Run(60 * 3, Press(dy: 1));
        g.Idle(60);
        Assert.True(key.Y > pushed + 20);
        g.PutKid(0, 60);
        g.Run(1, Press(dy: -1));
        g.Run(60 * 8, Press(dy: -1));
        Assert.True(key.Locked);
        Assert.Equal(GameEngine.KeyPoints, g.Score);
    }

    [Fact]
    public void Locking_the_sixth_key_frees_papa_and_starts_the_next_level()
    {
        var g = Game(3);
        foreach (var k in g.Stage.Keys.Skip(1)) { k.Locked = true; k.Y = k.LockY; }
        g.PutKid(0, 60);
        g.Run(1, Press(dy: -1));
        g.Run(60 * 8, Press(dy: -1));
        Assert.Equal(Phase.Clear, g.Phase);
        Assert.Equal("PAPA IS FREE!", g.Message);
        g.Idle((int)(60 * GameEngine.FreeSeconds) + 5);
        Assert.Equal(4, g.StageNumber);
        Assert.Equal(2, g.Level);
        Assert.Equal(StageKind.Jungle, g.Kind);
    }

    [Fact]
    public void An_extra_life_comes_at_10000()
    {
        var g = Game();
        int lives = g.Lives;
        g.SetScore(GameEngine.FirstExtraLife - 100);
        g.PutKid(7, 140);
        g.Run(30, Press(dx: -1));
        Assert.Equal(lives + 1, g.Lives);
    }

    // ───────────────────────────── around the game ─────────────────────────────

    [Fact]
    public void The_computer_kip_clears_the_first_stages()
    {
        var g = new GameEngine(1, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.StartGame();
        g.SetLives(9);
        for (int t = 0; t < 60 * 240 && g.State == GameState.Playing && g.StageNumber < 3; t++) g.Tick(g.Pilot.Decide(g));
        Assert.Equal(GameState.Playing, g.State);
        Assert.True(g.StageNumber >= 3, $"stage {g.StageNumber}");
    }

    [Fact]
    public void The_title_runs_a_demonstration_then_the_high_scores_then_back()
    {
        var g = new GameEngine(5, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 4);
        var (x, y) = (g.Kid.X, g.Kid.Y);
        g.Idle(60 * 10);
        Assert.NotEqual((x, y), (g.Kid.X, g.Kid.Y));
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 14)) + 5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * GameEngine.HighScoreSeconds) + 5);
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        static (int, double, double, int) Play(int seed)
        {
            var g = new GameEngine(seed, HighScoreTable.Load(TempFolder())) { Silent = true };
            g.StartGame();
            for (int t = 0; t < 60 * 40; t++) g.Tick(g.Pilot.Decide(g));
            return (g.Score, g.Kid.X, g.Kid.Y, g.StageNumber);
        }
        Assert.Equal(Play(12), Play(12));
    }

    [Fact]
    public void A_high_score_takes_initials_and_is_kept_in_the_table_file()
    {
        string folder = TempFolder();
        var g = new GameEngine(7, HighScoreTable.Load(folder)) { Silent = true, Calm = true };
        g.Go(3);
        g.SetScore(99999);
        g.SetLives(1);
        g.PutBird(g.Kid.X, g.Kid.Y, 1);
        g.Idle((int)(60 * (GameEngine.DyingSeconds + 5)));
        Assert.Equal(GameState.EnterInitials, g.State);
        foreach (char ch in "KIP") g.Initials!.Type(ch);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        var again = HighScoreTable.Load(folder);
        Assert.Equal(("KIP", 99999, 1), (again.Entries[0].Initials, again.Entries[0].Score, again.Entries[0].Stage));
        Directory.Delete(folder, true);
    }
}
