namespace CrucibleSux.Core;

/// <summary>A place in the world: a territory, a world position and, when the step targets one, the object there.</summary>
/// <param name="DataId">The ENpc, EObj or BNpcBase id to target; 0 for a bare position.</param>
/// <param name="Radius">How close counts as arrived for a bare position (the quest's own event range when it has one).</param>
public sealed record Spot(uint Territory, float X, float Y, float Z, uint DataId = 0, float Radius = 3);

public enum StepKind
{
    /// <summary>Talk to the NPC at <see cref="QuestStep.At"/>, answering with <see cref="QuestStep.Choice"/> when asked.</summary>
    Talk,

    /// <summary>Interact with the event object at <see cref="QuestStep.At"/> (the "destination" markers).</summary>
    Interact,

    /// <summary>Use the quest's event item <see cref="QuestStep.Arg"/> on the NPC at <see cref="QuestStep.At"/>.</summary>
    UseEventItem,

    /// <summary>Perform emote <see cref="QuestStep.Arg"/> at the NPC at <see cref="QuestStep.At"/>.</summary>
    Emote,

    /// <summary>Fight a striking dummy near <see cref="QuestStep.At"/> with the rotation until the sequence moves on.</summary>
    FightDummy,

    /// <summary>Summon beast <see cref="QuestStep.Arg"/>, then use its Tempered Release on the dummy near <see cref="QuestStep.At"/>.</summary>
    TemperedRelease,

    /// <summary>Summon a beast of kin <see cref="QuestStep.Kin"/>, Borrow, then use action <see cref="QuestStep.Arg"/> on the dummy.</summary>
    Borrow,

    /// <summary>Walk into the event range at <see cref="QuestStep.At"/> and win the fight that starts there.</summary>
    Battle,

    /// <summary>Walk (never mount) to <see cref="QuestStep.At"/> with the escorted NPC, then talk to it (DataId <see cref="QuestStep.Arg"/>).</summary>
    Escort,
}

/// <summary>What must hold before a step can be carried out; the planner fills the gap (levelling, captures, boards) first.</summary>
/// <param name="Level">Beastmaster level.</param>
/// <param name="Beasts">Unique beasts owned, the Cu Sith included.</param>
/// <param name="Owned">Beasts (XBMPet rows) that must be owned.</param>
/// <param name="OwnedKin">For each kin listed, at least one beast of it must be owned.</param>
/// <param name="Summoned">A beast that must be out when the step runs.</param>
/// <param name="Board">A board that must have been cleared.</param>
public sealed record StepNeed(
    int Level = 0,
    int Beasts = 0,
    IReadOnlyList<uint>? Owned = null,
    IReadOnlyList<Kin>? OwnedKin = null,
    uint Summoned = 0,
    int Board = 0);

/// <summary>One thing to do while the quest stands at <paramref name="Sequence"/>; a sequence may list several in order.</summary>
public sealed record QuestStep(
    byte Sequence,
    StepKind Kind,
    Spot At,
    uint Arg = 0,
    Kin Kin = Kin.Unknown,
    string? Choice = null,
    StepNeed? Need = null);

/// <summary>A job quest as a script: who gives it, what it needs before it can be accepted, and its steps by sequence.</summary>
/// <param name="AnyJob">True when the quest is taken on another job (the unlock quest); otherwise the player must be a Beastmaster.</param>
/// <param name="PreviousQuest">A quest outside this chain that must be complete first, 0 when none.</param>
public sealed record QuestScript(
    uint Id,
    string Name,
    int Level,
    Spot Giver,
    IReadOnlyList<QuestStep> Steps,
    StepNeed? Accept = null,
    bool AnyJob = false,
    uint PreviousQuest = 0)
{
    public IEnumerable<QuestStep> At(byte sequence) => this.Steps.Where(s => s.Sequence == sequence);
}

/// <summary>
/// The thirteen Beastmaster job quests, from the game's Quest, Level and quest text sheets (game 2026.09.15; see
/// docs/data/bst-progression.md). A quest's sequence is what <c>QuestManager</c> reports while it is accepted; 255 is the
/// turn-in. Positions are world coordinates in the Central Shroud (148) unless the spot says otherwise.
/// </summary>
public static class QuestScripts
{
    public const uint CentralShroud = 148;
    public const uint NewGridania = 132;

