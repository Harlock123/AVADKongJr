namespace AVADKongJr.Engine;

/// <summary>
/// The computer Kip, for the demonstration (and the tests). It knows a way up each stage and follows it from
/// wherever it finds itself on the way. It holds a pair of vines where it can, and lets go of one to let a
/// Nipper or a Spark go by on the other. It waits for a sliding platform to come close before stepping on,
/// waits for a vine to be clear before jumping to it, keeps out of the birds' way, and jumps over anything
/// coming at it along a platform. On the Keys it pushes each key up in turn, choosing a quiet chain.
/// </summary>
public sealed class KidPilot
{
    /// <summary>What it is up to, for the probe.</summary>
    public string Doing { get; private set; } = "";

    private int _tick;

    private static double L(double x) => Field.Left + x;

    private Controls Go(string what, int dx = 0, int dy = 0, bool jump = false)
    {
        Doing = what;
        return new Controls(dx, dy, jump);
    }

    /// <summary>A press to one side that lets go between presses, so each one counts on a vine.</summary>
    private Controls Tap(string what, int dx, int dy = 0) => Go(what, _tick % 2 == 0 ? dx : 0, dy);

    public Controls Decide(GameEngine g)
    {
        _tick++;
        var k = g.Kid;
        if (g.Phase != Phase.Play || !k.Alive) return default;
        if (k.State == Hold.Air) return Go("air");
        if (k.State == Hold.Ground && Dodge(g) is { } hop) return hop;
        return g.Kind switch
        {
            StageKind.Jungle => Jungle(g),
            StageKind.Hideout => Hideout(g),
            _ => Keys(g),
        };
    }

    /// <summary>Something coming along the platform at Kip: jump it.</summary>
    private Controls? Dodge(GameEngine g)
    {
        var k = g.Kid;
        // An egg coming down: step out from under it.
        foreach (var egg in g.Eggs)
            if (Math.Abs(egg.X - k.X) < 24 && egg.Y < k.Y)
            {
                int away = egg.X > k.X ? -1 : 1;
                if (!k.On!.Covers(k.X + away * 20)) away = -away;
                return Go("dodge egg", away);
            }
        foreach (var e in g.Enemies)
        {
            if (e.Dead || e.Walk != k.On || e.Walk == null) continue;
            double d = e.X - k.X;
            if (Math.Abs(d) < 30 && Math.Sign(d) == -e.Dir) return Go("hop", 0, 0, true);
        }
        return null;
    }

    /// <summary>
    /// Danger on a vine: something above Kip coming down (or sitting just above), something below coming up,
    /// or something walking along the platform overhead to climb down it while he's near the top.
    /// </summary>
    private static bool Threat(GameEngine g, Vine v, double within = 130)
    {
        var k = g.Kid;
        foreach (var e in g.Enemies)
        {
            if (e.Dead) continue;
            if (e.Walk != null && Math.Abs(e.TargetX - v.X) < 1 && Math.Abs(e.X - v.X) < 80 && k.HandY - v.Top < 90) return true;
            if (e.On != v) continue;
            double gap = k.Y - e.Y;
            if (gap > -30 && gap < within && (e.Dir > 0 || gap < 50)) return true;
            if (gap <= -30 && gap > -70 && e.Dir < 0) return true;
        }
        return false;
    }

    /// <summary>Anything at all (falling ones too) near a point.</summary>
    private static bool Near(GameEngine g, double x, double y, double rx = 30, double ry = 44) =>
        g.Enemies.Any(e => !e.Dead && Math.Abs(e.X - x) < rx && Math.Abs(e.Y - y) < ry);

    /// <summary>A bird flying at Kip's height and heading his way.</summary>
    private static bool BirdNear(GameEngine g, double y, double within = 220)
    {
        var k = g.Kid;
        return g.Enemies.Any(e => !e.Dead && e.Kind == EnemyKind.Squawker && Math.Abs(e.Lane - y) < 44
                                  && Math.Sign(k.X - e.X) == e.Dir && Math.Abs(k.X - e.X) < within)
               || g.Eggs.Any(x => Math.Abs(x.X - k.X) < 20 && x.Y < k.Y);
    }

