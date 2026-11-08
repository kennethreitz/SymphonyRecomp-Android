using System.Reflection;
using Android.Content;
using Android.Graphics;
using Android.Views;
using RecompOne.Runtime.Memory;
using Sotn;

namespace SymphonyRecomp.Android.SecondScreen;

// Draws the castle map for the Thor's lower screen.
//
// Canvas rather than GL on purpose: the game owns an EGL context on its own
// thread, and a second GL surface would mean either sharing that context across
// threads or building a second one. Neither is worth it to draw a few hundred
// rectangles at 10Hz.
public sealed class CastleMapView : View
{
    // Map geometry, matching the desktop tracker's overlay: the castle art is
    // 320x255 in map units and each room cell is 5 units square. Row 0 of the
    // map RAM sits 15 units above the top of the art, so early rows clip out.
    const int MapArtW = 320;
    const int MapArtH = 255;
    const int Cell = 5;
    const int RowOffset = 15;
    const int GridWidth = 64;

    // Player position. CastleX/Y are the room's origin; RoomX/Y are the offset
    // within it, in 256ths.
    const uint ZoneAddr = 0x800974A0;
    const uint CastleXAddr = 0x800730B0;
    const uint CastleYAddr = 0x800730B4;
    const uint RoomXAddr = 0x800973F0;
    const uint RoomYAddr = 0x800973F4;
    const uint PlayStateAddr = 0x80073060;

    readonly Paint _visited = new() { Color = new Color(0x3A, 0x6E, 0xA5), AntiAlias = false };
    readonly Paint _player = new() { Color = new Color(0xFF, 0xE0, 0x50), AntiAlias = false };
    readonly Paint _bitmap = new() { FilterBitmap = false, AntiAlias = false };
    readonly Paint _text = new() { Color = Color.DarkGray, TextSize = 28f, AntiAlias = true };

    Bitmap? _castle1, _castle2;
    bool _assetsTried;

    public CastleMapView(Context context) : base(context)
    {
        SetBackgroundColor(Color.Black);
    }

    // Blocky pixel art scaled up; nearest-neighbour keeps it crisp.
    void EnsureAssets()
    {
        if (_assetsTried) return;
        _assetsTried = true;
        _castle1 = LoadBitmap("map.Castle1.png");
        _castle2 = LoadBitmap("map.Castle2.png");
    }

    static Bitmap? LoadBitmap(string logicalName)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(logicalName);
            if (stream == null)
            {
                Console.Error.WriteLine($"[Map] embedded resource missing: {logicalName}");
                return null;
            }
            return BitmapFactory.DecodeStream(stream);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Map] failed to decode {logicalName}: {e.Message}");
            return null;
        }
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        EnsureAssets();

        var mem = RecompOne.Runtime.Runtime.Mem;
        if (mem == null)
        {
            canvas.DrawText("waiting for game...", 40f, 60f, _text);
            return;
        }

        bool secondCastle = (mem.ReadU8(ZoneAddr) & 0x20) == 0x20;
        var art = secondCastle ? _castle2 : _castle1;

        // Fit the map art to the panel, preserving aspect.
        float scale = Math.Min(Width / (float)MapArtW, Height / (float)MapArtH);
        float originX = (Width - MapArtW * scale) * 0.5f;
        float originY = (Height - MapArtH * scale) * 0.5f;

        DrawVisitedRooms(canvas, mem, scale, originX, originY);

        // Art goes on top: it is the room outlines with the interiors left clear,
        // so the visited fills below show through.
        if (art != null)
        {
            var dst = new RectF(originX, originY,
                originX + MapArtW * scale, originY + MapArtH * scale);
            canvas.DrawBitmap(art, null, dst, _bitmap);
        }

        DrawPlayer(canvas, mem, secondCastle, scale, originX, originY);
    }

    void DrawVisitedRooms(Canvas canvas, IMemory mem, float scale, float originX, float originY)
    {
        float size = Cell * scale;
        int rows = Map.Length / GridWidth;

        for (int y = 0; y < rows; y++)
        {
            int cy = y * Cell - RowOffset;
            if (cy < 0) continue;

            for (int x = 0; x < GridWidth; x++)
            {
                if (mem.ReadU8(Map.CastleMapAddr + (uint)(y * GridWidth + x)) == 0) continue;
                float px = originX + x * Cell * scale;
                float py = originY + cy * scale;
                canvas.DrawRect(px, py, px + size, py + size, _visited);
            }
        }
    }

    void DrawPlayer(Canvas canvas, IMemory mem, bool secondCastle,
                    float scale, float originX, float originY)
    {
        // 3 is "in a stage"; anything else means the position words are stale.
        if (mem.ReadU8(PlayStateAddr) != 3) return;

        int cx = mem.ReadU8(CastleXAddr) + mem.ReadU16(RoomXAddr) / 256;
        int cy = mem.ReadU8(CastleYAddr) + mem.ReadU16(RoomYAddr) / 256;
        if (secondCastle) cy -= 7;

        if (cx < 0 || cx >= GridWidth || cy < 0) return;

        int drawY = cy * Cell - RowOffset;
        if (drawY < 0) return;

        float size = Cell * scale;
        float px = originX + cx * Cell * scale;
        float py = originY + drawY * scale;
        canvas.DrawRect(px, py, px + size, py + size, _player);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _castle1?.Recycle();
            _castle2?.Recycle();
        }
        base.Dispose(disposing);
    }
}
