namespace SymphonyRecomp.Android.SecondScreen;

// US-disc RAM/VRAM layout documented by Xeeynamo/sotn-decomp, include/game.h
// and src/dra/{5087C,624DC}.c. Original decoder, no disc pixels or decomp code.
public sealed record CastleMapFrame(int[] Pixels, bool Inverted, int Explored,
    int Known, int PlayerPixelX, int PlayerPixelY)
{
    public const int Size = 256;
    public const int Cell = 4;
    public const int Grid = 64;
    public const uint BlueprintAddress = 0x801E0000;
    public const uint FlagsAddress = 0x8006BB74;
    public const int VramX = 832, VramY = 256, VramRowWords = 64;
    public const int PaletteX = 0, PaletteY = 7; // PSX CLUT id 0x1c0

    // Four cells per byte, high pair first. Separate 0x400-byte castle banks.
    public static int StateAt(ReadOnlySpan<byte> flags, int x, int y, bool inverted)
    {
        if ((uint)x >= Grid || (uint)y >= Grid) return 0;
        int offset = (inverted ? 0x400 : 0) + y * 16 + x / 4;
        return (flags[offset] >> ((3 - (x & 3)) * 2)) & 3;
    }

    public static (int X, int Y) PlayerPixel(int x, int y, bool inverted)
    {
        if ((uint)x >= Grid || (uint)y >= Grid) return (-1, -1);
        int sourceX = inverted ? Grid - 1 - x : x;
        int sourceY = inverted ? Grid - 1 - y : y;
        return Transform(sourceX * Cell + 2, sourceY * Cell + 2, inverted);
    }

    public static (int X, int Y) Transform(int x, int y, bool inverted)
        => inverted ? (Size - 1 - x, Size - 1 - y) : (x, y);

    public static int Color555(ushort color)
    {
        int r = color & 31, g = (color >> 5) & 31, b = (color >> 10) & 31;
        return unchecked((int)0xff000000) | ((r * 255 / 31) << 16)
            | ((g * 255 / 31) << 8) | (b * 255 / 31);
    }

    public static CastleMapFrame Decode(ReadOnlySpan<byte> blueprint,
        ReadOnlySpan<byte> flags, ReadOnlySpan<ushort> map, ReadOnlySpan<ushort> palette,
        bool inverted, int playerX, int playerY)
    {
        if (blueprint.Length != Size * Size / 2 || flags.Length != 0x800 ||
            map.Length != Size * VramRowWords || palette.Length != 16)
            throw new ArgumentException("Invalid castle map snapshot dimensions");
        int explored = 0, known = 0;
        for (int y = 0; y < Grid; y++)
        for (int x = 0; x < Grid; x++)
        {
            int state = StateAt(flags, x, y, inverted);
            if ((state & 1) != 0) explored++;
            if (state != 0) known++;
        }
        var pixels = new int[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int live = (map[y * VramRowWords + x / 4] >> ((x & 3) * 4)) & 15;
            int original = (blueprint[y * Size / 2 + x / 2] >> ((x & 1) * 4)) & 15;
            // Live VRAM already includes the game's explored fills, shared walls,
            // save/warp colors and secret passage pixels. Never paint cell rectangles.
            // Underlay the same RAM blueprint, dimmed, as an unexplored-castle guide.
            int color = live != 0 ? Color555(palette[live]) : original == 0
                ? unchecked((int)0xff000000) : original <= 2
                ? unchecked((int)0xff202630) : unchecked((int)0xff485260);
            var (dx, dy) = Transform(x, y, inverted);
            pixels[dy * Size + dx] = color;
        }
        var marker = PlayerPixel(playerX, playerY, inverted);
        return new(pixels, inverted, explored, known, marker.X, marker.Y);
    }
}