    /// <summary>Climbing a pair (or one of a pair): up, letting go with the hand whose vine has trouble on it.</summary>
    private Controls ClimbPair(GameEngine g, string what)
    {
        var k = g.Kid;
        var a = k.A!;
        // About to climb out at the top: not into something walking past.
        double top = k.B is { } held ? Math.Max(a.Top, held.Top) : a.Top;
        if (k.HandY - top < 26 && g.Enemies.Any(e => !e.Dead && e.Walk != null && Math.Abs(e.X - k.X) < 70))
            return Go("wait at top");
        if (k.B is { } b)
        {
            bool ta = Threat(g, a), tb = Threat(g, b);
            if (ta && !tb) return Tap("dodge", Math.Sign(b.X - a.X));
            if (tb && !ta) return Tap("dodge", Math.Sign(a.X - b.X));
            if (ta && tb) return Go("retreat", 0, 1);
            return Go(what, 0, -1);
        }
        var partner = g.Stage.Vines.Where(u => u != a && !u.Chain && Math.Abs(u.X - a.X) <= 36 && u.Top <= k.HandY && u.Bottom >= k.HandY)
            .OrderBy(u => Math.Abs(u.X - a.X)).FirstOrDefault();
        bool mine = Threat(g, a);
        if (partner != null)
        {
            // Holding both puts Kip between the vines: not with anything passing there.
            bool theirs = Threat(g, partner, 160) || Near(g, (a.X + partner.X) / 2, k.Y);
            // Back onto both once the other is clear.
            if (mine && !theirs) return Tap("dodge", Math.Sign(partner.X - a.X));
            if (!mine && !theirs) return Tap(what, Math.Sign(partner.X - a.X));
            if (!mine) return Go("wait", 0, 0);
            return Go("retreat", 0, 1);
        }
        return mine ? Go("retreat", 0, 1) : Go(what, 0, -1);
    }

    private Controls WalkTo(GameEngine g, double x, string what = "walk")
    {
        double d = x - g.Kid.X;
        return Math.Abs(d) < 2.5 ? Go(what) : Go(what, Math.Sign(d));
    }

    // ───────────────────────────── the jungle ─────────────────────────────

    private Controls Jungle(GameEngine g)
    {
        var k = g.Kid;
        var s = g.Stage;
        if (k.State == Hold.Ground)
        {
            switch (k.On!.Index)
            {
                case 0:
                {
                    var v = s.Vines[0];
                    if (Math.Abs(k.X - v.X) > 2.5) return WalkTo(g, v.X);
                    return Threat(g, v, 200) ? Go("wait") : Go("reach", 0, 0, true);
                }
                case 5:
                {
                    if (Math.Abs(k.X - L(112)) > 2.5) return WalkTo(g, L(112));
                    // The first vine clear near the bottom (or the Nipper on it heading away up), and the second clear low down.
                    bool busy = g.Enemies.Any(e => !e.Dead && ((e.On == s.Vines[1] && e.Y > 300 && !(e.Dir < 0 && e.Y < 340)) || (e.On == s.Vines[2] && e.Y > 350)
                                                               || (e.Falling && e.X < L(175) && e.Y > 280 && e.Y < 470)));
                    return busy ? Go("wait") : Go("leap", 1, 0, true);
                }
                case 7:
                    return WalkTo(g, s.GoalX, "key");
                default:
                    // Off the way up: head back to the start island.
                    return WalkTo(g, k.On.X0 - 40, "lost");
            }
        }
        var a = k.A!;
        if (a.Index == 0) return Threat(g, a) ? Go("retreat", 0, 1) : Go("climb", 0, -1);
        if (a.Index is 1 or 2)
        {
            var c = ClimbPair(g, "climb");
            // Backed down to the bottom: leap back to the ledge rather than drop into the water.
            if (c.Dy > 0 && k.HandY > a.Bottom - 14) return Go("bail", -1, 0, true);
            return c;
        }
        return Go("lost", 0, 1);
    }

