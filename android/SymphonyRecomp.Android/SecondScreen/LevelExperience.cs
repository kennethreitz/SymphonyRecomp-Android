namespace SymphonyRecomp.Android.SecondScreen;

// US MenuDrawStats uses g_ExpNext[level+1] - g_Status.exp, and zero at 99.
// Read thresholds from the user's running game; no game table is embedded.
public readonly record struct LevelExperience(uint Start, uint End, uint Next,
    float Fraction, bool MaxLevel, bool Available)
{
    public const uint TableAddress = 0x800AC780;
    public static LevelExperience Read(int level, uint experience, Func<uint,uint> word)
    {
        if (level == 99) return new(0,0,0,1,true,true);
        if (level < 1 || level > 99) return default;
        uint start = word(TableAddress + (uint)level*4);
        uint end = word(TableAddress + (uint)(level+1)*4);
        if (end <= start) return default;
        return new(start,end, experience < end ? end-experience : 0,
            (float)Math.Clamp(((double)experience-start)/(end-start),0,1),false,true);
    }
}
