namespace SymphonyRecomp.Android.SecondScreen;

// Read-only US PlayerStatus layout, include/game.h, g_Status = 0x80097964.
// Use the actual seven equipment ids at 0x29c, NOT attackHands damage at 0x2b8.
public sealed record GearStatus(int Hp, int HpMax, int Mp, int MpMax, int Hearts,
    int HeartsMax, int Level, uint Experience, uint Gold, int Relics, uint[] Equipment,
    int Hours, int Minutes, int Seconds, int Rooms, int BossRecords, uint Stage, uint Music)
{
    public const uint StatusAddress = 0x80097964;
    public static GearStatus Read(Func<uint, uint> word, Func<uint, byte> octet)
    {
        uint s = StatusAddress;
        int relics = 0, bosses = 0;
        for (uint i = 0; i < 30; i++) if ((octet(s + i) & 1) != 0) relics++;
        // Time-attack boss records 1..21; record 14 is rescuing Richter, not a kill.
        // The prologue Dracula and final-savepoint/encounter records are excluded.
        for (uint i = 1; i <= 21; i++)
            if (i != 14 && word(0x8003CA28 + i * 4) != 0) bosses++;
        var equipment = new uint[7];
        for (uint i = 0; i < 7; i++) equipment[i] = word(s + 0x29c + i * 4);
        return new((int)word(s + 0x23c), (int)word(s + 0x240),
            (int)word(s + 0x24c), (int)word(s + 0x250),
            (int)word(s + 0x244), (int)word(s + 0x248),
            (int)word(s + 0x284), word(s + 0x288), word(s + 0x28c), relics, equipment,
            (int)word(s + 0x2cc), (int)word(s + 0x2d0), (int)word(s + 0x2d4),
            (int)word(0x8003C760), bosses, word(0x800974A0), octet(0x8013901c));
    }
}

public sealed record GearFrame(CastleMapFrame Map, GearStatus Status);
