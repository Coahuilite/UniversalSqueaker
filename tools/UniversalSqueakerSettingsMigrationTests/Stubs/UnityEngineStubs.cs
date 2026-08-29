#nullable disable
namespace UnityEngine;

public struct Rect
{
    public float x;
    public float y;
    public float width;
    public float height;

    public Rect(float x, float y, float width, float height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}

public static class Mathf
{
    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    public static int Max(int a, int b) => a > b ? a : b;

    public static float Min(float a, float b) => a < b ? a : b;
}

public static class Time
{
    public static float realtimeSinceStartup;
}
