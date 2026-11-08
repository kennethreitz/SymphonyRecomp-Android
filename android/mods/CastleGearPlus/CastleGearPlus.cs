using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Modding;
using Recompiled;
using CastleGearPlusData;
using SymphonyRecomp.Android.SecondScreen;
using GameRuntime = RecompOne.Runtime.Runtime;

public sealed class CastleGearPlus : IMod
{
    readonly PlusTracker _tracker=new();
    GearProviders.Registration _provider;
    long _lastPublish;
    bool _reportedLive, _reportedError;
    public void OnLoad()
    {
        _provider=GearProviders.Register("castle-gear-plus");
        Publish();
        Event.AddListener<VSyncEvent>(Tick);
        Event.AddListener<PlayerLoadedEvent>(PlayerLoaded);
        Event.AddListener<SaveCreatedEvent>(SaveCreated);
        Console.WriteLine("[CastleGearPlus] SOURCE_LOADED API=1 EVENTS_ONLY NO_DETOURS");
    }
    void PlayerLoaded(PlayerLoadedEvent evt) => _tracker.BreakContinuity();
    void SaveCreated(SaveCreatedEvent evt) => _tracker.SaveCreated();
    void Tick(VSyncEvent evt)
    {
        try
        {
            var mem=GameRuntime.Mem;
            if(mem==null)return;
            var sample=PlusSample.Read(mem.ReadU32,mem.ReadU8);
            _tracker.Observe(sample);
            long now=Stopwatch.GetTimestamp();
            if(now-_lastPublish<Stopwatch.Frequency)return;
            _lastPublish=now;
            Publish();
            if(sample.InStage && !_reportedLive)
            {
                Console.WriteLine("[CastleGearPlus] LIVE map="+PlusTracker.Completion(sample.Rooms)+" rooms="+sample.Rooms+" kills="+sample.Kills+" area="+sample.Stage+" room="+sample.RoomX+","+sample.RoomY+" relicMask="+sample.RelicMask.ToString("X8"));
                _reportedLive=true;
            }
        }
        catch(Exception ex)
        {
            _tracker.BreakContinuity();
            if(!_reportedError)Console.Error.WriteLine("[CastleGearPlus] SNAPSHOT_ERROR "+ex);
            _reportedError=true;
        }
    }
    void Publish() => _provider.Publish(_tracker.Cards(stage=>Regex.Replace(((Sotn.Stage)stage).ToString(),"(?<=[a-z0-9])(?=[A-Z])"," ")));
    public void OnUnload()
    {
        Event.RemoveListener<VSyncEvent>(Tick);
        Event.RemoveListener<PlayerLoadedEvent>(PlayerLoaded);
        Event.RemoveListener<SaveCreatedEvent>(SaveCreated);
        _provider?.Dispose();
    }
}
