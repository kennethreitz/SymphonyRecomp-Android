using System;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Modding;
using Recompiled;
using Sotn;
using SkipPrologueData;
using SymphonyRecomp.Android.SecondScreen;
using GameRuntime=RecompOne.Runtime.Runtime;

public sealed class SkipPrologue : IMod
{
    readonly SkipPolicy _policy=new();
    GearProviders.Registration _card;
    SkipPhase _published=(SkipPhase)(-1);
    public void OnLoad()
    {
        _card=GearProviders.Register("skip-prologue");
        Event.AddListener<VSyncEvent>(Tick);
        Event.AddListener<SaveLoadedEvent>(SaveLoaded);
        Publish();
        Console.WriteLine("[SkipPrologue] SOURCE_LOADED EVENTS_ONLY NEW_GAMES_ONLY NO_DETOURS");
    }
    void SaveLoaded(SaveLoadedEvent evt)
    {
        _policy.Cancel();Publish();
        Console.WriteLine("[SkipPrologue] SAVE_LOAD_IGNORED no game writes");
    }
    void Publish()
    {
        if(_published==_policy.Phase)return;
        _published=_policy.Phase;
        _card.Publish(new GearCard("SKIP PROLOGUE",new[] {
            new GearField("Scope","Fresh Alucard games only"),new GearField("State",_policy.Phase.ToString()),
            new GearField("Outcome","Full HP / Cross / fast tier"),new GearField("Save access","None; loads are ignored") }));
    }
    void Tick(VSyncEvent evt)
    {
        var m=GameRuntime.Mem;if(m==null)return;
        try
        {
            var sample=SkipSample.Read(m.ReadU32,m.ReadU8);
            var before=_policy.Phase;
            var action=_policy.Observe(sample);
            if(before!=_policy.Phase)Console.WriteLine("[SkipPrologue] PHASE "+before+"->"+_policy.Phase+" "+sample);
            switch(action)
            {
                case SkipAction.RouteEntrance:
                    m.WriteU32(0x800974A0,0x41);
                    Console.WriteLine("[SkipPrologue] FRESH_CONFIRMED ST0->NO3 via native name-entry fence");break;
                case SkipAction.SkipOpening:
                    // Same native cleanup/loader used by HandleVideoPlayback's START skip.
                    GameApi.Call(0x800E4970);
                    Console.WriteLine("[SkipPrologue] OPENING_SKIPPED native transition to loading");break;
                case SkipAction.SeedOutcome:
                    SkipPolicy.SeedOutcome(m.WriteU32);
                    GameApi.TimeAttackController(TimeAttackEvent.DraculaDefeat,1);
                    Console.WriteLine("[SkipPrologue] OUTCOME_SEEDED before native InitStatsAndGear; Dracula record="+m.ReadU32(0x8003CA28));break;
                case SkipAction.EntranceSpawn:
                    // Native HandleNowLoading's already-complete entrance selector.
                    // Apply AFTER HandlePlay resets castle flags, BEFORE room init.
                    m.WriteU32(0x8006C374,0x11);
                    m.WriteU8(0x8003BE20,1); // g_CastleFlags[PROLOGUE_COMPLETE]
                    Console.WriteLine("[SkipPrologue] ENTRANCE_READY native spawn 0x11; opening run complete");break;
                case SkipAction.Finished:
                    Console.WriteLine("[SkipPrologue] COMPLETE stage="+m.ReadU32(0x800974A0)+" character="+m.ReadU32(0x8003C9A0)+
                        " HP="+m.ReadU32(0x80097BA0)+"/"+m.ReadU32(0x80097BA4)+" MP="+m.ReadU32(0x80097BB0)+"/"+m.ReadU32(0x80097BB4)+
                        " STR="+m.ReadU32(0x80097BB8)+" CON="+m.ReadU32(0x80097BBC)+" INT="+m.ReadU32(0x80097BC0)+" LCK="+m.ReadU32(0x80097BC4));break;
            }
            Publish();
        }
        catch(Exception ex)
        {
            _policy.Cancel();Publish();
            Console.Error.WriteLine("[SkipPrologue] ABORTED "+ex);
        }
    }
    public void OnUnload()
    {
        Event.RemoveListener<VSyncEvent>(Tick);Event.RemoveListener<SaveLoadedEvent>(SaveLoaded);
        _policy.Cancel();_card?.Dispose();
    }
}
