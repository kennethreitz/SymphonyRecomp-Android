using SymphonyRecomp.Android.SecondScreen;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length == 1)
{
    using var pe = new PEReader(File.OpenRead(args[0]));
    var md = pe.GetMetadataReader();
    foreach (var h in md.TypeDefinitions)
    {
        var t = md.GetTypeDefinition(h); var ns = md.GetString(t.Namespace); var name = md.GetString(t.Name);
        if (ns == "Recompiled" && new[] { "dra", "st0", "lib", "no3" }.Contains(name))
            Console.WriteLine($"GAME_TYPE {ns}.{name} methods={t.GetMethods().Count}");
    }
    foreach (var h in md.ManifestResources)
        Console.WriteLine("RESOURCE " + md.GetString(md.GetManifestResource(h).Name));
    return;
}
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
var flags = new byte[0x800];
flags[0] = 0b01_10_11_00;
flags[0x400] = 0b10_01_00_11;
for (int x = 0; x < 4; x++) Check(CastleMapFrame.StateAt(flags, x, 0, false) == new[]{1,2,3,0}[x], "normal packed pair " + x);
for (int x = 0; x < 4; x++) Check(CastleMapFrame.StateAt(flags, x, 0, true) == new[]{2,1,0,3}[x], "inverted bank pair " + x);
flags[0x7ff] = 1;
Check(CastleMapFrame.StateAt(flags, 63, 63, true) == 1, "last inverted cell in bounds");
Check(CastleMapFrame.StateAt(flags, -1, 64, true) == 0, "invalid cell hidden");
var map = new ushort[16384]; var palette = new ushort[16];
palette[1] = 31; palette[2] = 31 << 10;
map[20 * 64 + 10] = 0x0021; // x40 red, x41 blue, low nibble first.

