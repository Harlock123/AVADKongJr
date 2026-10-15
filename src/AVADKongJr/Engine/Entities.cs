namespace AVADKongJr.Engine;

/// <summary>The stick (left, right, up and down) and the jump button.</summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Fire = false);

/// <summary>
/// The screen, in pixels: each stage fills the column between <see cref="Left"/> and <see cref="Right"/>.
/// Stage layouts are given across that column from 0 to <see cref="Wide"/>.
/// </summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    public const double Left = 192, Right = 832, Top = 70, Bottom = 768;
    public const double Wide = Right - Left;
    public const double Gravity = 900;
}

/// <summary>Something to climb: a vine in the jungle, a wire in the hideout, a chain with a key on it.</summary>
public sealed class Vine
{
    public double X, Top, Bottom;
    public bool Chain;
    public int Index;
    /// <summary>Its key, if it's a chain.</summary>
    public ChainKey? Key;
}

/// <summary>Something to stand on. A moving one slides back and forth between <see cref="Min"/> and <see cref="Max"/> (its left end).</summary>
public sealed class Platform
{
    public double X0, X1, Y;
    public double Vx, Min, Max;
    public int Index;
    public bool Moving => Vx != 0;
    public bool Covers(double x, double slack = 0) => x >= X0 - slack && x <= X1 + slack;
}

/// <summary>Fruit hanging on a vine: touch it and it drops, crushing whatever it meets on the way down.</summary>
public sealed class Fruit
{
    public double X, Y, Vy;
    public int Kind;
    public bool Falling, Gone;
    public int Kills;
}

/// <summary>A key on a chain: push it up to its lock from below. Left alone, it slides back down.</summary>
public sealed class ChainKey
{
    public double Y, StartY, LockY;
    public bool Locked;
    public Vine Chain = null!;
}

public enum Hold
{
    Ground,
    /// <summary>Hanging on a vine by one hand, or two (holding a pair).</summary>
    Vine,
    Air,
}

/// <summary>Kip, the young ape. His position is the middle of his body: hands <see cref="Hands"/> above it, feet <see cref="Feet"/> below.</summary>
public sealed class Kid
{
    public const double HalfWidth = 12, Feet = 17, Hands = 14;

    public double X, Y, Vx, Vy;
    public Hold State = Hold.Ground;
    public Platform? On;
    public Vine? A, B;
    public int Facing = 1;
    public bool Alive = true;
    /// <summary>The highest point of the jump or fall he's in (falling too far from it is the end of him).</summary>
    public double PeakY;
    public Vine? LeftVine;
    public double LeftAt;
    /// <summary>For drawing: how far he's climbed (the arms swap as it grows), and whether he's moving.</summary>
    public double Climbed;
    public bool Moving;
    public bool TwoHands => State == Hold.Vine && B != null;
    public double HandY => Y - Hands;
    public double FeetY => Y + Feet;
}

public enum EnemyKind
{
    /// <summary>Red Nippers patrol up and down the vines, and walk the platforms between them.</summary>
    RedNipper,
    /// <summary>Blue Nippers crawl down a vine and drop off the bottom.</summary>
    BlueNipper,
    /// <summary>Sparks run along the hideout's wires, as the red Nippers do.</summary>
    Spark,
    /// <summary>Birds flying across; in the hideout they drop eggs.</summary>
    Squawker,
}

public sealed class Enemy
{
    public EnemyKind Kind;
    public double X, Y, Vx, Vy;
    public Vine? On;
    public Platform? Walk;
    /// <summary>Up (-1) or down (1) a vine; left (-1) or right (1) along a platform or through the air.</summary>
    public int Dir = 1;
    public double TargetX, T, Lane;
    public bool Dead, Falling, Dropped;
    /// <summary>Whether Kip has already had points for jumping over it this jump.</summary>
    public bool Cleared;
}

/// <summary>An egg dropped by a Squawker.</summary>
public sealed class Egg
{
    public double X, Y, Vy;
    public bool Dead;
}

public enum EffectKind { Score, Burst, Word, Splash, Sparks }

public sealed class Effect
{
    public EffectKind Kind;
    public double X, Y, Age, Life = 0.7, Size = 1;
    public string Text = "";
    public uint Colour = 0xFFFFFFFF;
    public bool Done => Age >= Life;
}
