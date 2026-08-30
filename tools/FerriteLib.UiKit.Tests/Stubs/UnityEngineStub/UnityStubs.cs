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
    public float r;
    public float g;
    public float b;
    public float a;

    public Color(float r, float g, float b)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        a = 1f;
    }

    public Color(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public static Color white => new(1f, 1f, 1f, 1f);
}

public enum KeyCode
{
    None = 0,
    Return = 13,
    KeypadEnter = 271
}

public enum TextAnchor
{
    UpperLeft = 0,
    UpperCenter = 1,
    UpperRight = 2,
    MiddleLeft = 3,
    MiddleCenter = 4,
    MiddleRight = 5,
    LowerLeft = 6,
    LowerCenter = 7,
    LowerRight = 8
}
