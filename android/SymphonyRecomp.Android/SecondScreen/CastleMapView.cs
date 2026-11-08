using System.Diagnostics;
using Android.Content;
using Android.Graphics;
using Android.Views;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Hle;
using GameRuntime = RecompOne.Runtime.Runtime;

namespace SymphonyRecomp.Android.SecondScreen;

// Nearest-neighbor blit of the game's own 256x256, 4bpp map. The UI never reads
// game memory: a VSync listener publishes immutable snapshots from the game thread.
public sealed class CastleMapView : View
{
    readonly Paint _bitmapPaint = new() { FilterBitmap = false, AntiAlias = false };
    readonly Paint _markerPaint = new() { Color = Color.Yellow, AntiAlias = false };
    readonly Paint _textPaint = new() { Color = Color.LightGray, TextSize = 24, AntiAlias = true };
    readonly byte[] _flags = new byte[0x800];
    readonly ushort[] _map = new ushort[16384], _palette = new ushort[16];
    Bitmap? _bitmap;
    GearFrame? _frame;
    CastleMapFrame? _drawn;
    int _page, _touchTab = -1;
    float _uiScale = 1, _uiLeft, _uiTop;
    readonly Paint _chrome = new() { AntiAlias = true };
    readonly Typeface _titleFont = Typeface.Create("serif", TypefaceStyle.Bold)!;
    readonly Typeface _bodyFont = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    static readonly Color Gold = new(178, 143, 84), Ink = new(13, 12, 20), Muted = new(139, 133, 143);
    static readonly string[] Tabs = ["STATUS", "MAP", "JOURNAL"];
    long _lastCapture;
    bool _subscribed, _reportedError;
    bool? _reportedCastle;
    readonly MapViewport _viewport = new();
    float _dragX, _dragY, _pinchSpan;
    bool _mapDrag;
    int _mapControl = -1;
    float _mapScale;
    bool _needsMapFocus;

