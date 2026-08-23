namespace UniversalSqueaker.Kernel;

/// <summary>
/// 内核选择链模式。适配层设置枚举只在适配层映射到此类型，
/// 使内核编译集不依赖产品设置枚举。
/// </summary>
public enum SelectionMode
{
    Off,
    Fallback,
    Remix,
}
