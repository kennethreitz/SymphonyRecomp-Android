namespace SymphonyRecomp.Android.SecondScreen;

// Runtime-only view of entity 0's current streamed sprite. The renderer's
// PlayerDraw.rect0 owns the actual uploaded pixels, for Alucard/Richter alike.
public sealed record PlayerPortrait(int[] Pixels, int Width, int Height, uint CharacterId)
{
    public string Name => CharacterName(CharacterId);
    public static string CharacterName(uint id) => id switch
        { 0 => "ALUCARD", 1 => "RICHTER", 2 => "MARIA", _ => $"PLAYER {id}" };
    public const uint CharacterAddress = 0x8003C9A0;
    public sealed record Source(int X,int Y,int Words,int Height,ushort Clut,bool Flip,uint CharacterId);
    static bool Pointer(uint p) => p >= 0x80000000 && p < 0x80800000;
    public static Source? Describe(Func<uint,uint> word, Func<uint,ushort> half)
    {
        const uint entity = 0x800733D8;
        short set = (short)half(entity+0x54);
        int pose = half(entity+0x56)&0x7fff, slot=half(entity+0x5a);
        if (set == 0 || pose == 0 || pose > 4095 || slot >= 16) return null;
        uint banks = set > 0 ? 0x800A3B70 : word(0x8003C788);
        if (!Pointer(banks)) return null;
        uint bank = word(banks+(uint)(set&0x7fff)*4);
        if (!Pointer(bank)) return null;
        uint frame = word(bank+(uint)pose*4);
        if (!Pointer(frame) || (half(frame)&0x8000)==0) return null;
        uint draw = 0x80097D1C+(uint)slot*40;
        int x=half(draw),y=half(draw+2),w=half(draw+4),h=half(draw+6);
        if (w < 1 || w > 48 || h < 1 || h > 192 || x+w>1024 || y+h>512) return null;
        ushort palette=half(entity+0x16);
        uint index=(palette&0x8000)!=0 ? (uint)(palette&0x7fff) : (uint)(palette+half(frame+6));
        if (index >= 768) return null;
        return new(x,y,w,h,half(0x8003C104+index*2),half(entity+0x14)!=0,
            word(CharacterAddress));
    }
    public static PlayerPortrait Decode(Source source, ReadOnlySpan<ushort> words,ReadOnlySpan<ushort> palette)
    {
        int width=source.Words*4;
        if(words.Length!=source.Words*source.Height || palette.Length!=16) throw new ArgumentException("Sprite buffer size");
        var pixels=new int[width*source.Height];
        for(int y=0;y<source.Height;y++) for(int x=0;x<width;x++)
        {
            int index=(words[y*source.Words+x/4]>>((x&3)*4))&15;
            int targetX=source.Flip?width-1-x:x;
            pixels[y*width+targetX]=index==0?0:CastleMapFrame.Color555(palette[index]);
        }
        return new(pixels,width,source.Height,source.CharacterId);
    }
    public static float FitScale(int width,int height,float boxWidth,float boxHeight)
        => width>0 && height>0?MathF.Min(boxWidth/width,boxHeight/height):0;
}
