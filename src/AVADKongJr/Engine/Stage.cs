namespace AVADKongJr.Engine;

public enum StageKind
{
    /// <summary>Vines over the water, and islands to start from; fruit to drop on the Nippers.</summary>
    Jungle,
    /// <summary>Wires with Sparks running on them, platforms sliding over a pit, and birds dropping eggs.</summary>
    Hideout,
    /// <summary>Six chains, each with a key to push up into a lock on Papa's cage.</summary>
    Keys,
}

/// <summary>
/// The layout of a stage: its platforms, vines (or wires, or chains), fruit and keys, where Kip starts, where
/// the key he's after hangs, and where the Keeper stands. Built fresh each time the stage starts.
/// </summary>
public sealed class Stage
{
    public StageKind Kind;
    public readonly List<Platform> Platforms = [];
    public readonly List<Vine> Vines = [];
    public readonly List<Fruit> Fruits = [];
    public readonly List<ChainKey> Keys = [];
    /// <summary>Fall below this and it's over: the water in the jungle, the pit in the hideout.</summary>
    public double DeathY = Field.Bottom + 40;
    public bool Water;
    public double StartX;
    public int StartPlatform;
    /// <summary>The key Kip is climbing for (on the Jungle and the Hideout).</summary>
    public double GoalX, GoalY;
    public double KeeperX;
    public int KeeperPlatform;
    public double CageX, CageY;
    /// <summary>The heights the birds fly across at.</summary>
    public double[] Lanes = [];

    private static double L(double x) => Field.Left + x;

    private Platform Plat(double x0, double x1, double y, double vx = 0, double min = 0, double max = 0)
    {
        var p = new Platform { X0 = L(x0), X1 = L(x1), Y = y, Vx = vx, Min = L(min), Max = L(max), Index = Platforms.Count };
        Platforms.Add(p);
        return p;
    }

    private Vine Line(double x, double top, double bottom, bool chain = false)
    {
        var v = new Vine { X = L(x), Top = top, Bottom = bottom, Chain = chain, Index = Vines.Count };
        Vines.Add(v);
        return v;
    }

    private void Hang(int vine, double y, int kind) => Fruits.Add(new Fruit { X = Vines[vine].X, Y = y, Kind = kind });

    public static string NameOf(StageKind k) => k switch
    {
        StageKind.Jungle => "THE JUNGLE", StageKind.Hideout => "THE HIDEOUT", _ => "THE KEYS",
    };

    public static Stage Build(StageKind kind)
    {
        var s = new Stage { Kind = kind };
        switch (kind)
        {
            case StageKind.Jungle:
                s.Water = true;
                s.DeathY = 726;
                s.Plat(0, 110, 700);        // 0: the island Kip starts on
                s.Plat(160, 240, 706);      // 1
                s.Plat(300, 380, 694);      // 2
                s.Plat(440, 520, 706);      // 3
                s.Plat(570, 640, 696);      // 4
                s.Plat(0, 120, 430);        // 5: a ledge on the left
                s.Plat(470, 640, 440);      // 6: a ledge on the right
                s.Plat(0, 230, 160);        // 7: the top left, with the cage, the key and the Keeper
                s.Plat(400, 640, 160);      // 8: the top right
                s.Line(60, 440, 655);       // 0: from the left ledge down
                s.Line(130, 170, 400);      // 1, 2: a pair from the top left
                s.Line(156, 170, 400);
                s.Line(212, 170, 330);      // 3
                s.Line(316, 230, 640);      // 4, 5: a pair over the middle island
                s.Line(342, 230, 640);
                s.Line(470, 170, 640);      // 6: over the fourth island
                s.Line(500, 170, 405);      // 7, 8: a pair from the top right, down to the right ledge
                s.Line(526, 170, 405);
                s.Line(600, 450, 650);      // 9: from the right ledge down
                s.Hang(2, 260, 0);
                s.Hang(5, 360, 1);
                s.Hang(6, 300, 2);
                s.Hang(8, 290, 3);
                s.StartX = L(40);
                s.StartPlatform = 0;
                (s.GoalX, s.GoalY) = (L(100), 136);
                (s.KeeperX, s.KeeperPlatform) = (L(205), 7);
                (s.CageX, s.CageY) = (L(42), 160);
                s.Lanes = [300, 560];
                break;
            case StageKind.Hideout:
                s.DeathY = 745;
                s.Plat(0, 90, 700);                         // 0: start
                s.Plat(92, 162, 700, 45, 92, 274);          // 1: slides between the start and the middle
                s.Plat(350, 430, 700);                      // 2: the middle
                s.Plat(240, 420, 450);                      // 3: a ledge in the middle
                s.Plat(0, 200, 160);                        // 4: the top left, with the cage, the key and the Keeper
                s.Plat(400, 640, 160);                      // 5: the top right
                s.Plat(520, 640, 400);                      // 6: a ledge on the right
                s.Plat(450, 520, 600, 40, 450, 570);        // 7: slides on the right
                s.Line(400, 460, 660);                      // 0: from the middle ledge down
                s.Line(190, 170, 420);                      // 1: from the top left
                s.Line(120, 170, 360);                      // 2
                s.Line(470, 170, 560);                      // 3: from the top right
                s.Line(560, 170, 365);                      // 4: down to the right ledge
                s.Line(600, 410, 590);                      // 5: from the right ledge down
                s.Line(214, 170, 420);                      // 6: beside wire 1, so the two can be held together
                s.Hang(1, 250, 1);
                s.Hang(3, 330, 0);
                s.Hang(0, 560, 2);
                s.StartX = L(40);
                s.StartPlatform = 0;
                (s.GoalX, s.GoalY) = (L(100), 136);
                (s.KeeperX, s.KeeperPlatform) = (L(170), 4);
                (s.CageX, s.CageY) = (L(42), 160);
                s.Lanes = [300, 540];
                break;
            default:
                s.Plat(0, 640, 700);        // 0: the floor
                s.Plat(0, 640, 160);        // 1: the top, with the cage in the middle
                foreach (double x in new[] { 60.0, 150, 240, 400, 490, 580 })
                {
                    var c = s.Line(x, 170, 660, chain: true);
                    var k = new ChainKey { Y = 560, StartY = 560, LockY = 205, Chain = c };
                    c.Key = k;
                    s.Keys.Add(k);
                }
                s.Hang(1, 330, 1);
                s.Hang(4, 330, 2);
                s.StartX = L(320);
                s.StartPlatform = 0;
                (s.KeeperX, s.KeeperPlatform) = (L(440), 1);
                (s.CageX, s.CageY) = (L(320), 160);
                s.Lanes = [300, 430, 560];
                break;
        }
        return s;
    }

    /// <summary>The platform a vine hangs from (its top meets it), if any.</summary>
    public Platform? Above(Vine v) => Platforms.FirstOrDefault(p => !p.Moving && v.Top >= p.Y && v.Top - p.Y <= 20 && p.Covers(v.X, 4));

    /// <summary>The vines hanging from a platform.</summary>
    public IEnumerable<Vine> HangingFrom(Platform p) => Vines.Where(v => Above(v) == p);
}
