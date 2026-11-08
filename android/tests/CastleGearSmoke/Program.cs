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
var blueprint = new byte[32768]; var map = new ushort[16384]; var palette = new ushort[16];
palette[1] = 31; palette[2] = 31 << 10;
map[20 * 64 + 10] = 0x0021; // x40 red, x41 blue, low nibble first.
blueprint[20 * 128 + 21] = 0x20; // high nibble x43: unexplored blueprint only.
var normal = CastleMapFrame.Decode(blueprint, flags, map, palette, false, 10, 5);
Check(normal.Explored == 2 && normal.Known == 3, "packed explored/known counts, no other castle bleed");
Check(normal.Pixels[20 * 256 + 40] == unchecked((int)0xffff0000), "PSX BGR555 red and low nibble");
Check(normal.Pixels[20 * 256 + 41] == unchecked((int)0xff0000ff), "high VRAM nibble blue");
Check(normal.Pixels[20 * 256 + 42] == unchecked((int)0xff000000), "no rectangular fill over empty geometry");
Check(normal.Pixels[20 * 256 + 43] != unchecked((int)0xff000000), "RAM blueprint uses same pixel grid");
Check((normal.PlayerPixelX, normal.PlayerPixelY) == (42,22), "normal marker shares bitmap coordinates");
var inverted = CastleMapFrame.Decode(blueprint, flags, map, palette, true, 53, 58);
Check(inverted.Explored == 3 && inverted.Known == 4, "inverted bank counts");
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
var status = GearStatus.Read(a=>words.GetValueOrDefault(a), a=>bytes.GetValueOrDefault(a));
Check(status.Hp==37 && status.HpMax==80 && status.Mp==12 && status.Hearts==8, "real PlayerStatus vitals offsets");
Check(status.Level==9 && status.Experience==1234 && status.Gold==500, "level experience gold offsets");
Check(status.Equipment.SequenceEqual(new uint[]{10,11,12,13,14,15,16}), "all seven actual equipment ids, not attack damage");
Check(status.Relics==2, "relic found bit, not activation bit");
Check(status.BossRecords==2, "boss records exclude prologue, Richter rescue and final savepoint");
Check(status.Hours==3 && status.Minutes==4 && status.Seconds==5 && status.Rooms==145, "play time and global rooms");
Check(status.Stage==0x27 && status.Music==17, "area and current music id");
Console.WriteLine($"Castle Gear: {checks} ROM-free checks passed");