var normal = CastleMapFrame.Decode(flags, map, palette, false, 10, 5);
Check(normal.Explored == 2 && normal.Known == 1, "packed explored/known counts, no other castle bleed");
Check(normal.Pixels[20 * 256 + 40] == unchecked((int)0xffff0000), "PSX BGR555 red and low nibble");
Check(normal.Pixels[20 * 256 + 41] == unchecked((int)0xff0000ff), "high VRAM nibble blue");
Check(normal.Pixels[20 * 256 + 42] == unchecked((int)0xff000000), "no rectangular fill over empty geometry");
Check(normal.Pixels[20 * 256 + 43] == unchecked((int)0xff000000), "unexplored blueprint is hidden, not painted as explored rooms");
Check((normal.PlayerPixelX, normal.PlayerPixelY) == (42,22), "normal marker shares bitmap coordinates");
var inverted = CastleMapFrame.Decode(flags, map, palette, true, 53, 58);
Check(inverted.Explored == 3 && inverted.Known == 1, "inverted bank counts");
Check(inverted.Pixels[235 * 256 + 215] == normal.Pixels[20 * 256 + 40], "inverted pixels rotate exactly 180 degrees");
Check((inverted.PlayerPixelX, inverted.PlayerPixelY) == (213,233), "inverted marker rotates with same transform, no magic row offset");
Check(CastleMapFrame.PlayerPixel(64, 2, false) == (-1,-1), "out-of-range marker absent");
var words = new Dictionary<uint,uint>(); var bytes = new Dictionary<uint,byte>(); uint s = GearStatus.StatusAddress;
words[s+0x23c]=37; words[s+0x240]=80; words[s+0x24c]=12; words[s+0x250]=40;
words[s+0x244]=8; words[s+0x248]=60; words[s+0x284]=9; words[s+0x288]=1234; words[s+0x28c]=500;
for (uint i=0;i<7;i++) words[s+0x29c+i*4]=i+10;
words[s+0x2b8]=999; words[s+0x2cc]=3; words[s+0x2d0]=4; words[s+0x2d4]=5;
words[0x8003C760]=145; words[0x800974a0]=0x27;
bytes[s]=1; bytes[s+1]=3; bytes[s+2]=2; bytes[0x8013901c]=17;
foreach(uint i in new uint[]{0,1,14,21,22}) words[0x8003ca28+i*4]=99;
words[LevelExperience.TableAddress+9*4]=1000; words[LevelExperience.TableAddress+10*4]=1500;
var status = GearStatus.Read(a=>words.GetValueOrDefault(a), a=>bytes.GetValueOrDefault(a));
Check(status.Hp==37 && status.HpMax==80 && status.Mp==12 && status.Hearts==8, "real PlayerStatus vitals offsets");
Check(status.LevelProgress.Next==266 && Math.Abs(status.LevelProgress.Fraction-.468f)<.0001,"snapshot reads both native level thresholds");
Check(status.Level==9 && status.Experience==1234 && status.Gold==500, "level experience gold offsets");
Check(status.Equipment.SequenceEqual(new uint[]{10,11,12,13,14,15,16}), "all seven actual equipment ids, not attack damage");
Check(status.Relics==2, "relic found bit, not activation bit");
Check(status.BossRecords==2, "boss records exclude prologue, Richter rescue and final savepoint");
Check(status.Hours==3 && status.Minutes==4 && status.Seconds==5 && status.Rooms==145, "play time and global rooms");
Check(status.Stage==0x27 && status.Music==17, "area and current music id");
var viewport = new MapViewport();
viewport.Reset();
viewport.PanBy(999,-999); Check(viewport.CenterX==128 && viewport.CenterY==128,"fit cannot pan map out of view");
viewport.ScaleBy(2); viewport.PanBy(30,-20); Check(viewport.Zoom==2 && viewport.CenterX==158 && viewport.CenterY==108,"zoomed drag pans in source-pixel coordinates");
viewport.ScaleBy(100); viewport.PanBy(999,-999); Check(viewport.Zoom==8 && viewport.CenterX==256 && viewport.CenterY==0,"zoom and pan are bounded");
viewport.Reset(); Check(viewport.Zoom==1 && viewport.CenterX==128 && viewport.CenterY==128,"FIT resets zoom and pan");
viewport.FocusPlayer(30,122);
Check(viewport.Zoom==4 && viewport.CenterX==30 && viewport.CenterY==122,"MAP opens 4x centered on Alchemy Lab save-room pause-map pixel");
Check(CastleMapFrame.PlayerPixel(7,30,false)==(30,122),"US DrawMapCursor cell 7,30 uses exact native pixel 30,122");
var projected = viewport.Project(30,122,8);
Check(projected==(500,505),"native current room and marker project to the same viewport center");
var origin=viewport.Project(0,0,8);
Check(origin.X+30*8==projected.X && origin.Y+122*8==projected.Y,"letterbox/viewport origin shared by native bitmap and marker");
Check(RecompOne.Runtime.Hle.PsxPixel.FromRgba8(255,0,0,255)==0x801f,"guaranteed GLES RGBA8 readback converts red plus STP");
Check(RecompOne.Runtime.Hle.PsxPixel.FromRgba8(0,255,0,0)==0x03e0,"RGBA8 readback converts green without alpha");
for(int v=0;v<32;v++) { byte expanded=(byte)((v*255+15)/31); Check((RecompOne.Runtime.Hle.PsxPixel.FromRgba8(expanded,0,0,0)&31)==v,"all 5-bit channels round trip through RGBA8 "+v); }
Check(CastleMapFrame.ClutOrigin(0x3d20)==(512,244),"map CLUT origin decoded from the game CLUT id, never hardcoded");
Check(CastleMapFrame.MapClutAddress==0x8003C484,"InitFade logical map palette 0x1c0 resolves through g_ClutIds");

