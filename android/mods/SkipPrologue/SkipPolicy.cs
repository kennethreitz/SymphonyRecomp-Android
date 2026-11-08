using System;

namespace SkipPrologueData;
public enum SkipPhase { Idle, NameEntry, Confirmed, Loading, Seeded, Spawning, Complete }
public enum SkipAction { None, RouteEntrance, SkipOpening, SeedOutcome, EntranceSpawn, Finished }
public readonly record struct SkipSample(uint State,uint Step,uint Engine,uint FreshFlag,uint Stage,uint Character,uint Demo)
{
    public bool Naming => State==8 && Engine==0x11 && Demo==0;
    public bool FreshAlucard => State==8 && Engine==0x12 && FreshFlag==0 && Stage==0x1f && Character==0 && Demo==0;
    public static SkipSample Read(Func<uint,uint> word,Func<uint,byte> octet) => new(
        word(0x8003C734),word(0x80073060),word(0x8003C9A4),word(0x8003C730),
        word(0x800974A0),word(0x8003C9A0),octet(0x80097914));
}

// A one-shot fence established only by observing the native NEW GAME name-entry flow.
// Loading a save or enabling the mod in an existing game cannot establish this fence.
public sealed class SkipPolicy
{
    public SkipPhase Phase {get;private set;}
    public void Cancel() => Phase=SkipPhase.Idle;
    public SkipAction Observe(SkipSample s)
    {
        if(s.Naming) {Phase=SkipPhase.NameEntry;return SkipAction.None;}
        if(Phase==SkipPhase.NameEntry)
        {
            if(s.FreshAlucard) {Phase=SkipPhase.Confirmed;return SkipAction.RouteEntrance;}
            Cancel();return SkipAction.None;
        }
        if(Phase==SkipPhase.Idle || Phase==SkipPhase.Complete)return SkipAction.None;
        if(s.Stage!=0x41 || s.Character!=0 || s.Demo!=0 || s.FreshFlag!=0) {Cancel();return SkipAction.None;}
        if(Phase==SkipPhase.Confirmed)
        {
            if(s.FreshFlag!=0){Cancel();return SkipAction.None;}
            if(s.State==5) {Phase=SkipPhase.Loading;return SkipAction.SkipOpening;}
            if(s.State==4)Phase=SkipPhase.Loading;
            else if(s.State!=8){Cancel();return SkipAction.None;}
        }
        if(Phase==SkipPhase.Loading && s.State==4 && s.Step==10)
        {
            if(s.FreshFlag!=0){Cancel();return SkipAction.None;}
            Phase=SkipPhase.Seeded;return SkipAction.SeedOutcome;
        }
        if(Phase==SkipPhase.Seeded && s.State==2 && s.Step==1)
        {Phase=SkipPhase.Spawning;return SkipAction.EntranceSpawn;}
        if(Phase==SkipPhase.Spawning && s.State==2 && s.Step==3 && s.Engine==1)
        {Phase=SkipPhase.Complete;return SkipAction.Finished;}
        if(s.State!=8 && s.State!=5 && s.State!=4 && s.State!=2)Cancel();
        return SkipAction.None;
    }

    // Inputs to the GAME'S initializer, never the final Alucard stats/equipment.
    // Profile: unassisted full-HP Richter, Cross, 30 hearts, below the 41-heart
    // reward threshold; one-second synthetic completion, native fastest tier.
    public static void SeedOutcome(Action<uint,uint> word)
    {
        for(uint i=0;i<32;i++)word(0x8003CA28+i*4,0);
        word(0x8003CB00,0);word(0x8003CB04,0);
        word(0x80097BA0,50);word(0x80097BA4,50);word(0x80097BA8,30);
        word(0x80097BFC,4);word(0x80139008,30);word(0x801397FC,0);
        word(0x80097C30,0);word(0x80097C34,0);word(0x80097C38,1);word(0x80097C3C,0);
        // Caller uses native TimeAttackController(DraculaDefeat, SET_RECORD).
        // Native InitStatsAndGear subsequently awards stats/items and resets time.
    }
}
