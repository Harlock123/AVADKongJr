namespace AVADKongJr.Engine;

public sealed partial class GameEngine
{
    /// <summary>A level is the three stages in turn: the Jungle, the Hideout, the Keys.</summary>
    public const int StagesPerLevel = 3;
    public const double WalkSpeed = 100, JumpSpeed = 260, JumpAcross = 110, LeapAcross = 140, LeapUp = 110;
    /// <summary>Climbing: one hand is slow up and quick down; two hands (on a pair of vines) the other way about.</summary>
    public const double OneUp = 55, OneDown = 170, TwoUp = 110, TwoDown = 90, ChainUp = 75;
    /// <summary>Fall further than this and Kip doesn't get up.</summary>
    public const double SafeDrop = 75;
    public const double StartSeconds = 2.4, DyingSeconds = 2.2, ClearSeconds = 3.0, FreeSeconds = 4.5;
    public const double BonusEvery = 2.0, KeySlide = 25;
    public const int JumpOverPoints = 100, FruitPoints = 400, KeyPoints = 200;
    public const int FirstExtraLife = 10000, ExtraLifeEvery = 40000;

    /// <summary>Which stage of the game this is, counting from 1 (three to a level).</summary>
    public int StageNumber { get; private set; } = 1;
    public int Level => (StageNumber - 1) / StagesPerLevel + 1;
    public StageKind Kind => (StageKind)((StageNumber - 1) % StagesPerLevel);
    public int Score { get; private set; }
    public int Lives { get; private set; }
    /// <summary>What finishing the stage is worth: it counts down as you go, and at nothing Kip is lost.</summary>
    public int Bonus { get; private set; }
    public Stage Stage { get; private set; } = Stage.Build(StageKind.Jungle);
    public Kid Kid { get; } = new();
    public List<Enemy> Enemies { get; } = [];
    public List<Egg> Eggs { get; } = [];
    public KidPilot Pilot { get; } = new();
    public string Cause { get; private set; } = "";
    public double StageTime { get; private set; }
    /// <summary>For tests: nothing spawns and the bonus doesn't count down.</summary>
    internal bool Calm { get; set; }

    private int _nextExtra, _dxPrev;
    private double _spawnIn, _birdIn, _bonusIn, _climbTick;
    private bool _firePrev;
    private bool[] _locked = [];

    public double Pace => Math.Min(1.75, 1 + 0.15 * (Level - 1));
    public int StartBonus => Math.Min(8000, 5000 + 1000 * (Level - 1));

    private void NewGame()
    {
        _rng = new Random(_seed);
        Score = 0;
        Lives = 3;
        StageNumber = 1;
        _nextExtra = FirstExtraLife;
        _locked = [];
        LayOut();
        SetPhase(Phase.Start);
    }

    private void BeginStage(int number)
    {
        StageNumber = number;
        _locked = [];
        LayOut();
        SetPhase(Phase.Start);
        Say($"STAGE {number}", Stage.NameOf(Kind), StartSeconds);
        Play(Sfx.StageStart);
    }

    /// <summary>The stage afresh (any keys already locked stay locked), with Kip at the start.</summary>
    private void LayOut()
    {
        Stage = Stage.Build(Kind);
        for (int i = 0; i < _locked.Length && i < Stage.Keys.Count; i++)
            if (_locked[i]) { Stage.Keys[i].Locked = true; Stage.Keys[i].Y = Stage.Keys[i].LockY; }
        Enemies.Clear();
        Eggs.Clear();
        Effects.Clear();
        StageTime = 0;
        Bonus = StartBonus;
        _bonusIn = BonusEvery;
        _spawnIn = 1.5;
        _birdIn = 4;
        var k = Kid;
        var p = Stage.Platforms[Stage.StartPlatform];
        Stand(p, Stage.StartX);
        k.Alive = true;
        k.Facing = 1;
        k.Vx = k.Vy = 0;
    }