var threshold=new Dictionary<uint,uint> { [LevelExperience.TableAddress+4*4]=500,[LevelExperience.TableAddress+5*4]=950 };
var xp=LevelExperience.Read(4,675,a=>threshold.GetValueOrDefault(a));
Check(xp.Next==275 && Math.Abs(xp.Fraction-175f/450)<.0001,"NEXT uses next-level table entry and current-level progress");
Check(LevelExperience.Read(4,500,a=>threshold.GetValueOrDefault(a)).Fraction==0,"new level starts empty");
Check(LevelExperience.Read(4,950,a=>threshold.GetValueOrDefault(a)).Next==0,"threshold reached has no unsigned NEXT underflow");
Check(LevelExperience.Read(4,499,a=>threshold.GetValueOrDefault(a)).Fraction==0,"experience below threshold clamps progress");
var maximum=LevelExperience.Read(99,uint.MaxValue,_=>throw new Exception("max level must not read past table"));
Check(maximum.Next==0 && maximum.Fraction==1 && maximum.MaxLevel,"level 99 shows NEXT 0/MAX with full bar and no level-100 lookup");
Check(!LevelExperience.Read(0,0,_=>0).Available && !LevelExperience.Read(100,0,_=>0).Available,"invalid levels do not index game table");
Check(!LevelExperience.Read(4,0,_=>100).Available,"invalid/zero-width threshold range is guarded");
var portraitPalette=new ushort[16];portraitPalette[1]=31;portraitPalette[2]=0x7c00;
var source=new PlayerPortrait.Source(513,257,1,1,0,false,0);
var sprite=PlayerPortrait.Decode(source,new ushort[]{0x0201},portraitPalette);
Check(sprite.Pixels[0]==CastleMapFrame.Color555(31) && sprite.Pixels[1]==0 && sprite.Pixels[2]==CastleMapFrame.Color555(0x7c00),"live 4bpp sprite nibble order and transparent index zero");
var flipped=PlayerPortrait.Decode(source with {Flip=true,CharacterId=1},new ushort[]{0x0201},portraitPalette);
Check(flipped.Pixels[3]==sprite.Pixels[0] && flipped.Name=="RICHTER","current character identity and facing preserved, not hardcoded Alucard");
Check(PlayerPortrait.FitScale(32,64,132,172)==172f/64,"portrait scaling preserves aspect ratio");
Check(PlayerPortrait.Describe(_=>0,_=>0)==null,"missing entity sprite metadata safely hides portrait");

var portraitWords=new Dictionary<uint,uint> { [0x800A3B74]=0x80100000,[0x80100004]=0x80101000,[PlayerPortrait.CharacterAddress]=0 };
var portraitHalves=new Dictionary<uint,ushort> { [0x8007342c]=1,[0x8007342e]=1,[0x80101000]=0x8001,[0x80097d1c]=513,[0x80097d1e]=257,[0x80097d20]=8,[0x80097d22]=64,[0x800733ee]=0x8100,[0x8003c304]=0x3c00 };
var described=PlayerPortrait.Describe(a=>portraitWords.GetValueOrDefault(a),a=>portraitHalves.GetValueOrDefault(a));
Check(described is {X:513,Y:257,Words:8,Height:64,CharacterId:0},"Alucard entity resolves renderer-owned streamed VRAM rectangle");
portraitHalves[0x8007342c]=0x8001;portraitWords[0x8003c788]=0x800f0000;portraitWords[0x800f0004]=0x80100000;portraitWords[PlayerPortrait.CharacterAddress]=1;
described=PlayerPortrait.Describe(a=>portraitWords.GetValueOrDefault(a),a=>portraitHalves.GetValueOrDefault(a));
Check(described is {CharacterId:1,Words:8},"Richter/overlay negative animation bank uses the same live extraction path");
portraitHalves[0x80097d22]=500;
Check(PlayerPortrait.Describe(a=>portraitWords.GetValueOrDefault(a),a=>portraitHalves.GetValueOrDefault(a))==null,"out-of-bounds sprite readback is rejected");

