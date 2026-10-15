using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVADKongJr.Engine;
using Effect = AVADKongJr.Engine.Effect;

namespace AVADKongJr.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVADKongJr/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF4F0E8, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFFA8B0A0, Leaf = 0xFF3AC85A, Fur = 0xFF8A4A1E, Face = 0xFFF0C890;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF0A0E10), new Rect(0, 0, Field.Width, Field.Height));
        bool inGame = g.State is GameState.Playing or GameState.Attract or GameState.Paused or GameState.GameOver;
        if (inGame)
        {
            using (dc.PushClip(new Rect(Field.Left, Field.Top, Field.Wide, Field.Bottom - Field.Top)))
                DrawStage(dc);
            dc.DrawLine(Pen(0xFF3A4A30, 2), new Point(Field.Left, Field.Top), new Point(Field.Left, Field.Bottom));
            dc.DrawLine(Pen(0xFF3A4A30, 2), new Point(Field.Right, Field.Top), new Point(Field.Right, Field.Bottom));
            DrawPanels(dc);
        }
        foreach (var e in g.Effects) DrawEffect(dc, e);
        DrawHud(dc);
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default: DrawMessages(dc); break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(Field.Width / 2, 340), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(Field.Width / 2, 400), 26, White, center: true);
        }
        if (g.State == GameState.GameOver)
        {
            dc.FillRectangle(Brush(0xB0000000), new Rect(Field.Left, 300, Field.Wide, 110));
            Text(dc, "GAME OVER", new Point(Field.Width / 2, 354), 100, Red, center: true);
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(Field.Width / 2, Field.Height - 18), 26, Yellow, center: true);
    }

    // ───────────────────────────── the stage ─────────────────────────────

    private void DrawStage(DrawingContext dc)
    {
        var g = Engine;
        var s = g.Stage;
        DrawBackdrop(dc, s);
        foreach (var v in s.Vines) DrawVine(dc, v, s.Kind);
        foreach (var p in s.Platforms) DrawPlatform(dc, p, s.Kind);
        foreach (var key in s.Keys) DrawKey(dc, key);
        DrawCage(dc, s);
        DrawKeeper(dc, s);
        if (s.Kind != StageKind.Keys) DrawGoalKey(dc, new Point(s.GoalX, s.GoalY));
        foreach (var f in s.Fruits.Where(f => !f.Gone)) DrawFruit(dc, new Point(f.X, f.Y), f.Kind);
        foreach (var e in g.Enemies) DrawEnemy(dc, e);
        foreach (var egg in g.Eggs) dc.DrawEllipse(Brush(0xFFF8F0D8), Pen(0xFF9A8060, 1.2), new Point(egg.X, egg.Y), 5, 7);
        DrawKid(dc, g.Kid, 1);
        if (s.Water) DrawWater(dc, s.DeathY);
    }

    private void DrawBackdrop(DrawingContext dc, Stage s)
    {
        var area = new Rect(Field.Left, Field.Top, Field.Wide, Field.Bottom - Field.Top);
        switch (s.Kind)
        {
            case StageKind.Jungle:
                dc.FillRectangle(Brush(0xFF071A24), area);
                // Hanging leaves along the top, and a moon.
                dc.DrawEllipse(Brush(0x30FFF4C0), null, new Point(Field.Left + 520, 230), 46, 46);
                dc.DrawEllipse(Brush(0xFFF4EDC0), null, new Point(Field.Left + 520, 230), 30, 30);
                for (int i = 0; i < 26; i++)
                {
                    double x = Field.Left + i * 26 + Hash(i, 1, 3) % 10;
                    double h = 16 + Hash(i, 2, 3) % 26;
                    Poly(dc, i % 2 == 0 ? 0xFF14501E : 0xFF1C6A28, null, 0, new Point(x - 16, Field.Top), new Point(x + 16, Field.Top), new Point(x, Field.Top + h));
                }
                break;
            case StageKind.Hideout:
                dc.FillRectangle(Brush(0xFF14101E), area);
                for (int i = 0; i < 9; i++)
                {
                    double x = Field.Left + 30 + i * 72;
                    dc.FillRectangle(Brush(0xFF1E1A2C), new Rect(x, Field.Top, 18, Field.Bottom - Field.Top));
                    dc.DrawLine(Pen(0xFF2A2440, 2), new Point(x + 4, Field.Top), new Point(x + 4, Field.Bottom));
                }
                // The pit glows below.
                for (int i = 0; i < 6; i++)
                    dc.FillRectangle(Brush(Alpha(0xFFFF6A2A, 0.05 + i * 0.03)), new Rect(Field.Left, s.DeathY - 6 + i * 6, Field.Wide, 6));
                dc.FillRectangle(Brush(0xFF2A0A08), new Rect(Field.Left, s.DeathY + 30, Field.Wide, 60));
                break;
            default:
                dc.FillRectangle(Brush(0xFF161414), area);
                for (int r = 0; r < 24; r++)
                    for (int c = 0; c < 12; c++)
                    {
                        double x = Field.Left + c * 56 + (r % 2) * 28, y = Field.Top + r * 30;
                        dc.DrawRectangle(Brush(0xFF201C1A), Pen(0xFF2C2622, 1.5), new Rect(x, y, 54, 28));
                    }
                break;
        }
    }

    private void DrawWater(DrawingContext dc, double y)
    {
        dc.FillRectangle(Brush(0xC0104A8A), new Rect(Field.Left, y, Field.Wide, Field.Bottom - y));
        for (int i = 0; i < 40; i++)
        {
            double x = Field.Left + i * 16 + Math.Sin(_clock * 2 + i) * 4;
            double wy = y + Math.Sin(_clock * 3 + i * 0.7) * 2;
            dc.DrawLine(Pen(0xFF8AD0FF, 2), new Point(x, wy), new Point(x + 9, wy));
        }
    }

    private void DrawVine(DrawingContext dc, Vine v, StageKind kind)
    {
        if (v.Chain)
        {
            for (double y = v.Top; y < v.Bottom; y += 12)
            {
                bool flat = ((int)((y - v.Top) / 12)) % 2 == 0;
                if (flat) dc.DrawEllipse(null, Pen(0xFFB0B8C0, 2.2), new Point(v.X, y + 6), 3.5, 6.5);
                else dc.DrawLine(Pen(0xFF808890, 3), new Point(v.X, y), new Point(v.X, y + 12));
            }
            return;
        }
        if (kind == StageKind.Hideout)
        {
            dc.DrawLine(Pen(0xFF3A3A48, 5), new Point(v.X, v.Top - 10), new Point(v.X, v.Bottom));
            dc.DrawLine(Pen(0xFF9AA0B8, 2), new Point(v.X, v.Top - 10), new Point(v.X, v.Bottom));
            dc.FillRectangle(Brush(0xFF6A6A80), new Rect(v.X - 5, v.Bottom - 2, 10, 6));
            return;
        }
        // A twisting vine with leaves.
        var pts = new List<Point>();
        for (double y = v.Top - 10; y <= v.Bottom; y += 8) pts.Add(new Point(v.X + Math.Sin(y * 0.09 + v.X) * 2.2, y));
        for (int i = 0; i < pts.Count - 1; i++) dc.DrawLine(Pen(0xFF2A8A2A, 4), pts[i], pts[i + 1]);
        for (int i = 0; i < pts.Count - 1; i++) dc.DrawLine(Pen(0xFF5AD05A, 1.4), pts[i], pts[i + 1]);
        for (double y = v.Top + 12; y < v.Bottom; y += 34)
        {
            int side = ((int)(y / 34) + v.Index) % 2 == 0 ? 1 : -1;
            Poly(dc, Leaf, 0xFF1A6A1A, 1, new Point(v.X, y), new Point(v.X + side * 12, y - 6), new Point(v.X + side * 9, y + 3));
        }
    }

    private static void DrawPlatform(DrawingContext dc, Platform p, StageKind kind)
    {
        var r = new Rect(p.X0, p.Y, p.X1 - p.X0, 10);
        switch (kind)
        {
            case StageKind.Jungle:
                dc.FillRectangle(Brush(0xFF6A4020), new Rect(r.X, r.Y + 4, r.Width, 10));
                dc.FillRectangle(Brush(0xFF3AB04A), new Rect(r.X, r.Y, r.Width, 6), 3);
                for (double x = r.X + 4; x < r.Right - 2; x += 8)
                    dc.DrawLine(Pen(0xFF7AE07A, 1.5), new Point(x, r.Y), new Point(x + 2, r.Y - 4));
                break;
            case StageKind.Hideout:
                dc.FillRectangle(Brush(p.Moving ? 0xFFE08A2A : 0xFF9A3A3A), r);
                for (double x = r.X; x < r.Right - 8; x += 16)
                {
                    dc.DrawLine(Pen(0xFF5A1A1A, 1.5), new Point(x, r.Bottom), new Point(x + 8, r.Y));
                    dc.DrawLine(Pen(0xFF5A1A1A, 1.5), new Point(x + 8, r.Y), new Point(x + 16, r.Bottom));
                }
                dc.DrawRectangle(null, Pen(0xFFF0B060, 1.2), r);
                break;
            default:
                dc.FillRectangle(Brush(0xFF6A6058), r);
                for (double x = r.X; x < r.Right; x += 32) dc.DrawLine(Pen(0xFF3A3430, 1.5), new Point(x, r.Y), new Point(x, r.Bottom));
                dc.DrawLine(Pen(0xFFA09080, 1.5), r.TopLeft, r.TopRight);
                break;
        }
    }

    private static void DrawKey(DrawingContext dc, ChainKey key)
    {
        var c = new Point(key.Chain.X, key.Y);
        // The lock it's heading for.
        var lockAt = new Point(key.Chain.X, key.LockY - 6);
        dc.FillRectangle(Brush(key.Locked ? 0xFF4AE06A : 0xFF5A4A20), new Rect(lockAt.X - 11, lockAt.Y - 10, 22, 18), 3);
        dc.DrawEllipse(Brush(0xFF201808), null, new Point(lockAt.X, lockAt.Y - 2), 3, 3);
        if (key.Locked) return;
        dc.DrawEllipse(null, Pen(Yellow, 3), new Point(c.X, c.Y - 5), 6, 6);
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y + 1), new Point(c.X, c.Y + 12));
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y + 9), new Point(c.X + 5, c.Y + 9));
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y + 12), new Point(c.X + 4, c.Y + 12));
    }

    private void DrawGoalKey(DrawingContext dc, Point c)
    {
        double glow = 0.5 + 0.5 * Math.Sin(_clock * 5);
        dc.DrawEllipse(Brush(Alpha(Yellow, 0.15 + 0.15 * glow)), null, c, 18, 18);
        dc.DrawEllipse(null, Pen(Yellow, 3), new Point(c.X, c.Y - 6), 6, 6);
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y), new Point(c.X, c.Y + 14));
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y + 10), new Point(c.X + 6, c.Y + 10));
        dc.DrawLine(Pen(Yellow, 3), new Point(c.X, c.Y + 14), new Point(c.X + 5, c.Y + 14));
    }

    /// <summary>Papa in his cage: a big ape, sad behind the bars, and over the moon once he's free.</summary>
    private void DrawCage(DrawingContext dc, Stage s)
    {
        var g = Engine;
        bool free = g.Phase == Phase.Clear && s.Kind == StageKind.Keys;
        var c = new Point(s.CageX, s.CageY - 34);
        DrawPapa(dc, c, free ? Math.Sin(_clock * 10) : 0, free);
        if (free && g.PhaseTime > 0.8) return;
        double lift = free ? g.PhaseTime * 120 : 0;
        var r = new Rect(c.X - 38, c.Y - 36 - lift, 76, 70);
        for (double x = r.X; x <= r.Right + 0.1; x += 9.5) dc.DrawLine(Pen(0xFF9AA0A8, 3), new Point(x, r.Y), new Point(x, r.Bottom));
        dc.DrawLine(Pen(0xFF6A7078, 5), r.TopLeft, r.TopRight);
        dc.DrawLine(Pen(0xFF6A7078, 5), r.BottomLeft, r.BottomRight);
    }

    private static void DrawPapa(DrawingContext dc, Point c, double beat, bool happy)
    {
        dc.DrawEllipse(Brush(0xFF5A2E10), Pen(0xFF2A1404, 1.5), new Point(c.X, c.Y + 6), 26, 26);
        // Arms: drumming on the chest when he's free.
        foreach (int s in new[] { -1, 1 })
        {
            double up = happy ? 8 * beat * s : 0;
            dc.DrawEllipse(Brush(0xFF5A2E10), Pen(0xFF2A1404, 1.2), new Point(c.X + s * 26, c.Y + 12 - up), 9, 15);
        }
        dc.DrawEllipse(Brush(0xFFC08A5A), null, new Point(c.X, c.Y + 12), 15, 14);
        dc.DrawEllipse(Brush(0xFF5A2E10), Pen(0xFF2A1404, 1.5), new Point(c.X, c.Y - 18), 16, 14);
        dc.DrawEllipse(Brush(0xFFD8A878), null, new Point(c.X, c.Y - 14), 11, 9);
        dc.DrawEllipse(Brush(0xFF000000), null, new Point(c.X - 5, c.Y - 20), 2.2, 2.2);
        dc.DrawEllipse(Brush(0xFF000000), null, new Point(c.X + 5, c.Y - 20), 2.2, 2.2);
        if (happy) dc.DrawLine(Pen(0xFF3A1404, 2), new Point(c.X - 5, c.Y - 11), new Point(c.X + 5, c.Y - 11));
        else Poly(dc, 0, 0xFF3A1404, 1.8, new Point(c.X - 5, c.Y - 8), new Point(c.X, c.Y - 11), new Point(c.X + 5, c.Y - 8));
    }

    /// <summary>The Keeper: pith helmet, khaki coat and a whip he cracks to send the next one out.</summary>
    private void DrawKeeper(DrawingContext dc, Stage s)
    {
        var g = Engine;
        var p = s.Platforms[s.KeeperPlatform];
        var c = new Point(s.KeeperX + 18, p.Y - 26);
        bool running = g.Phase == Phase.Clear;
        if (running) c = new Point(c.X + g.PhaseTime * 140, c.Y);
        dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(c.X - 7, c.Y + 12, 5, 14));
        dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(c.X + 2, c.Y + 12, 5, 14));
        dc.DrawRectangle(Brush(0xFFC0A060), Pen(0xFF6A5020, 1.2), new Rect(c.X - 10, c.Y - 8, 20, 22), 3);
        dc.DrawEllipse(Brush(0xFFF0C8A0), null, new Point(c.X, c.Y - 14), 7, 7);
        Poly(dc, 0xFFE8E0C0, 0xFF8A8060, 1, new Point(c.X - 11, c.Y - 17), new Point(c.X - 6, c.Y - 26), new Point(c.X + 6, c.Y - 26), new Point(c.X + 11, c.Y - 17));
        // The whip.
        double crack = Math.Sin(_clock * 4);
        var hand = new Point(c.X - 12, c.Y);
        var tip = new Point(hand.X - 26 - crack * 6, hand.Y - 10 + crack * 14);
        dc.DrawLine(Pen(0xFF4A2A10, 2.5), hand, new Point(hand.X - 8, hand.Y - 4));
        dc.DrawLine(Pen(0xFF6A4A20, 1.5), new Point(hand.X - 8, hand.Y - 4), tip);
    }

    private static void DrawFruit(DrawingContext dc, Point c, int kind)
    {
        switch (kind % 4)
        {
            case 0:
                Poly(dc, 0xFFFFE04A, 0xFFA08010, 1.2, new Point(c.X - 8, c.Y - 4), new Point(c.X - 2, c.Y + 8), new Point(c.X + 9, c.Y + 6), new Point(c.X + 2, c.Y + 3));
                break;
            case 1:
                dc.DrawEllipse(Brush(0xFFE02A2A), Pen(0xFF7A0A0A, 1.2), c, 8, 8);
                dc.DrawLine(Pen(0xFF5A3A10, 2), new Point(c.X, c.Y - 8), new Point(c.X + 2, c.Y - 12));
                Poly(dc, Leaf, null, 0, new Point(c.X + 1, c.Y - 9), new Point(c.X + 8, c.Y - 12), new Point(c.X + 4, c.Y - 7));
                break;
            case 2:
                foreach (var (dx, dy) in new[] { (-4.0, -4.0), (4.0, -4.0), (0.0, 0.0), (-4.0, 4.0), (4.0, 4.0), (0.0, 8.0) })
                    dc.DrawEllipse(Brush(0xFF9A4AE0), Pen(0xFF4A1A7A, 1), new Point(c.X + dx, c.Y + dy), 3.6, 3.6);
                break;
            default:
                dc.DrawEllipse(Brush(0xFFB0E040), Pen(0xFF5A7A10, 1.2), new Point(c.X, c.Y + 3), 7, 8);
                dc.DrawEllipse(Brush(0xFFB0E040), Pen(0xFF5A7A10, 1.2), new Point(c.X, c.Y - 4), 4.5, 5);
                break;
        }
    }

    // ───────────────────────────── Kip and the enemies ─────────────────────────────

    /// <summary>Kip: a little ape in a blue vest. Standing, walking, climbing by one hand or two, leaping, or knocked out.</summary>
    private void DrawKid(DrawingContext dc, Kid k, double scale)
    {
        var g = Engine;
        var c = new Point(k.X, k.Y);
        if (!k.Alive)
        {
            if (k.State == Hold.Air && k.Y > Field.Bottom) return;
            double spin = g.PhaseTime * 10;
            DrawApe(dc, c, k.Facing, "fall", spin, scale);
            if (k.State != Hold.Air)
                for (int i = 0; i < 3; i++)
                {
                    double a = _clock * 6 + i * 2.1;
                    dc.DrawEllipse(Brush(Yellow), null, new Point(c.X + Math.Cos(a) * 16, c.Y - 22 + Math.Sin(a) * 4), 2.5, 2.5);
                }
            return;
        }
        string pose = k.State switch
        {
            Hold.Ground => k.Moving ? "walk" : "stand",
            Hold.Air => "leap",
            _ => k.TwoHands ? "two" : "one",
        };
        double phase = k.State == Hold.Vine ? k.Climbed * 0.12 : _clock * 12;
        // On one vine, he hangs to the side he's facing.
        if (pose == "one") c = new Point(c.X - k.Facing * 7, c.Y);
        DrawApe(dc, c, k.Facing, pose, phase, scale);
    }

    private static void DrawApe(DrawingContext dc, Point c, int facing, string pose, double phase, double scale)
    {
        double k = scale;
        Point P(double x, double y) => new(c.X + x * k, c.Y + y * k);
        uint fur = Fur, edge = 0xFF3A1A08;
        if (pose == "fall")
        {
            var rot = Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateRotation(phase) * Matrix.CreateTranslation(c.X, c.Y);
            using (dc.PushTransform(rot)) DrawApe(dc, c, facing, "leap", 0, scale);
            return;
        }
        double sw = Math.Sin(phase);
        // Legs.
        switch (pose)
        {
            case "walk":
                dc.DrawLine(Pen(fur, 5 * k), P(-4, 8), P(-5 + sw * 5, 17));
                dc.DrawLine(Pen(fur, 5 * k), P(4, 8), P(5 - sw * 5, 17));
                break;
            case "one" or "two":
                dc.DrawLine(Pen(fur, 5 * k), P(-4, 8), P(-7, 16 + sw * 2));
                dc.DrawLine(Pen(fur, 5 * k), P(4, 8), P(7, 16 - sw * 2));
                break;
            case "leap":
                dc.DrawLine(Pen(fur, 5 * k), P(-4, 8), P(-12, 13));
                dc.DrawLine(Pen(fur, 5 * k), P(4, 8), P(12, 13));
                break;
            default:
                dc.DrawLine(Pen(fur, 5 * k), P(-4, 8), P(-5, 17));
                dc.DrawLine(Pen(fur, 5 * k), P(4, 8), P(5, 17));
                break;
        }
        // Body and vest.
        dc.DrawEllipse(Brush(fur), Pen(edge, 1.2), P(0, 2), 10 * k, 11 * k);
        Poly(dc, 0xFF2A7AE0, 0xFF0A3A8A, 1, P(-9, -4), P(9, -4), P(7, 8), P(-7, 8));
        dc.DrawEllipse(Brush(Face), null, P(0, 4), 4.5 * k, 5 * k);
        // Arms.
        switch (pose)
        {
            case "one":
            {
                // Both hands up the one vine, alternating as he climbs.
                double hx = facing * 7;
                dc.DrawLine(Pen(fur, 4.5 * k), P(-3, -4), P(hx, -15 - sw * 3));
                dc.DrawLine(Pen(fur, 4.5 * k), P(3, -4), P(hx, -12 + sw * 3));
                break;
            }
            case "two":
                dc.DrawLine(Pen(fur, 4.5 * k), P(-6, -4), P(-13, -15 - sw * 3));
                dc.DrawLine(Pen(fur, 4.5 * k), P(6, -4), P(13, -15 + sw * 3));
                break;
            case "leap":
                dc.DrawLine(Pen(fur, 4.5 * k), P(-6, -4), P(-14, -12));
                dc.DrawLine(Pen(fur, 4.5 * k), P(6, -4), P(14, -12));
                break;
            default:
                dc.DrawLine(Pen(fur, 4.5 * k), P(-8, -3), P(-11, 7 - sw * 2));
                dc.DrawLine(Pen(fur, 4.5 * k), P(8, -3), P(11, 7 + sw * 2));
                break;
        }
        // Head: ears, face, eyes looking the way he faces.
        bool back = pose is "one" or "two";
        dc.DrawEllipse(Brush(fur), Pen(edge, 1), P(-9, -13), 4 * k, 4 * k);
        dc.DrawEllipse(Brush(fur), Pen(edge, 1), P(9, -13), 4 * k, 4 * k);
        dc.DrawEllipse(Brush(fur), Pen(edge, 1.2), P(0, -12), 9 * k, 8.5 * k);
        if (back) return;
        dc.DrawEllipse(Brush(Face), null, P(facing * 1.5, -10), 6.5 * k, 5.5 * k);
        dc.DrawEllipse(Brush(0xFF000000), null, P(facing * 1.5 - 2.6, -12), 1.5 * k, 1.8 * k);
        dc.DrawEllipse(Brush(0xFF000000), null, P(facing * 1.5 + 2.6, -12), 1.5 * k, 1.8 * k);
        dc.DrawLine(Pen(0xFF6A2A10, 1.2), P(facing * 1.5 - 2, -7.5), P(facing * 1.5 + 2, -7.5));
        // A tuft on top.
        Poly(dc, fur, null, 0, P(-2, -19), P(1, -24), P(3, -19));
    }

    private void DrawEnemy(DrawingContext dc, Enemy e)
    {
        var c = new Point(e.X, e.Y);
        switch (e.Kind)
        {
            case EnemyKind.RedNipper or EnemyKind.BlueNipper:
            {
                // A clamp of jaws on legs, snapping.
                uint body = e.Kind == EnemyKind.RedNipper ? 0xFFE03A2A : 0xFF3A7AF0, dark = e.Kind == EnemyKind.RedNipper ? 0xFF7A0A0A : 0xFF0A2A7A;
                double snap = Math.Abs(Math.Sin(e.T * 9)) * 6;
                bool onVine = e.On != null;
                if (onVine)
                {
                    // Gripping the vine, jaws facing the way it goes.
                    var dir = e.Dir;
                    Poly(dc, body, dark, 1.5, new Point(c.X - 11, c.Y - dir * snap), new Point(c.X + 11, c.Y - dir * snap), new Point(c.X + 9, c.Y + dir * 10), new Point(c.X - 9, c.Y + dir * 10));
                    Poly(dc, body, dark, 1.5, new Point(c.X - 11, c.Y + dir * snap * 0.3), new Point(c.X + 11, c.Y + dir * snap * 0.3), new Point(c.X + 9, c.Y - dir * 10), new Point(c.X - 9, c.Y - dir * 10));
                    for (int i = -2; i <= 2; i++) Poly(dc, 0xFFFFFFFF, null, 0, new Point(c.X + i * 4 - 2, c.Y - dir * snap * 0.5), new Point(c.X + i * 4 + 2, c.Y - dir * snap * 0.5), new Point(c.X + i * 4, c.Y - dir * (snap * 0.5 - 4)));
                }
                else
                {
                    var dir = e.Dir == 0 ? 1 : e.Dir;
                    Poly(dc, body, dark, 1.5, new Point(c.X - dir * 10, c.Y - 9), new Point(c.X + dir * 10, c.Y - 3 - snap), new Point(c.X + dir * 12, c.Y), new Point(c.X - dir * 10, c.Y));
                    Poly(dc, body, dark, 1.5, new Point(c.X - dir * 10, c.Y), new Point(c.X + dir * 12, c.Y), new Point(c.X + dir * 10, c.Y + 3 + snap * 0.5), new Point(c.X - dir * 10, c.Y + 8));
                }
                dc.DrawEllipse(Brush(0xFFFFFFFF), null, new Point(c.X - 4, c.Y - 4), 3, 3);
                dc.DrawEllipse(Brush(0xFFFFFFFF), null, new Point(c.X + 4, c.Y - 4), 3, 3);
                dc.DrawEllipse(Brush(0xFF000000), null, new Point(c.X - 4, c.Y - 4), 1.4, 1.4);
                dc.DrawEllipse(Brush(0xFF000000), null, new Point(c.X + 4, c.Y - 4), 1.4, 1.4);
                break;
            }
            case EnemyKind.Spark:
            {
                double pulse = 0.5 + 0.5 * Math.Sin(e.T * 20);
                dc.DrawEllipse(Brush(Alpha(0xFFFFF060, 0.25 + 0.2 * pulse)), null, c, 15, 15);
                dc.DrawEllipse(Brush(0xFFFFFFC0), Pen(0xFFFFC020, 2), c, 7, 7);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 + e.T * 8 + Hash(i, (int)(e.T * 12), 4) % 10 * 0.1;
                    var mid = new Point(c.X + Math.Cos(a + 0.3) * 8, c.Y + Math.Sin(a + 0.3) * 8);
                    var end = new Point(c.X + Math.Cos(a) * 14, c.Y + Math.Sin(a) * 14);
                    dc.DrawLine(Pen(0xFFFFF060, 1.6), new Point(c.X + Math.Cos(a) * 6, c.Y + Math.Sin(a) * 6), mid);
                    dc.DrawLine(Pen(0xFFFFF060, 1.6), mid, end);
                }
                break;
            }
            case EnemyKind.Squawker:
            {
                double flap = Math.Sin(e.T * 14) * 9;
                int d = e.Dir;
                Poly(dc, 0xFF9A3AC0, 0xFF4A0A6A, 1, new Point(c.X - 4, c.Y), new Point(c.X - 14, c.Y - 4 - flap), new Point(c.X - 2, c.Y - 6));
                Poly(dc, 0xFF9A3AC0, 0xFF4A0A6A, 1, new Point(c.X + 4, c.Y), new Point(c.X + 14, c.Y - 4 - flap), new Point(c.X + 2, c.Y - 6));
                dc.DrawEllipse(Brush(0xFFC05AE0), Pen(0xFF4A0A6A, 1.2), c, 10, 7);
                dc.DrawEllipse(Brush(0xFFC05AE0), Pen(0xFF4A0A6A, 1.2), new Point(c.X + d * 9, c.Y - 4), 5, 5);
                Poly(dc, 0xFFFFB020, null, 0, new Point(c.X + d * 13, c.Y - 5), new Point(c.X + d * 20, c.Y - 3), new Point(c.X + d * 13, c.Y - 1));
                dc.DrawEllipse(Brush(0xFFFFFFFF), null, new Point(c.X + d * 10, c.Y - 5), 1.8, 1.8);
                Poly(dc, 0xFF6A1A8A, null, 0, new Point(c.X - d * 8, c.Y), new Point(c.X - d * 17, c.Y - 3), new Point(c.X - d * 16, c.Y + 4));
                break;
            }
        }
    }

    private void DrawEffect(DrawingContext dc, Effect e)
    {
        double t = e.Age / e.Life;
        var c = new Point(e.X, e.Y);
        switch (e.Kind)
        {
            case EffectKind.Burst:
            {
                double r = (8 + 30 * t) * e.Size;
                dc.DrawEllipse(null, Pen(Alpha(e.Colour, 1 - t), 3), c, r, r);
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4, d = r * 0.8;
                    dc.FillRectangle(Brush(Alpha(0xFFFFF0C0, 1 - t)), new Rect(c.X + Math.Cos(a) * d - 2, c.Y + Math.Sin(a) * d - 2, 4, 4));
                }
                break;
            }
            case EffectKind.Splash:
                for (int k = 0; k < 9; k++)
                {
                    double a = -Math.PI * (0.15 + 0.7 * k / 8.0), sp = 60 * e.Size;
                    double x = c.X + Math.Cos(a) * sp * t * 1.2, y = c.Y + Math.Sin(a) * sp * t * 1.6 + 120 * t * t;
                    dc.DrawEllipse(Brush(Alpha(e.Colour, 1 - t)), null, new Point(x, y), 3, 3);
                }
                dc.DrawEllipse(null, Pen(Alpha(0xFFFFFFFF, 1 - t), 2), c, 10 + 30 * t, 3 + 6 * t);
                break;
            case EffectKind.Score:
                Text(dc, e.Text, new Point(c.X, c.Y - 14 - t * 24), 26, e.Colour, center: true);
                break;
            case EffectKind.Word:
                Text(dc, e.Text, new Point(c.X, c.Y - t * 30), 36, e.Colour, center: true);
                break;
        }
    }

    // ───────────────────────────── the score bar, panels and screens ─────────────────────────────

    private void DrawHud(DrawingContext dc)
    {
        var g = Engine;
        Text(dc, "SCORE", new Point(16, 18), 22, Dim);
        Text(dc, g.Score.ToString("D6"), new Point(16, 48), 36, Yellow);
        Text(dc, "HI", new Point(Field.Width - 16, 18), 22, Dim, right: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D6"), new Point(Field.Width - 16, 48), 30, Dim, right: true);
        if (g.State is GameState.Playing or GameState.Attract or GameState.Paused)
            Text(dc, Stage.NameOf(g.Kind), new Point(Field.Width / 2, 36), 38, White, center: true);
    }

    /// <summary>The lives and the bonus on the left; the level and the three stages on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = Field.Left / 2, rx = Field.Right + (Field.Width - Field.Right) / 2;
        Text(dc, "BONUS", new Point(lx, 120), 30, Dim, center: true);
        var box = new Rect(lx - 66, 142, 132, 50);
        bool low = g.Bonus <= 1000 && g.Phase == Phase.Play;
        dc.DrawRectangle(Brush(0xFF101808), Pen(low && (int)(_clock * 4) % 2 == 0 ? Red : 0xFF4AA0E0, 3), box);
        Text(dc, g.Bonus.ToString(), new Point(lx, 167), 40, low ? Red : Yellow, center: true);
        Text(dc, "LIVES", new Point(lx, 250), 30, Dim, center: true);
        int spare = Math.Max(0, g.Lives - (g.Kid.Alive ? 1 : 0));
        for (int i = 0; i < Math.Min(spare, 8); i++)
            DrawApe(dc, new Point(lx - 45 + i % 3 * 45, 296 + i / 3 * 52), 1, "stand", 0, 0.9);

        Text(dc, $"LEVEL {g.Level}", new Point(rx, 120), 34, White, center: true);
        for (int i = 0; i < GameEngine.StagesPerLevel; i++)
        {
            var kind = (StageKind)i;
            bool now = kind == g.Kind;
            double y = 178 + i * 46;
            if (now) dc.FillRectangle(Brush(0x40FFE14A), new Rect(rx - 86, y - 20, 172, 40), 4);
            Text(dc, Stage.NameOf(kind), new Point(rx, y), 26, now ? Yellow : Dim, center: true);
        }
        if (g.Kind == StageKind.Keys)
        {
            int locked = g.Stage.Keys.Count(k => k.Locked);
            Text(dc, "KEYS", new Point(rx, 360), 30, Dim, center: true);
            for (int i = 0; i < g.Stage.Keys.Count; i++)
            {
                var at = new Point(rx - 62 + i * 25, 396);
                dc.DrawEllipse(null, Pen(i < locked ? Yellow : 0xFF4A4A40, 3), new Point(at.X, at.Y - 6), 5, 5);
                dc.DrawLine(Pen(i < locked ? Yellow : 0xFF4A4A40, 3), new Point(at.X, at.Y - 1), new Point(at.X, at.Y + 10));
            }
        }
    }

    private void DrawMessages(DrawingContext dc)
    {
        var g = Engine;
        if (g.Message is not { } msg) return;
        double cx = Field.Width / 2, cy = 330;
        var (w, h) = (Math.Min(Field.Wide, Math.Max(msg.Length * 30, (g.SubMessage?.Length ?? 0) * 15) + 60), g.SubMessage != null ? 110.0 : 74.0);
        dc.FillRectangle(Brush(0xC0081008), new Rect(cx - w / 2, cy - 48, w, h), 6);
        Text(dc, msg, new Point(cx, cy - 12), 56, g.Phase == Phase.Dying ? Red : Yellow, center: true);
        if (g.SubMessage is { } sub) Text(dc, sub, new Point(cx, cy + 36), 28, White, center: true);
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        Text(dc, "AVA DKONG JR", new Point(cx, 100), 104, 0xFFFFB040, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", new Point(cx, 166), 28, Dim, center: true);
        // Papa in his cage, the Keeper, and Kip climbing to the rescue.
        var cage = new Point(cx - 120, 250);
        DrawPapa(dc, cage, 0, false);
        for (double x = cage.X - 38; x <= cage.X + 38.1; x += 9.5) dc.DrawLine(Pen(0xFF9AA0A8, 3), new Point(x, cage.Y - 36), new Point(x, cage.Y + 34));
        DrawApe(dc, new Point(cx + 30, 262), -1, "one", _clock * 6, 1.6);
        dc.DrawLine(Pen(0xFF2A8A2A, 4), new Point(cx + 30 - 11, 200), new Point(cx + 30 - 11, 320));
        DrawEnemy(dc, new Enemy { Kind = EnemyKind.RedNipper, X = cx + 150, Y = 250, T = _clock, Dir = 1, On = new Vine() });
        DrawEnemy(dc, new Enemy { Kind = EnemyKind.Spark, X = cx + 210, Y = 250, T = _clock });
        DrawEnemy(dc, new Enemy { Kind = EnemyKind.Squawker, X = cx + 270, Y = 250, T = _clock, Dir = -1 });
        string[] lines =
        [
            "THE KEEPER HAS CAGED PAPA - CLIMB UP AND FREE HIM",
            "TWO VINES CLIMB FASTER - ONE VINE SLIDES DOWN FASTER",
            "DROP FRUIT ON THE NIPPERS - PUSH THE KEYS UP INTO THE LOCKS",
        ];
        for (int i = 0; i < lines.Length; i++) Text(dc, lines[i], new Point(cx, 360 + i * 30), 25, White, center: true);
        (string What, string Points)[] table = [("JUMP ONE", "100"), ("FRUIT DROP", "400 800 1200"), ("A KEY", "200"), ("THE BONUS", "WHAT'S LEFT")];
        for (int i = 0; i < table.Length; i++)
        {
            double x = cx - 300 + i * 200;
            Text(dc, table[i].What, new Point(x, 478), 24, Dim, center: true);
            Text(dc, table[i].Points, new Point(x, 504), 26, Yellow, center: true);
        }
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER OR START", new Point(cx, 590), 46, Yellow, center: true);
        Text(dc, "ARROWS: WALK AND CLIMB    SPACE: JUMP", new Point(cx, 660), 26, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "THE TOP CLIMBERS", new Point(cx, 130), 64, 0xFFFFB040, center: true);
        Text(dc, "RANK NAME    SCORE   LEVEL", new Point(cx, 196), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,4} ", new Point(cx, 240 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 700), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}