    private void Stand(Platform p, double x)
    {
        var k = Kid;
        k.State = Hold.Ground;
        k.On = p;
        k.A = k.B = null;
        k.X = x;
        k.Y = p.Y - Kid.Feet;
        k.Vx = k.Vy = 0;
        foreach (var e in Enemies) e.Cleared = false;
    }

    // ───────────────────────────── a step of play ─────────────────────────────

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Start:
                _firePrev = c.Fire;
                MovePlatforms(dt);
                if (PhaseTime > StartSeconds)
                {
                    SetPhase(Phase.Play);
                    if (State == GameState.Playing) Music(true);
                }
                break;
            case Phase.Play:
                StageTime += dt;
                MovePlatforms(dt);
                MoveKid(c, dt);
                if (Phase != Phase.Play) break;
                MoveFruit(dt);
                MoveKeys(dt);
                MoveEnemies(dt);
                MoveEggs(dt);
                Spawn(dt);
                Hits();
                if (Phase == Phase.Play) Countdown(dt);
                break;
            case Phase.Dying:
                MovePlatforms(dt);
                MoveFruit(dt);
                if (!Kid.Alive && Kid.State == Hold.Air)
                {
                    // Falling still, if he fell.
                    Kid.Vy = Math.Min(500, Kid.Vy + Field.Gravity * dt);
                    Kid.Y = Math.Min(Field.Bottom + 40, Kid.Y + Kid.Vy * dt);
                }
                if (PhaseTime > DyingSeconds)
                {
                    if (Lives <= 0) { GameOver(); return; }
                    _locked = Stage.Keys.Select(k => k.Locked).ToArray();
                    LayOut();
                    SetPhase(Phase.Start);
                    Say("TRY AGAIN", Stage.NameOf(Kind), StartSeconds);
                }
                break;
            case Phase.Clear:
                if (PhaseTime > (Kind == StageKind.Keys ? FreeSeconds : ClearSeconds)) BeginStage(StageNumber + 1);
                break;
        }
    }

    private void MovePlatforms(double dt)
    {
        foreach (var p in Stage.Platforms.Where(p => p.Moving))
        {
            double w = p.X1 - p.X0;
            p.X0 += p.Vx * dt;
            if (p.X0 < p.Min) { p.X0 = p.Min; p.Vx = Math.Abs(p.Vx); }
            if (p.X0 > p.Max) { p.X0 = p.Max; p.Vx = -Math.Abs(p.Vx); }
            p.X1 = p.X0 + w;
            if (Kid.State == Hold.Ground && Kid.On == p) Kid.X += p.Vx * dt;
        }
    }

    // ───────────────────────────── Kip ─────────────────────────────

    /// <summary>The vine beside <paramref name="v"/> on the given side, near enough to hold with the other hand.</summary>
    private Vine? Partner(Vine v, int side)
    {
        var k = Kid;
        return Stage.Vines.Where(u => u != v && !u.Chain && Math.Sign(u.X - v.X) == side && Math.Abs(u.X - v.X) <= 36 && u.Top <= k.HandY && u.Bottom >= k.HandY)
            .OrderBy(u => Math.Abs(u.X - v.X)).FirstOrDefault();
    }

    private void MoveKid(Controls c, double dt)
    {
        var k = Kid;
        bool jump = c.Fire && !_firePrev;
        _firePrev = c.Fire;
        bool sidePressed = c.Dx != 0 && c.Dx != _dxPrev;
        _dxPrev = c.Dx;
        k.Moving = false;
        switch (k.State)
        {
            case Hold.Ground:
            {
                var p = k.On!;
                if (c.Dx != 0) k.Facing = c.Dx;
                if (jump)
                {
                    Leave(c.Dx * JumpAcross, -JumpSpeed);
                    Play(Sfx.Jump);
                    break;
                }
                if (c.Dy < 0 && Reachable() is { } v)
                {
                    Grab(v);
                    break;
                }
                k.X = Math.Clamp(k.X + c.Dx * WalkSpeed * dt, Field.Left + Kid.HalfWidth, Field.Right - Kid.HalfWidth);
                k.Moving = c.Dx != 0;
                if (!p.Covers(k.X, 6)) Leave(c.Dx * WalkSpeed, 0);
                break;
            }
            case Hold.Vine:
                Climb(c, dt, jump, sidePressed);
                break;
            case Hold.Air:
                Fly(dt);
                break;
        }
        if (k.Alive && Math.Abs(k.X - Stage.GoalX) < 16 && Math.Abs(k.Y - Stage.GoalY) < 30 && Kind != StageKind.Keys) Reached();
    }

    /// <summary>A vine within reach overhead (from the ground).</summary>
    private Vine? Reachable()
    {
        var k = Kid;
        return Stage.Vines.Where(v => Math.Abs(v.X - k.X) < 16 && k.HandY >= v.Top - 4 && k.HandY <= v.Bottom + 12 && !KeyBlocks(v, k.HandY))
            .OrderBy(v => Math.Abs(v.X - k.X)).FirstOrDefault();
    }

    private static bool KeyBlocks(Vine v, double handY) => v.Key is { } key && handY < key.Y + 12;

    private void Grab(Vine v)
    {
        var k = Kid;
        k.State = Hold.Vine;
        k.A = v;
        k.B = null;
        k.On = null;
        k.X = v.X;
        k.Vx = k.Vy = 0;
        foreach (var e in Enemies) e.Cleared = false;
        Play(Sfx.Grab);
    }

    private void Leave(double vx, double vy)
    {
        var k = Kid;
        k.LeftVine = k.State == Hold.Vine ? k.A : null;
        var other = k.State == Hold.Vine ? k.B : null;
        k.LeftAt = StageTime;
        k.State = Hold.Air;
        k.On = null;
        k.A = other;
        k.B = null;
        k.Vx = vx;
        k.Vy = vy;
        k.PeakY = k.Y;
    }

    private void Climb(Controls c, double dt, bool jump, bool sidePressed)
    {
        var k = Kid;
        var a = k.A!;
        if (jump)
        {
            int dir = c.Dx != 0 ? c.Dx : k.Facing;
            k.Facing = dir;
            Leave(dir * LeapAcross, -LeapUp);
            // Both vines it was holding are left behind.
            k.LeftVine ??= a;
            Play(Sfx.Jump);
            return;
        }
        if (sidePressed)
        {
            k.Facing = c.Dx;
            if (k.B is { } b)
            {
                // Let go with one hand: keep the vine on the side pressed.
                k.A = c.Dx < 0 ? (a.X < b.X ? a : b) : (a.X > b.X ? a : b);
                k.B = null;
            }
            else if (Partner(a, c.Dx) is { } p) k.B = p;
            k.X = k.B is { } bb ? (k.A!.X + bb.X) / 2 : k.A!.X;
            a = k.A!;
        }
        bool two = k.B != null;
        double top = two ? Math.Max(a.Top, k.B!.Top) : a.Top;
        double bottom = two ? Math.Min(a.Bottom, k.B!.Bottom) : a.Bottom;
        if (c.Dy < 0)
        {
            double up = (a.Chain ? ChainUp : two ? TwoUp : OneUp) * dt;
            double hand = k.HandY - up;
            if (a.Key is { Locked: false } key && hand < key.Y + 12)
            {
                // Pushing the key up its chain.
                key.Y = hand - 12;
                if (key.Y <= key.LockY) LockKey(key);
                hand = Math.Max(hand, key.Y + 12);
            }
            else if (a.Key is { Locked: true } done) hand = Math.Max(hand, done.LockY + 12);
            if (hand <= top)
            {
                if (Stage.Platforms.FirstOrDefault(p => !p.Moving && top >= p.Y && top - p.Y <= 20 && p.Covers(k.X, 4)) is { } plat && a.Key is not { Locked: false })
                {
                    Stand(plat, k.X);
                    return;
                }
                hand = top;
            }
            k.Moving = hand < k.HandY - 0.01;
            k.Climbed += k.HandY - hand;
            k.Y = hand + Kid.Hands;
        }
        else if (c.Dy > 0)
        {
            double down = (two ? TwoDown : OneDown) * dt;
            k.Y += down;
            k.Climbed += down * 0.5;
            k.Moving = true;
            if (k.HandY > bottom)
            {
                if (two)
                {
                    // The shorter vine runs out: hang on by the longer.
                    k.A = a.Bottom > k.B!.Bottom ? a : k.B;
                    k.B = null;
                    k.X = k.A!.X;
                    if (k.HandY > k.A.Bottom) Leave(0, 0);
                }
                else Leave(0, 0);
            }
        }
        if (k.Moving)
        {
            _climbTick -= dt;
            if (_climbTick <= 0)
            {
                bool pushing = c.Dy < 0 && a.Key is { Locked: false } kk && k.HandY - (kk.Y + 12) < 2;
                Play(pushing ? Sfx.KeyPush : Sfx.Climb);
                _climbTick = 0.18;
            }
        }
    }

    private void Fly(double dt)
    {
        var k = Kid;
        double prevFeet = k.FeetY;
        k.Vy = Math.Min(520, k.Vy + Field.Gravity * dt);
        k.X += k.Vx * dt;
        k.Y += k.Vy * dt;
        k.PeakY = Math.Min(k.PeakY, k.Y);
        if (k.X < Field.Left + Kid.HalfWidth || k.X > Field.Right - Kid.HalfWidth)
        {
            k.X = Math.Clamp(k.X, Field.Left + Kid.HalfWidth, Field.Right - Kid.HalfWidth);
            k.Vx = 0;
        }
        // Points for anything jumped over.
        foreach (var e in Enemies)
        {
            if (e.Dead || e.Cleared || e.Kind == EnemyKind.Squawker) continue;
            if (Math.Abs(e.X - k.X) < 14 && e.Y > k.Y + 12 && e.Y - k.Y < 70)
            {
                e.Cleared = true;
                AddScore(JumpOverPoints, k.X, k.Y - 20);
                Play(Sfx.JumpOver);
            }
        }
        // Catch a vine on the way past.
        foreach (var v in Stage.Vines)
        {
            if ((v == k.LeftVine || v == k.A) && StageTime - k.LeftAt < 0.35) continue;
            if (Math.Abs(k.X - v.X) < 9 && k.HandY >= v.Top && k.HandY <= v.Bottom && !KeyBlocks(v, k.HandY))
            {
                Grab(v);
                return;
            }
        }
        // Land on a platform.
        if (k.Vy > 0)
            foreach (var p in Stage.Platforms)
            {
                if (prevFeet > p.Y + 4 || k.FeetY < p.Y || !p.Covers(k.X, 4)) continue;
                double drop = p.Y - Kid.Feet - k.PeakY;
                Stand(p, k.X);
                if (drop > SafeDrop) Die("FELL");
                return;
            }
        if (k.FeetY > Stage.DeathY)
        {
            if (Stage.Water)
            {
                Effects.Add(new Effect { Kind = EffectKind.Splash, X = k.X, Y = Stage.DeathY, Life = 0.9, Colour = 0xFF6AC8FF });
                Play(Sfx.Splash);
                Die("SPLASH");
            }
            else
            {
                Play(Sfx.Fall);
                Die("FELL");
            }
            k.State = Hold.Air;
        }
    }

    private void Reached()
    {
        SetPhase(Phase.Clear);
        Music(false);
        AddScore(Bonus);
        Say("THE KEY!", $"BONUS {Bonus}", ClearSeconds);
        Play(Sfx.Clear);
    }

    // ───────────────────────────── fruit and keys ─────────────────────────────

    private void MoveFruit(double dt)
    {
        var k = Kid;
        foreach (var f in Stage.Fruits)
        {
            if (f.Gone) continue;
            if (!f.Falling)
            {
                if (k.Alive && Math.Abs(f.X - k.X) < 16 && Math.Abs(f.Y - k.Y) < 24)
                {
                    f.Falling = true;
                    Play(Sfx.FruitDrop);
                }
                continue;
            }
            f.Vy = Math.Min(450, f.Vy + 700 * dt);
            f.Y += f.Vy * dt;
            foreach (var e in Enemies)
            {
                if (e.Dead || Math.Abs(e.X - f.X) > 20 || Math.Abs(e.Y - f.Y) > 20) continue;
                e.Dead = true;
                f.Kills++;
                int points = FruitPoints * f.Kills;
                AddScore(points, e.X, e.Y);
                Effects.Add(new Effect { Kind = EffectKind.Burst, X = e.X, Y = e.Y, Colour = 0xFFFFE14A, Size = 0.8 });
                Play(Sfx.FruitHit);
            }
            if (f.Y > Field.Bottom + 20) f.Gone = true;
        }
    }

    private void MoveKeys(double dt)
    {
        var k = Kid;
        foreach (var key in Stage.Keys)
        {
            if (key.Locked || key.Y >= key.StartY) continue;
            bool held = k.State == Hold.Vine && k.A == key.Chain && k.HandY - (key.Y + 12) < 2;
            if (held) continue;
            double floor = k.State == Hold.Vine && k.A == key.Chain ? Math.Min(key.StartY, k.HandY - 12) : key.StartY;
            key.Y = Math.Min(floor, key.Y + KeySlide * dt);
        }
    }

    private void LockKey(ChainKey key)
    {
        key.Y = key.LockY;
        key.Locked = true;
        AddScore(KeyPoints, key.Chain.X, key.LockY);
        Play(Sfx.KeyLock);
        if (Stage.Keys.All(x => x.Locked))
        {
            SetPhase(Phase.Clear);
            Music(false);
            AddScore(Bonus);
            Say("PAPA IS FREE!", $"BONUS {Bonus}", FreeSeconds);
            Play(Sfx.PapaFree);
        }
    }

    // ───────────────────────────── the enemies ─────────────────────────────

    private int MaxEnemies => Kind switch { StageKind.Jungle => 2 + Level, StageKind.Hideout => 2 + Level, _ => 1 + Level };

    private void Spawn(double dt)
    {
        if (Calm) return;
        _spawnIn -= dt;
        if (_spawnIn <= 0)
        {
            _spawnIn = (Kind == StageKind.Keys ? 5.0 : 3.8) / Pace * (0.7 + 0.6 * _rng.NextDouble());
            if (Enemies.Count(e => !e.Dead && e.Kind != EnemyKind.Squawker) < MaxEnemies)
            {
                var kind = Kind switch
                {
                    StageKind.Jungle => _rng.NextDouble() < 0.45 ? EnemyKind.BlueNipper : EnemyKind.RedNipper,
                    StageKind.Hideout => EnemyKind.Spark,
                    _ => EnemyKind.RedNipper,
                };
                Release(kind);
            }
        }
        if (Kind != StageKind.Jungle || Level > 1)
        {
            _birdIn -= dt;
            if (_birdIn <= 0)
            {
                _birdIn = (Kind == StageKind.Keys ? 5.0 : 7.0) / Pace * (0.7 + 0.6 * _rng.NextDouble());
                int dir = _rng.Next(2) == 0 ? 1 : -1;
                double lane = Stage.Lanes[_rng.Next(Stage.Lanes.Length)];
                Enemies.Add(new Enemy { Kind = EnemyKind.Squawker, Dir = dir, X = dir > 0 ? Field.Left - 30 : Field.Right + 30, Y = lane, Lane = lane });
                Play(Sfx.Squawk);
            }
        }
    }

    /// <summary>The Keeper cracks his whip and lets one go along his platform, towards a vine hanging from it.</summary>
    private Enemy? Release(EnemyKind kind)
    {
        var p = Stage.Platforms[Stage.KeeperPlatform];
        var vines = Stage.HangingFrom(p).ToList();
        if (vines.Count == 0) return null;
        var v = vines[_rng.Next(vines.Count)];
        var e = new Enemy { Kind = kind, X = Stage.KeeperX, Y = p.Y - 10, Walk = p, TargetX = v.X };
        Enemies.Add(e);
        Play(kind == EnemyKind.Spark ? Sfx.Zap : Sfx.Whip);
        return e;
    }

    private double CrawlSpeed(Enemy e) => (e.Kind == EnemyKind.Spark ? 70 : e.Kind == EnemyKind.BlueNipper ? 75 : 55) * Pace;

    private void MoveEnemies(double dt)
    {
        foreach (var e in Enemies)
        {
            if (e.Dead) continue;
            e.T += dt;
            if (e.Kind == EnemyKind.Squawker)
            {
                e.X += e.Dir * 95 * Pace * dt;
                e.Y = e.Lane + Math.Sin(e.T * 3) * 14;
                if (e.X < Field.Left - 50 || e.X > Field.Right + 50) e.Dead = true;
                if (Kind == StageKind.Hideout && !e.Dropped && Kid.Alive && Math.Abs(e.X - Kid.X) < 10 && Kid.Y > e.Y)
                {
                    // Only about half of them have an egg to drop.
                    e.Dropped = true;
                    if (_rng.NextDouble() < 0.5)
                    {
                        Eggs.Add(new Egg { X = e.X, Y = e.Y + 10 });
                        Play(Sfx.Egg);
                    }
                }
                continue;
            }
            double speed = CrawlSpeed(e);
            if (e.Falling)
            {
                e.Vy = Math.Min(480, e.Vy + Field.Gravity * dt);
                e.Y += e.Vy * dt;
                if (e.Y > Stage.DeathY)
                {
                    e.Dead = true;
                    if (Stage.Water) Effects.Add(new Effect { Kind = EffectKind.Splash, X = e.X, Y = Stage.DeathY, Life = 0.6, Size = 0.6, Colour = 0xFF6AC8FF });
                }
                continue;
            }
            if (e.Walk is { } p)
            {
                // Quicker along a platform than on a vine.
                speed = 95 * Pace;
                double d = e.TargetX - e.X;
                e.Dir = Math.Sign(d);
                if (Math.Abs(d) <= speed * dt)
                {
                    e.X = e.TargetX;
                    var v = Stage.Vines.First(v => Math.Abs(v.X - e.TargetX) < 1 && Stage.Above(v) == p);
                    e.On = v;
                    e.Walk = null;
                    e.Dir = 1;
                    e.Y = v.Top + 8;
                }
                else e.X += e.Dir * speed * dt;
                continue;
            }
            if (e.On is not { } vine) { e.Dead = true; continue; }
            e.Y += e.Dir * speed * (e.Dir < 0 ? 0.8 : 1) * dt;
            if (e.Dir > 0 && e.Y >= vine.Bottom - 8)
            {
                if (e.Kind == EnemyKind.BlueNipper)
                {
                    e.On = null;
                    e.Falling = true;
                    e.Vy = 0;
                }
                else
                {
                    e.Y = vine.Bottom - 8;
                    e.Dir = -1;
                }
            }
            else if (e.Dir < 0 && e.Y <= vine.Top + 8)
            {
                e.Y = vine.Top + 8;
                e.Dir = 1;
                // Now and then, off along the platform to another vine.
                if (Stage.Above(vine) is { } top && _rng.NextDouble() < 0.5)
                {
                    var others = Stage.HangingFrom(top).Where(v => v != vine).ToList();
                    if (others.Count > 0)
                    {
                        e.On = null;
                        e.Walk = top;
                        e.Y = top.Y - 10;
                        e.TargetX = others[_rng.Next(others.Count)].X;
                    }
                }
            }
        }
        Enemies.RemoveAll(e => e.Dead);
    }

    private void MoveEggs(double dt)
    {
        foreach (var g in Eggs)
        {
            g.Vy = Math.Min(400, g.Vy + Field.Gravity * 0.6 * dt);
            g.Y += g.Vy * dt;
            if (g.Y > Field.Bottom) g.Dead = true;
        }
        Eggs.RemoveAll(g => g.Dead);
    }

    private void Hits()
    {
        var k = Kid;
        if (!k.Alive) return;
        foreach (var e in Enemies)
        {
            if (e.Dead) continue;
            bool hit = e.Kind == EnemyKind.Squawker
                ? Math.Abs(e.X - k.X) < 20 && Math.Abs(e.Y - k.Y) < 18
                : Math.Abs(e.X - k.X) < 16 && Math.Abs(e.Y - k.Y) < 22;
            if (hit) { Die(e.Kind == EnemyKind.Spark ? "ZAPPED" : e.Kind == EnemyKind.Squawker ? "PECKED" : "NIPPED"); return; }
        }
        foreach (var g in Eggs)
            if (Math.Abs(g.X - k.X) < 13 && Math.Abs(g.Y - k.Y) < 18) { Die("EGGED"); return; }
    }

    private void Countdown(double dt)
    {
        if (Calm) return;
        _bonusIn -= dt;
        if (_bonusIn > 0) return;
        _bonusIn += BonusEvery;
        Bonus = Math.Max(0, Bonus - 100);
        if (Bonus == 0) Die("TIME");
    }

    private void Die(string cause)
    {
        if (!Kid.Alive) return;
        Cause = cause;
        Kid.Alive = false;
        Lives--;
        SetPhase(Phase.Dying);
        Music(false);
        if (cause is not ("SPLASH" or "FELL")) Play(Sfx.Death);
        else if (cause == "FELL" && Kid.State != Hold.Air) Play(Sfx.Death);
    }

    private void AddScore(int points, double? x = null, double? y = null)
    {
        Score += points;
        if (x is { } px && y is { } py)
            Effects.Add(new Effect { Kind = EffectKind.Score, X = px, Y = py, Text = points.ToString(), Colour = points >= 800 ? 0xFFFFE14A : 0xFFFFFFFF, Life = 0.9 });
        while (Score >= _nextExtra)
        {
            _nextExtra += ExtraLifeEvery;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    // ───────────────────────────── test hooks ─────────────────────────────

    internal void Run(int ticks, Controls c)
    {
        for (int i = 0; i < ticks; i++) Tick(c);
    }

    internal void Idle(int ticks) => Run(ticks, default);

    /// <summary>Straight into the play of a stage.</summary>
    internal void Go(int stage = 1)
    {
        StartGame();
        BeginStage(stage);
        SetPhase(Phase.Play);
        Message = SubMessage = null;
    }

    internal void PutKid(int platform, double localX) => Stand(Stage.Platforms[platform], Field.Left + localX);

    internal void PutKidOnVine(int vine, double handY, bool two = false)
    {
        var v = Stage.Vines[vine];
        Kid.Alive = true;
        Grab(v);
        Kid.Y = handY + Kid.Hands;
        if (two && Partner(v, 1) is { } p) { Kid.B = p; Kid.X = (v.X + p.X) / 2; }
    }

    internal Enemy PutEnemy(EnemyKind kind, int vine, double y, int dir = 1)
    {
        var v = Stage.Vines[vine];
        var e = new Enemy { Kind = kind, X = v.X, Y = y, On = v, Dir = dir };
        Enemies.Add(e);
        return e;
    }

    internal Enemy PutWalker(EnemyKind kind, int platform, double localX, int vineTarget)
    {
        var p = Stage.Platforms[platform];
        var e = new Enemy { Kind = kind, X = Field.Left + localX, Y = p.Y - 10, Walk = p, TargetX = Stage.Vines[vineTarget].X };
        Enemies.Add(e);
        return e;
    }

    internal Enemy PutBird(double x, double y, int dir)
    {
        var e = new Enemy { Kind = EnemyKind.Squawker, X = x, Y = y, Lane = y, Dir = dir };
        Enemies.Add(e);
        return e;
    }

    internal Enemy? ReleaseForTest(EnemyKind kind) => Release(kind);
    internal void SetScore(int s) => Score = s;
    internal void SetLives(int n) => Lives = n;
    internal void SetBonus(int b) => Bonus = b;
}