    /// <summary>The striking dummies the quests name (ENEMY0); they stand near the Bannock.</summary>
    public const uint StrikingDummy = 19456;

    public const uint RawMeat = 2003830;
    public const uint PetEmote = 105;
    public const uint CuSithGourd = 49805;

    /// <summary>"The Ultimate Weapon", the quest before the unlock.</summary>
    public const uint UnlockPrerequisite = 70058;

    public const uint QuellingWave = 44900;
    public const uint SoulCrush = 44902;

    private static Spot At(float x, float y, float z, uint id = 0, float r = 3) => new(CentralShroud, x, y, z, id, r);

    private static readonly Spot Dummies = At(107.6f, -9.4f, -75.6f, StrikingDummy, 40);
    private static readonly Spot TiaFirst = At(-302.8f, 63.5f, -257.2f, 1059767);
    private static readonly Spot TiaBannock = At(119.9f, -7.0f, -87.5f, 1059323);
    private static readonly Spot TiaCamp = At(72.3f, -6.5f, -117.3f, 1059325);
    private static readonly Spot TiaBentbranch = At(23.8f, -6.0f, 64.8f, 1059340);
    private static readonly Spot LaudaBentbranch = At(25.5f, -6.0f, 67.5f, 1059316);
    private static readonly Spot LaudaPath = At(25.5f, -6.0f, 67.5f, 1059759);
    private static readonly Spot Sylmond = At(19.5f, -6.0f, 59.9f, 1059407);