    public CastleMapView(Context context) : base(context) => SetBackgroundColor(Color.Black);

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        if (!_subscribed)
        {
            Event.AddListener<VSyncEvent>(Capture);
            _subscribed = true;
        }
    }

    protected override void OnDetachedFromWindow()
    {
        if (_subscribed) Event.RemoveListener<VSyncEvent>(Capture);
        _subscribed = false;
        base.OnDetachedFromWindow();
    }

    void Capture(VSyncEvent _)
    {
        long now = Stopwatch.GetTimestamp();
        if (now - _lastCapture < Stopwatch.Frequency / 10) return;
        _lastCapture = now;
        try
        {
            var mem = GameRuntime.Mem;
            var vram = GameRuntime.Gpu?.Vram;
            // The blueprint address is reused outside stages. Never interpret a
            // title/menu/movie buffer or an incomplete stage load as a castle map.
            if (mem == null || vram == null || mem.ReadU32(0x8003C734) != 2 ||
                mem.ReadU32(0x80073060) != 3 || mem.ReadU32(0x800974A0) == 0x1f)
            {
                Volatile.Write(ref _frame, null);
                return;
            }
            bool inverted = (mem.ReadU32(0x800974A0) & 0x20) != 0;
            for (int i = 0; i < _flags.Length; i++)
                _flags[i] = mem.ReadU8(CastleMapFrame.FlagsAddress + (uint)i);
            // HLE rendering owns the authoritative VRAM texture. The CPU shadow
            // is stale until StoreImage reads it back; use the supported GPU queue.
            GpuJobs.Run(() =>
            {
                var backend = GpuHle.Backend;
                if (GpuHle.Active && backend != null)
                {
                    backend.ReadVram(CastleMapFrame.VramX, CastleMapFrame.VramY,
                        CastleMapFrame.VramRowWords, CastleMapFrame.Size, _map);
                    backend.ReadVram(CastleMapFrame.PaletteX, CastleMapFrame.PaletteY, 16, 1, _palette);
                }
                else
                {
                    for (int y = 0; y < CastleMapFrame.Size; y++)
                        Array.Copy(vram, (CastleMapFrame.VramY + y) * 1024 + CastleMapFrame.VramX,
                            _map, y * CastleMapFrame.VramRowWords, CastleMapFrame.VramRowWords);
                    Array.Copy(vram, CastleMapFrame.PaletteY * 1024, _palette, 0, 16);
                }
            });
            int playerX = (int)mem.ReadU32(0x800730B0) + ((int)mem.ReadU32(0x800973F0) >> 8);
            int playerY = (int)mem.ReadU32(0x800730B4) + ((int)mem.ReadU32(0x800973F4) >> 8);
            var frame = CastleMapFrame.Decode(_flags, _map, _palette,
                inverted, playerX, playerY);
            var status = GearStatus.Read(mem.ReadU32, mem.ReadU8);
            Volatile.Write(ref _frame, new GearFrame(frame, status));
            if (_reportedCastle != inverted)
            {
                Console.WriteLine($"[MapV2] LIVE_BITMAP FLAGS=8006bb74 VRAM=832,256 CLUT=0,7 " +
                    $"inverted={inverted} explored={frame.Explored} known={frame.Known} " +
                    $"playerCell={playerX},{playerY} marker={frame.PlayerPixelX},{frame.PlayerPixelY} " +
                    $"cellState={CastleMapFrame.StateAt(_flags,playerX,playerY,inverted)} liveWords={_map.Count(v=>v!=0)} palette={string.Join(",", _palette.Select(v=>v.ToString("x4")))}");
                _reportedCastle = inverted;
            }
        }
        catch (Exception e)
        {
            Volatile.Write(ref _frame, null);
            if (!_reportedError) Console.Error.WriteLine($"[MapV2] snapshot unavailable: {e}");
            _reportedError = true;
        }
    }

    void Text(Canvas c, string text, float x, float y, float size, Color color, bool title = false)
    {
        _chrome.SetStyle(Paint.Style.Fill);
        _chrome.Color = color;
        _chrome.TextSize = size;
        _chrome.SetTypeface(title ? _titleFont : _bodyFont);
        c.DrawText(text, x, y, _chrome);
    }

    void Panel(Canvas c, float l, float t, float r, float b, Color fill, Color stroke)
    {
        _chrome.SetStyle(Paint.Style.Fill); _chrome.Color = fill;
        c.DrawRoundRect(l, t, r, b, 12, 12, _chrome);
        _chrome.SetStyle(Paint.Style.Stroke); _chrome.StrokeWidth = 2; _chrome.Color = stroke;
        c.DrawRoundRect(l, t, r, b, 12, 12, _chrome);
        _chrome.SetStyle(Paint.Style.Fill);
    }

    static string Nice(string value) => System.Text.RegularExpressions.Regex.Replace(value,
        "(?<=[a-z0-9])(?=[A-Z])", " ");

    string Area(GearFrame? frame) => frame == null ? "Awaiting your journey" :
        Nice(((Sotn.Stage)frame.Status.Stage).ToString()).Replace("First Visit", "");

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.DrawColor(Color.Black);
        // A fixed device-space layout; drawing AND touch use this single transform.
        _uiScale = MathF.Min(Width / 1000f, Height / 840f);
        if (_uiScale <= 0) return;
        _uiLeft = (Width - 1000 * _uiScale) / 2f;
        _uiTop = (Height - 840 * _uiScale) / 2f;
        canvas.Save(); canvas.Translate(_uiLeft, _uiTop); canvas.Scale(_uiScale, _uiScale);
        Panel(canvas, 10, 10, 990, 830, new Color(29, 18, 29), Gold);
        Panel(canvas, 22, 22, 978, 818, Ink, new Color(78, 58, 65));
        // Original geometric corner ornaments, not game artwork.
        _chrome.Color = Gold; _chrome.StrokeWidth = 2;
        foreach (float x in new[] { 42f, 958f })
        {
            canvas.DrawLine(x - 8, 42, x + 8, 42, _chrome);
            canvas.DrawLine(x, 34, x, 50, _chrome);
            canvas.DrawLine(x - 8, 792, x + 8, 792, _chrome);
        }
        Text(canvas, "CASTLE GEAR", 70, 61, 34, Gold, true);
        Text(canvas, DateTime.Now.ToString("ddd HH:mm"), 755, 59, 22, Color.LightGray);
        var frame = Volatile.Read(ref _frame);
        Text(canvas, Area(frame), 70, 97, 24, Color.LightGray);
        Text(canvas, frame == null ? "NO SIGNAL" : "LIVE / READ ONLY", 755, 95, 16, Muted);
        for (int i = 0; i < Tabs.Length; i++)
        {
            float x = 42 + i * 306;
            Panel(canvas, x, 122, x + 296, 182, i == _page ? new Color(65, 32, 47) : Ink,
                i == _page ? Gold : new Color(64, 49, 59));
            Text(canvas, Tabs[i], x + 92, 160, 24, i == _page ? Gold : Muted, true);
        }
        Panel(canvas, 42, 200, 958, 758, new Color(8, 9, 16), new Color(68, 58, 71));
        if (frame == null)
        {
            Text(canvas, "The castle awaits.", 250, 409, 36, Gold, true);
            Text(canvas, "Load a save or enter a stage on the upper screen.", 180, 457, 23, Muted);
            Text(canvas, "Map, status and journal follow the live game.", 202, 495, 22, Muted);
        }
        else if (_page == 0) DrawStatus(canvas, frame.Status);
        else if (_page == 1) DrawMap(canvas, frame.Map);
        else DrawJournal(canvas, frame);
        Text(canvas, "Touch a card to switch pages", 70, 794, 20, Muted);
        Text(canvas, "AYN THOR", 812, 794, 18, Gold, true);
        canvas.Restore();
    }

    void DrawMap(Canvas canvas, CastleMapFrame frame)
    {
        if (_needsMapFocus && frame.PlayerPixelX >= 0)
        { _viewport.FocusPlayer(frame.PlayerPixelX,frame.PlayerPixelY); _needsMapFocus = false; }
        _bitmap ??= Bitmap.CreateBitmap(256, 256, Bitmap.Config.Argb8888!)!;
        if (!ReferenceEquals(frame, _drawn))
        {
            _bitmap.SetPixels(frame.Pixels, 0, 256, 0, 0, 256, 256);
            _drawn = frame;
        }
        // Physical integer scaling, with the same pixel-space transform for marker.
        float physicalScale = MathF.Floor(448 * _uiScale / 256);
        if (physicalScale < 1) physicalScale = 448 * _uiScale / 256;
        float scale = physicalScale / _uiScale * _viewport.Zoom;
        _mapScale = scale;
        var origin = _viewport.Project(0,0,scale);
        float left = MathF.Round(_uiLeft + origin.X * _uiScale) / _uiScale - _uiLeft / _uiScale;
        float top = MathF.Round(_uiTop + origin.Y * _uiScale) / _uiScale - _uiTop / _uiScale;
        canvas.Save(); canvas.ClipRect(66, 260, 934, 710);
        canvas.DrawColor(Color.Black);
        canvas.DrawBitmap(_bitmap, null, new RectF(left, top, left + 256 * scale, top + 256 * scale), _bitmapPaint);
        if (frame.PlayerPixelX >= 0)
        {
            float x = left + (frame.PlayerPixelX + .5f) * scale;
            float y = top + (frame.PlayerPixelY + .5f) * scale;
            canvas.DrawRect(x - scale, y - scale, x + scale, y + scale, _markerPaint);
        }
        canvas.Restore();
        string[] controls = ["-", "FIT", "+"];
        for (int i = 0; i < 3; i++)
        {
            float x = 676 + i * 88;
            Panel(canvas, x, 208, x + 78, 250, Ink, Gold);
            Text(canvas, controls[i], x + (i == 1 ? 18 : 30), 237, 22, Gold, true);
        }
        Text(canvas, $"{_viewport.Zoom:0.#}x", 564, 236, 20, Muted);
        Text(canvas, frame.Inverted ? "INVERTED CASTLE" : "CASTLE", 66, 230, 16, Gold);
        Text(canvas, $"{frame.Explored} explored / {frame.Known} charted-only", 66, 736, 18, Muted);
        Text(canvas, "Drag / pinch / +/- to zoom. FIT resets.", 520, 736, 17, Muted);
    }

    void Meter(Canvas c, string label, int value, int max, float y, Color color)
    {
        Text(c, label, 74, y, 22, Muted);
        Text(c, $"{value} / {max}", 187, y, 28, Color.LightGray);
        _chrome.Color = new Color(35, 31, 42); c.DrawRect(74, y + 12, 430, y + 26, _chrome);
        _chrome.Color = color;
        c.DrawRect(74, y + 12, 74 + 356 * Math.Clamp(value / (float)Math.Max(1, max), 0, 1), y + 26, _chrome);
    }

    void DrawStatus(Canvas c, GearStatus s)
    {
        Text(c, "VITALS", 74, 248, 24, Gold, true);
        Meter(c, "HP", s.Hp, s.HpMax, 298, new Color(161, 55, 76));
        Meter(c, "MP", s.Mp, s.MpMax, 372, new Color(74, 100, 172));
        Meter(c, "HEARTS", s.Hearts, s.HeartsMax, 446, new Color(172, 93, 139));
        Text(c, $"LEVEL {s.Level}", 74, 540, 30, Gold, true);
        Text(c, $"EXP  {s.Experience:N0}", 74, 584, 24, Color.LightGray);
        Text(c, $"GOLD  {s.Gold:N0}", 74, 628, 24, Color.LightGray);
        Text(c, $"RELICS  {s.Relics} collected", 74, 684, 22, Muted);
        Text(c, "EQUIPMENT", 504, 248, 24, Gold, true);
        string[] slots = ["L HAND", "R HAND", "HEAD", "ARMOR", "CAPE", "RING I", "RING II"];
        for (int i = 0; i < s.Equipment.Length; i++)
        {
            float y = 296 + i * 60;
            string item = i < 2 ? ((Sotn.HandItem)s.Equipment[i]).ToString()
                : ((Sotn.BodyItem)s.Equipment[i]).ToString();
            Text(c, slots[i], 504, y, 16, Muted);
            Text(c, Nice(item), 504, y + 26, 23, Color.LightGray);
        }
    }

    void DrawJournal(Canvas c, GearFrame frame)
    {
        var s = frame.Status;
        Text(c, "CHRONICLE", 78, 254, 30, Gold, true);
        Text(c, "Time played", 78, 318, 23, Muted);
        Text(c, $"{s.Hours:D2}:{s.Minutes:D2}:{s.Seconds:D2}", 540, 318, 30, Color.LightGray);
        Text(c, "Rooms explored (both castles)", 78, 382, 23, Muted);
        Text(c, s.Rooms.ToString(), 740, 382, 30, Color.LightGray);
        Text(c, "Boss records (time attack)", 78, 446, 23, Muted);
        Text(c, $"{s.BossRecords} / 20", 740, 446, 30, Color.LightGray);
        Text(c, "Relics collected", 78, 510, 23, Muted);
        Text(c, s.Relics.ToString(), 740, 510, 30, Color.LightGray);
        Text(c, "Music track id", 78, 574, 23, Muted);
        Text(c, $"0x{s.Music:X2}", 740, 574, 30, Color.LightGray);
        Text(c, "Records exclude prologue Dracula and rescuing Richter.", 78, 646, 19, Muted);
        Text(c, "Map percentage and named music archive: planned.", 78, 689, 20, Muted);
    }

    int TabAt(float rawX, float rawY)
    {
        float x = (rawX - _uiLeft) / _uiScale, y = (rawY - _uiTop) / _uiScale;
        if (y < 122 || y > 182) return -1;
        for (int i = 0; i < Tabs.Length; i++) if (x >= 42 + i * 306 && x <= 338 + i * 306) return i;
        return -1;
    }

    int MapControlAt(float x, float y)
    {
        if (y < 208 || y > 250) return -1;
        for (int i = 0; i < 3; i++) if (x >= 676 + i * 88 && x <= 754 + i * 88) return i;
        return -1;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return false;
        float x = (e.GetX() - _uiLeft) / _uiScale, y = (e.GetY() - _uiTop) / _uiScale;
        if (e.ActionMasked == MotionEventActions.Down)
        {
            _touchTab = TabAt(e.GetX(), e.GetY());
            _mapControl = _page == 1 ? MapControlAt(x,y) : -1;
            _mapDrag = _page == 1 && x >= 66 && x <= 934 && y >= 260 && y <= 710;
            _dragX = x; _dragY = y; _pinchSpan = 0;
        }
        else if (_mapDrag && e.PointerCount >= 2)
        {
            float dx = e.GetX(0)-e.GetX(1), dy = e.GetY(0)-e.GetY(1);
            float span = MathF.Sqrt(dx*dx+dy*dy);
            if (_pinchSpan > 0 && span > 0) _viewport.ScaleBy(span/_pinchSpan);
            _pinchSpan = span; Invalidate();
        }
        else if (e.ActionMasked == MotionEventActions.Move && _mapDrag)
        {
            if (_pinchSpan > 0) { _pinchSpan = 0; _dragX=x; _dragY=y; }
            else if (_mapScale > 0) _viewport.PanBy(-(x-_dragX)/_mapScale, -(y-_dragY)/_mapScale);
            _dragX=x; _dragY=y; Invalidate();
        }
        else if (e.ActionMasked == MotionEventActions.Up)
        {
            int tab = TabAt(e.GetX(), e.GetY());
            if (tab >= 0 && tab == _touchTab)
            {
                _page = tab;
                if (tab == 1) _needsMapFocus = true;
                Console.WriteLine($"[CastleGear] PAGE {Tabs[tab]}");
                Invalidate(); PerformClick();
            }
            else if (_page == 1 && _mapControl >= 0 && MapControlAt(x,y) == _mapControl)
            {
                if (_mapControl == 1) _viewport.Reset();
                else _viewport.ScaleBy(_mapControl == 0 ? .5f : 2f);
                Console.WriteLine($"[CastleGear] MAP_ZOOM {_viewport.Zoom}x center={_viewport.CenterX},{_viewport.CenterY}");
                Invalidate(); PerformClick();
            }
            _touchTab = _mapControl = -1; _mapDrag = false; _pinchSpan = 0;
        }
        else if (e.ActionMasked == MotionEventActions.Cancel)
        { _touchTab = _mapControl = -1; _mapDrag = false; _pinchSpan = 0; }
        return true;
    }

    public override bool PerformClick() { base.PerformClick(); return true; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_subscribed) Event.RemoveListener<VSyncEvent>(Capture);
            _subscribed = false;
            _bitmap?.Recycle();
            _bitmap?.Dispose();
            _bitmapPaint.Dispose();
            _markerPaint.Dispose();
            _textPaint.Dispose();
            _chrome.Dispose();
            _titleFont.Dispose();
            _bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
