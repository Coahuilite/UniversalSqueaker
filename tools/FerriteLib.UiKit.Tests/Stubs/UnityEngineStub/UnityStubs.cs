namespace UnityEngine;

/// <summary>Executable stub for the Unity types the UiKit test paths touch at runtime.</summary>
public struct Vector2
{
    public float x;
    public float y;

    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public struct Rect
{
    private float _x;
    private float _y;
    private float _width;
    private float _height;

    public Rect(float x, float y, float width, float height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public float x
    {
        get => _x;
        set => _x = value;
    }

    public float y
    {
        get => _y;
        set => _y = value;
    }

    public float width
    {
        get => _width;
        set => _width = value;
    }

    public float height
    {
        get => _height;
        set => _height = value;
    }

    public float xMax => _x + _width;

    public float yMax => _y + _height;
}

public struct Color
{
    private float _r;
    private float _g;
    private float _b;
    private float _a;

    public Color(float r, float g, float b)
    {
        _r = r;
        _g = g;
        _b = b;
        _a = 1f;
    }

    public Color(float r, float g, float b, float a)
    {
        _r = r;
        _g = g;
        _b = b;
        _a = a;
    }

    public float r
    {
        get => _r;
        set => _r = value;
    }

    public float g
    {
        get => _g;
        set => _g = value;
    }

    public float b
    {
        get => _b;
        set => _b = value;
    }

    public float a
    {
        get => _a;
        set => _a = value;
    }

    public static Color white => new(1f, 1f, 1f, 1f);
}