    public static readonly IReadOnlyList<QuestScript> All =
    [
        new(71026, "Strangers in the Wood", 50, new(NewGridania, 28.5f, -8.2f, 122.7f, 1059317),
        [
            new(1, StepKind.Interact, At(-319.5f, 60.9f, -128.1f, 2015394)),
            new(2, StepKind.Talk, At(-302.8f, 63.5f, -257.2f, 1059318)),
            new(3, StepKind.UseEventItem, At(-305.1f, 63.5f, -258.1f, 1059320), RawMeat),
            new(4, StepKind.Emote, At(-305.1f, 63.5f, -258.1f, 1059320), PetEmote),
            new(255, StepKind.Talk, TiaFirst),
        ], AnyJob: true, PreviousQuest: UnlockPrerequisite),

        new(71027, "The Wilds Call", 1, TiaFirst,
        [
            new(1, StepKind.Talk, At(121.8f, -7.0f, -86.4f, 1059322)),
            new(2, StepKind.Talk, TiaBannock),
            new(3, StepKind.Talk, TiaBannock, Need: new(Summoned: Beast.CuSith)),
            new(255, StepKind.Talk, TiaBannock, Need: new(Beasts: 3)),
        ]),

        new(71028, "Hearts Aligned", 8, TiaBannock,
        [
            new(1, StepKind.FightDummy, Dummies),
            new(2, StepKind.Talk, TiaBannock),
            new(3, StepKind.Talk, TiaCamp, Need: new(Level: 14)),
            new(4, StepKind.Talk, At(-38.9f, -32.3f, 326.1f, 1059327)),
            new(5, StepKind.Talk, At(-38.9f, -32.3f, 326.1f, 1059327), Need: new(Owned: [Beast.Geshunpest], Summoned: Beast.Geshunpest)),
            new(6, StepKind.Talk, TiaBannock, Need: new(Level: 16)),
            new(7, StepKind.FightDummy, Dummies),
            new(255, StepKind.Talk, TiaBannock),
        ]),

        new(71029, "Master of the Lhu", 18, TiaBannock,
        [
            new(1, StepKind.TemperedRelease, Dummies, Beast.OpoOpo, Need: new(Owned: [Beast.OpoOpo, Beast.FlyingTrap])),
            new(1, StepKind.TemperedRelease, Dummies, Beast.FlyingTrap, Need: new(Owned: [Beast.OpoOpo, Beast.FlyingTrap])),
            new(2, StepKind.Talk, TiaBannock),
            new(3, StepKind.Talk, TiaBannock, Need: new(Level: 22)),
            new(4, StepKind.Borrow, Dummies, QuellingWave, Kin.Wavekin, Need: new(OwnedKin: [Kin.Wavekin, Kin.Soulkin])),
            new(4, StepKind.Borrow, Dummies, SoulCrush, Kin.Soulkin, Need: new(OwnedKin: [Kin.Wavekin, Kin.Soulkin])),
            new(5, StepKind.Talk, TiaBannock),
            new(6, StepKind.Battle, At(-163.5f, 1.0f, -19.7f, r: 3.8f)),
            new(7, StepKind.Talk, At(-161.1f, 0.9f, -29.1f, 1059329)),
            new(8, StepKind.Talk, At(-160.9f, 0.9f, -29.4f, 1059333)),
            new(9, StepKind.Talk, TiaCamp),
            new(255, StepKind.Talk, TiaCamp, Need: new(Summoned: Beast.CuSith)),
        ]),

        new(71030, "Into the Crucible", 30, TiaCamp,
        [
            new(1, StepKind.Talk, TiaCamp, Need: new(Beasts: 15)),
            new(2, StepKind.Talk, At(89.3f, -6.4f, -122.0f, 1001280)),
            new(2, StepKind.Talk, At(135.2f, -7.0f, -67.0f, 1000411)),
            new(2, StepKind.Talk, At(96.7f, -1.5f, -73.8f, 1000409)),
            new(3, StepKind.Talk, At(84.3f, -2.1f, -50.6f, 1059336), Choice: "A woman clad in lavender was escorted to Bentbranch Meadows."),
            new(255, StepKind.Talk, At(25.4f, -6.0f, 67.4f, 1059339)),
        ]),

        new(71031, "Wit as Weaponry", 30, TiaBentbranch,
        [
            new(1, StepKind.Talk, TiaCamp),
            new(2, StepKind.Talk, At(178.8f, -8.9f, -25.8f, 1059343), Choice: "Speak with Loosefrox."),
            new(3, StepKind.Escort, At(277.0f, -8.8f, 12.7f, r: 8), 1059343),
            new(4, StepKind.Escort, At(324.3f, -7.1f, -45.4f, r: 8), 1059343),
            new(5, StepKind.Escort, At(418.3f, 4.2f, -79.2f, r: 8), 1059343),
            new(6, StepKind.Battle, At(428.1f, 7.9f, -87.0f, r: 3.8f)),
            new(7, StepKind.Talk, At(424.7f, 6.9f, -93.0f, 1059344)),
            new(255, StepKind.Talk, TiaCamp),
        ], Accept: new(Board: 1)),

        new(71032, "Gobsmacked", 40, TiaCamp,
        [
            new(1, StepKind.Talk, TiaCamp, Need: new(Summoned: Beast.CuSith)),
            new(2, StepKind.Talk, TiaCamp, Need: new(Beasts: 20)),
            new(255, StepKind.Talk, LaudaBentbranch),
        ]),

        new(71033, "Sin of Blood", 40, LaudaBentbranch,
        [
            new(1, StepKind.Interact, At(-26.1f, 1.7f, -163.3f, 2015400)),
            new(255, StepKind.Talk, TiaCamp),
        ], Accept: new(Board: 2)),

        new(71034, "A Beastmaster's Path", 50, TiaCamp,
        [
            new(1, StepKind.FightDummy, Dummies),
            new(2, StepKind.Talk, TiaBannock),
            new(3, StepKind.Talk, TiaCamp),
            new(255, StepKind.Talk, LaudaPath),
        ]),

        new(71035, "Bonds Unbroken", 50, LaudaPath,
        [
            new(1, StepKind.Talk, At(-24.7f, -35.5f, 426.8f, 1059411)),
            new(255, StepKind.Talk, LaudaPath),
        ], Accept: new(Board: 3)),

        new(71036, "Free for All", 50, Sylmond, [new(255, StepKind.Talk, Sylmond)], Accept: new(Beasts: 25)),
        new(71037, "Mastery Rematch", 50, Sylmond, [new(255, StepKind.Talk, Sylmond)], Accept: new(Beasts: 30, Board: 4)),
        new(71045, "Blazing Trails", 50, Sylmond, [new(255, StepKind.Talk, Sylmond)], Accept: new(Board: 5)),
    ];

    public static QuestScript ById(uint id) =>
        All.FirstOrDefault(q => q.Id == id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "not a Beastmaster job quest");
}

/// <summary>XBMPet rows the quests name.</summary>
public static class Beast
{
    public const uint CuSith = 1;
    public const uint OpoOpo = 5;
    public const uint Geshunpest = 13;
    public const uint FlyingTrap = 20;
}