    // ───────────────────────────── the hideout ─────────────────────────────

    private Controls Hideout(GameEngine g)
    {
        var k = g.Kid;
        var s = g.Stage;
        if (k.State == Hold.Ground)
        {
            var p = k.On!;
            switch (p.Index)
            {
                case 0:
                {
                    var m = s.Platforms[1];
                    // Once on the way, keep going.
                    if (k.X > L(86) || (m.X0 <= L(96) && m.Vx > 0 && k.X > L(76))) return Go("board", 1);
                    return WalkTo(g, L(82), "wait");
                }
                case 1:
                {
                    // Ride at the front, and step off as it reaches the middle platform.
                    if (p.X0 >= p.Max - 8 || k.X > p.X1 - 4) return Go("off", 1);
                    return WalkTo(g, p.X1 - 10, "ride");
                }
                case 2:
                {
                    var v = s.Vines[0];
                    if (Math.Abs(k.X - v.X) > 2.5) return WalkTo(g, v.X);
                    return BirdNear(g, k.Y - 60) || Threat(g, v, 200) ? Go("wait") : Go("reach", 0, -1);
                }
                case 3:
                {
                    if (Math.Abs(k.X - L(252)) > 2.5) return WalkTo(g, L(252));
                    bool busy = g.Enemies.Any(e => !e.Dead && (e.On == s.Vines[1] || e.On == s.Vines[6]) && e.Y > 300);
                    return busy || BirdNear(g, k.Y) ? Go("wait") : Go("leap", -1, 0, true);
                }
                case 4:
                    return WalkTo(g, s.GoalX, "key");
                default:
                    return WalkTo(g, p.X0, "lost");
            }
        }
        var a = k.A!;
        if (a.Index == 0)
        {
            // Keep out of the birds' lane.
            if (BirdNear(g, k.Y)) return Go("duck", 0, k.Y > 560 ? 1 : -1);
            return Go("climb", 0, -1);
        }
        if (a.Index is 1 or 6)
        {
            if (BirdNear(g, k.Y) && k.Y > 330) return Go("duck", 0, -1);
            return ClimbPair(g, "climb");
        }
        return Go("lost", 0, 1);
    }

    // ───────────────────────────── the keys ─────────────────────────────

    private Controls Keys(GameEngine g)
    {
        var k = g.Kid;
        var s = g.Stage;
        if (k.State == Hold.Ground)
        {
            if (k.On!.Index != 0) return WalkTo(g, Field.Left + 320, "lost");
            // The nearest key still to lock, on a chain with nothing coming down it.
            Vine? best = null;
            double cost = double.MaxValue;
            foreach (var key in s.Keys.Where(x => !x.Locked))
            {
                var v = key.Chain;
                bool busy = g.Enemies.Any(e => !e.Dead && e.On == v && (e.Y > 380 || e.Dir > 0));
                double c = Math.Abs(v.X - k.X) + (busy ? 400 : 0);
                if (c < cost) { cost = c; best = v; }
            }
            if (best == null) return Go("wait");
            if (Math.Abs(k.X - best.X) > 2.5) return WalkTo(g, best.X);
            bool clear = !g.Enemies.Any(e => !e.Dead && e.On == best && (e.Y > 380 || e.Dir > 0));
            return clear && !BirdNear(g, 560, 160) ? Go("reach", 0, -1) : Go("wait");
        }
        var a = k.A!;
        if (a.Key is { Locked: true }) return Go("down", 0, 1);
        if (Threat(g, a, 150)) return Go("retreat", 0, 1);
        if (BirdNear(g, k.Y)) return Go("duck", 0, k.Y > 480 ? 1 : -1);
        return Go("push", 0, -1);
    }
}
