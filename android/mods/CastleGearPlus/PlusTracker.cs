using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SymphonyRecomp.Android.SecondScreen;

namespace CastleGearPlusData;

// Memory-layout facts only; no game implementation or extracted tables/assets.
public readonly record struct PlusSample(uint State, uint Step, uint Engine, uint Stage, uint Demo,
    uint Character, long Clock, int Hp, int HpMax, uint Kills, uint Rooms,
    int RoomX, int RoomY, uint RelicMask)
{
    public bool InStage => State == 2 && Step == 3 && Stage != 0x1f && Stage < 0x80 && Demo == 0;
    public bool Active => InStage && Engine == 1;
    public static PlusSample Read(Func<uint,uint> word, Func<uint,byte> octet)
    {
        uint mask = 0;
        for (uint i=0;i<30;i++) if ((octet(0x80097964+i)&1)!=0) mask |= 1u << (int)i;
        long clock = ((long)word(0x80097C30)*3600 + word(0x80097C34)*60 + word(0x80097C38))*60 + word(0x80097C3C);
        return new(word(0x8003C734),word(0x80073060),word(0x8003C9A4),word(0x800974A0),
            octet(0x80097914),word(0x8003C9A0),clock,(int)word(0x80097BA0),(int)word(0x80097BA4),
            word(0x80097BF4),word(0x8003C760),(int)word(0x800730B0),(int)word(0x800730B4),mask);
    }
}

public sealed class PlusTracker
{
    // US has 28 obtainable relics. Slots 23 and 24 are JP-only, not missing US relics.
    public const uint ObtainableMask = 0x3fffffff & ~(1u << 23) & ~(1u << 24);
    public static readonly string[] RelicNames = {
        "Soul of Bat", "Fire of Bat", "Echo of Bat", "Force of Echo", "Soul of Wolf", "Power of Wolf",
        "Skill of Wolf", "Form of Mist", "Power of Mist", "Gas Cloud", "Cube of Zoe", "Spirit Orb",
        "Gravity Boots", "Leap Stone", "Holy Symbol", "Faerie Scroll", "Jewel of Open", "Merman Statue",
        "Bat Card", "Ghost Card", "Faerie Card", "Demon Card", "Sword Card", "JP slot 23", "JP slot 24",
        "Heart of Vlad", "Tooth of Vlad", "Rib of Vlad", "Ring of Vlad", "Eye of Vlad"
    };
    PlusSample? _previous;
    PlusSample? _lastStage;
    uint _lastState = uint.MaxValue, _newRelics;
    bool _seenGameplay;
    public long SessionKills { get; private set; }
    public long HpLost { get; private set; }
    public int LastHpLoss { get; private set; }
    public int GameOvers { get; private set; }
    public int SaveSnapshots { get; private set; }
    public long ActiveTicks { get; private set; }
    public readonly Dictionary<uint,long> AreaTicks = new();

    // Save-loaded/new-player events break comparison baselines, never count loaded data as activity.
    public void BreakContinuity() { _previous = null; _seenGameplay = false; }
    public void SaveCreated() => SaveSnapshots++;
    public void Observe(PlusSample sample)
    {
        if (sample.State == 3 && sample.Demo == 0 && _lastState != 3 && _seenGameplay)
        { GameOvers++; _seenGameplay=false; }
        _lastState=sample.State;
        if (sample.State != 2 && sample.State != 3) _seenGameplay=false;
        if (sample.InStage) _lastStage=sample;
        if (!sample.Active) { _previous=null; return; }
        _seenGameplay=true;
        AreaTicks.TryAdd(sample.Stage,0);
        if (_previous is { } duplicate && duplicate.Clock==sample.Clock && duplicate.Character==sample.Character && duplicate.Stage==sample.Stage) return;
        if (_previous is { } old && old.Character == sample.Character && sample.Clock-old.Clock == 1)
        {
            ActiveTicks++;
            AreaTicks[sample.Stage]++;
            if (sample.Kills >= old.Kills) SessionKills += sample.Kills-old.Kills;
            _newRelics |= (sample.RelicMask & ~old.RelicMask) & ObtainableMask;
            // A precise observed HP delta, not inferred raw damage or attacker attribution.
            // Exclude room/area/max-HP transitions, reloads, menus and skipped frames.
            if (sample.Stage==old.Stage && sample.RoomX==old.RoomX && sample.RoomY==old.RoomY &&
                sample.HpMax==old.HpMax && sample.Hp>=0 && old.Hp<=old.HpMax && old.Hp>sample.Hp)
            { LastHpLoss=old.Hp-sample.Hp; HpLost+=LastHpLoss; }
        }
        _previous=sample;
    }

    // Native US save selector: integer tenths, floor(rooms * 1000 / 942).
    // Do not cap at 100: both castles can reach 200.6%.
    public static string Completion(uint rooms)
    {
        ulong tenths=(ulong)rooms*1000/942;
        return (tenths/10).ToString(CultureInfo.InvariantCulture)+"."+(tenths%10)+"%";
    }
    static string Time(long ticks)
    {
        long sec=ticks/60;
        return string.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00}",sec/3600,sec/60%60,sec%60);
    }
    static int Bits(uint bits) { int n=0;for(;bits!=0;bits&=bits-1)n++;return n; }
    static GearField F(string name,object value) => new(name,Convert.ToString(value,CultureInfo.InvariantCulture) ?? "");
    public GearCard[] Cards(Func<uint,string> areaName)
    {
        if (_lastStage is not { } s) return new[] { new GearCard("CASTLE GEAR PLUS", new[] { F("State","Awaiting live gameplay"), F("Provider API",GearProviders.ApiVersion) }) };
        var summary = new GearCard("CASTLE GEAR PLUS", new[] {
            F("Map completion",Completion(s.Rooms)), F("Rooms explored",s.Rooms), F("Kills / native total",s.Kills),
            F("Kills / session observed",SessionKills), F("HP loss / observed",HpLost), F("Last HP loss",LastHpLoss),
            F("Game overs / session",GameOvers), F("Save snapshots / session",SaveSnapshots), F("New relic types / session",Bits(_newRelics)) });
        var location = new GearCard("LOCATION / ACTIVE TIME",new[] {
            F("Area",areaName(s.Stage)), F("Area id", "0x"+s.Stage.ToString("X2")), F("Room origin",s.RoomX+", "+s.RoomY),
            F("Active session time",Time(ActiveTicks)), F("Areas visited",AreaTicks.Count),
            F("State",_previous.HasValue ? "Live / read only" : "Paused / last snapshot") });
        var relics = new GearCard("RELIC CHECKLIST / "+Bits(s.RelicMask&ObtainableMask)+" OF 28",Enumerable.Range(0,30)
            .Where(i=>(ObtainableMask&(1u<<i))!=0).Select(i=>F(RelicNames[i],(s.RelicMask&(1u<<i))!=0?"HAVE":"MISSING")).ToArray());
        var areas = new GearCard("TIME PER AREA / SESSION",AreaTicks.OrderBy(a=>a.Key).Select(a=>F(areaName(a.Key),Time(a.Value))).ToArray());
        return new[] {summary,location,relics,areas};
    }
}
