namespace SqueakyRatkin.Kernel;

/// <summary>
/// 内核选择链模式。SR 设置层的 <c>SqueakVoicePackMode</c> 只在适配层映射到此类型，
/// 使内核编译集不再依赖产品设置枚举。
/// </summary>
public enum SelectionMode
{
    Off,
    Fallback,
    Remix,
}