// Generic push API ownership, bounds and immutability without Android or a ROM.
using(var provider=GearProviders.Register("test"))
{
    var fields=new List<GearField>{new("Alpha\n","One")};
    provider.Publish(new GearCard("CARD",fields));
    fields[0]=new("Changed","Two");
    Check(GearProviders.Snapshot()[0].Fields[0]==new GearField("Alpha","One"),"provider publication copies and sanitizes caller data");
    bool duplicate=false;try{GearProviders.Register("TEST");}catch(InvalidOperationException){duplicate=true;}
    Check(duplicate,"provider IDs are unique case-insensitively");
    provider.Publish(new GearCard(new string('t',100),Enumerable.Range(0,100).Select(i=>new GearField(new string('n',100),new string('v',200))).ToArray()));
    var card=GearProviders.Snapshot()[0];
    Check(card.Title.Length==48 && card.Fields.Count==64 && card.Fields[0].Name.Length==40 && card.Fields[0].Value.Length==96,"provider payload sizes are bounded");
    provider.Publish();
    Check(GearProviders.Snapshot().Count==0,"empty publication removes provider cards");
}
Check(GearProviders.Snapshot().Count==0,"unload removes provider cards");
var disposed=GearProviders.Register("test");disposed.Dispose();disposed.Dispose();
bool rejected=false;try{disposed.Publish();}catch(ObjectDisposedException){rejected=true;}
Check(rejected,"disposed publisher cannot resurrect cards");
var tracker=new CastleGearPlusData.PlusTracker();
var sample=new CastleGearPlusData.PlusSample(2,3,1,12,0,0,100,73,73,84,53,7,30,1u<<10);
tracker.Observe(sample);
Check(tracker.SessionKills==0 && tracker.HpLost==0 && tracker.ActiveTicks==0,"first live sample establishes baselines without counting loaded stats");
tracker.Observe(sample with {Clock=101,Hp=68,Kills=86,RelicMask=(1u<<10)|(1u<<11)});
Check(tracker.SessionKills==2 && tracker.HpLost==5 && tracker.LastHpLoss==5 && tracker.ActiveTicks==1,"consecutive native frames count kills, observed HP loss and active time");
tracker.Observe(sample with {Clock=102,Hp=73,Kills=86});
Check(tracker.HpLost==5,"healing is not negative damage");
tracker.Observe(sample with {Clock=103,Hp=10,HpMax=10,Kills=86});
Check(tracker.HpLost==5,"max-HP changes are not counted as damage");
tracker.Observe(sample with {Clock=104,Hp=2,RoomX=8,Kills=86});
Check(tracker.HpLost==5,"room transitions do not produce spurious damage");
tracker.Observe(sample with {Clock=110,Hp=1,Kills=86});
Check(tracker.HpLost==5,"skipped native frames are not inferred as damage");
long before=tracker.ActiveTicks;
tracker.Observe(sample with {Clock=111,Engine=2});
tracker.Observe(sample with {Clock=500,Hp=1,Kills=100});
Check(tracker.HpLost==5 && tracker.SessionKills==2 && tracker.ActiveTicks==before,"menus and resume reset baselines instead of counting loaded deltas");
tracker.BreakContinuity();tracker.Observe(sample with {Clock=501,Hp=1,Kills=200});
Check(tracker.SessionKills==2,"save-loaded/new-player event breaks continuity");
tracker.Observe(sample with {Clock=502,Stage=1,Kills=200});
Check(tracker.AreaTicks.Count==2 && tracker.AreaTicks[1]==1,"active time is attributed independently to each area");
tracker.Observe(sample with {State=3});tracker.Observe(sample with {State=3});
Check(tracker.GameOvers==1,"game-over transition counted once, not every VSync");
tracker.SaveCreated();
Check(tracker.SaveSnapshots==1,"native save-snapshot event count is distinct from disk-write success");
Check(CastleGearPlusData.PlusTracker.Completion(53)=="5.6%" && CastleGearPlusData.PlusTracker.Completion(942)=="100.0%" && CastleGearPlusData.PlusTracker.Completion(1890)=="200.6%","map completion matches native integer tenths including both castles");
var cards=tracker.Cards(id=>"Area "+id);
var checklist=cards.Single(c=>c.Title.StartsWith("RELIC CHECKLIST"));
Check(checklist.Fields.Count==28 && checklist.Fields.Single(f=>f.Name=="Cube of Zoe").Value=="HAVE" && checklist.Fields.All(f=>!f.Name.StartsWith("JP")),"US checklist includes 28 obtainable relics and tests found bit");
Check(!(sample with {Demo=1}).Active && !(sample with {Stage=31}).InStage,"demo and Richter prologue are excluded from castle telemetry");
var sampleWords=new Dictionary<uint,uint>{[0x8003C734]=2,[0x80073060]=3,[0x8003C9A4]=1,[0x800974A0]=12,[0x80097C30]=1,[0x80097C34]=2,[0x80097C38]=3,[0x80097C3C]=4,[0x80097BF4]=84,[0x8003C760]=53,[0x80097BA0]=73,[0x80097BA4]=73,[0x800730B0]=7,[0x800730B4]=30};
var read=CastleGearPlusData.PlusSample.Read(a=>sampleWords.GetValueOrDefault(a),a=>(byte)(a==0x8009796e?3:0));
Check(read.Clock==223384 && read.Kills==84 && read.Rooms==53 && read.RelicMask==(1u<<10) && read.RoomY==30,"US telemetry addresses, native play clock and relic found flags are independently sampled");

