using Godot;

namespace IronDominion.Game;

internal sealed class HealthBar
{
    private const int Width = 64;
    private const int Height = 8;
    private static readonly Color TrackColor = new("172331");
    private static readonly Color FillColor = new("a9e66f");

    private readonly int _maxHp;
    private readonly Image _image;
    private readonly ImageTexture _texture;
    private int _lastHp = -1;

    public HealthBar(Node3D actor, int maxHp, float height, float pixelSize)
    {
        _maxHp = maxHp;
        _image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        _texture = ImageTexture.CreateFromImage(_image);

        actor.AddChild(new Sprite3D
        {
            Name = "HealthBar",
            Position = Vector3.Up * height,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Texture = _texture,
            PixelSize = pixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
        });

        SetHitPoints(maxHp);
    }

    public void SetHitPoints(int hitPoints)
    {
        if (_lastHp == hitPoints)
            return;

        _lastHp = hitPoints;
        _image.Fill(TrackColor);
        var fill = (int)Math.Ceiling((Width - 4) * ((double)hitPoints / _maxHp));
        if (fill > 0)
            _image.FillRect(new Rect2I(2, 2, fill, Height - 4), FillColor);

        _texture.Update(_image);
    }
}