var dedup=new CastleGearPlusData.PlusTracker();dedup.Observe(sample);
dedup.Observe(sample with {Hp=68,Kills=85});
dedup.Observe(sample with {Clock=101,Hp=68,Kills=85});
Check(dedup.HpLost==5 && dedup.SessionKills==1 && dedup.ActiveTicks==1,"duplicate VSync within one native frame cannot erase the comparison baseline");
dedup.Observe(sample with {State=1});dedup.Observe(sample with {State=3,Demo=1});
Check(dedup.GameOvers==0,"title/demo game-over transitions cannot count as a player death");

var seeded=Path.Combine(AppContext.BaseDirectory,"bundle-test-"+Guid.NewGuid().ToString("N"));
SymphonyRecomp.Android.BundledMods.Seed(seeded);
string deployed=Path.Combine(seeded,"CastleGearPlus");
Check(File.Exists(Path.Combine(deployed,"mod.json")) && File.Exists(Path.Combine(deployed,"PlusTracker.cs")) && File.Exists(Path.Combine(deployed,"CastleGearPlus.cs")),"bundled source deploys through app-owned storage");
File.WriteAllText(Path.Combine(deployed,"PlusTracker.cs"),"user source");
File.WriteAllText(Path.Combine(seeded,"mod-settings.json"),"user settings");
SymphonyRecomp.Android.BundledMods.Seed(seeded);
Check(File.ReadAllText(Path.Combine(deployed,"PlusTracker.cs"))=="user source","bundled source never overwrites user edits");
Check(File.ReadAllText(Path.Combine(seeded,"mod-settings.json"))=="user settings","bundled source does not enable mods or change settings");

Check(GearLayout.Tab(1,4).CenterX==384 && GearLayout.Tab(3,4).Right==GearLayout.Card.Right,"four-tab layout derives MAP and rightmost tab geometry from the full card");
for(int tab=0;tab<4;tab++)
{
    var bounds=GearLayout.Tab(tab,4);
    float inkOrigin=GearLayout.InkOrigin(bounds.CenterX,-2,67);
    Check((inkOrigin-2+inkOrigin+67)/2==bounds.CenterX && bounds.Contains(bounds.CenterX,bounds.CenterY),"tab label ink and touch target share exact center "+tab);
}
float firstBaseline=GearLayout.EmptyFirstBaseline(-30,7);
Check((firstBaseline-30+firstBaseline+86+7)/2==GearLayout.Card.CenterY,"three-line empty block is vertically centered by actual first/last glyph bounds");
Check(GearLayout.InkOrigin(GearLayout.Card.CenterX,-1,349)+(-1+349)/2f==500,"empty-state lines center independently on the complete card, not a status column");

Console.WriteLine($"Castle Gear: {checks} ROM-free checks passed");
