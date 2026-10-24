using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

internal static class BindingProxyTests
{
    /// <summary>A full forwarding access surface; registration remains on its current concrete backing.</summary>
    private sealed class Proxy : IUiBindingAccess
    {
        internal UiBindings Backing = null!;
        public UiBindings BindingOwner => Backing.BindingOwner;
        public T Get<T>(string binding) => Backing.Get<T>(binding);
        public bool TryGet<T>(string binding, out T value) => Backing.TryGet(binding, out value);
        public bool TryGetBool(string key, out bool value) => Backing.TryGetBool(key, out value);
        public void Set<T>(string binding, T value) => Backing.Set(binding, value);
        public bool IsWritable(string binding) => Backing.IsWritable(binding);
        public IReadOnlyList<T> GetOptions<T>(string binding) => Backing.GetOptions<T>(binding);
        public void Invoke<T>(string actionBind, T payload) => Backing.Invoke(actionBind, payload);
        public void Invoke(string actionBind) => Backing.Invoke(actionBind);
        public bool CanExecute(string actionBind) => Backing.CanExecute(actionBind);
        public bool TryInvokeCommand(string actionBind) => Backing.TryInvokeCommand(actionBind);
        public int GetRevision(string key) => Backing.GetRevision(key);
        public UiInvalidation GetInvalidation(string key) => Backing.GetInvalidation(key);
        public void ValidateValue<T>(string binding, string path) => Backing.ValidateValue<T>(binding, path);
        public void ValidateOptions<T>(string binding, string path) => Backing.ValidateOptions<T>(binding, path);
        public void ValidateAction<T>(string actionBind, string path) => Backing.ValidateAction<T>(actionBind, path);
        public void ValidateCommand(string actionBind, string path) => Backing.ValidateCommand(actionBind, path);
        public bool TryValue(string key, out UiValueEndpoint? endpoint) => Backing.TryValue(key, out endpoint);
        public UiValueEndpoint Value(string key) => Backing.Value(key);
        public UiChoiceEndpoint Choices(string key) => Backing.Choices(key);
    }
    internal static void Run()
    {
        UsKernelWidgetRegistrar.EnsureRegistered(); // The existing lane also runs independently.
        var a = new UiBindings(); var b = new UiBindings();
        SqueakVoicePackMode currentA = SqueakVoicePackMode.Vanilla, currentB = SqueakVoicePackMode.Disabled;
        a.BindValue("mode", () => currentA, value => currentA = value);
        b.BindValue("mode", () => currentB, value => currentB = value);
        a.BindChoices<int>("choices", () => new[] { new UiChoice<int>("A", 7) });
        b.BindChoices<int>("choices", () => new[] { new UiChoice<int>("B", 13) });
        var proxy = new Proxy { Backing = a };
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"coahuilite.universalsqueaker\"><Widget Id=\"mode\" Kind=\"us/mode-row\" Bind=\"mode\"/></UiPage>");
        using var page = new Program.Page(new UiHost(Program.Scope, manifest, proxy, UsTheme.Surface(), Program.Metrics, new Program.StubTranslation()));
        var view = new UiLayoutRect(0, 0, 600, 100);
        page.Draw(view);
        Program.Check(ReferenceEquals(page.Host.Bindings, proxy) && ReferenceEquals(proxy.BindingOwner, a), "actual US widget validates full proxy; ordinary getters need no tracked registration");
        UiValueEndpoint retainedA = proxy.Value("mode");
        Program.Check(retainedA.Read<SqueakVoicePackMode>() == currentA && proxy.Choices("choices").ValueType == typeof(int)
            && (int)proxy.Choices("choices").Read()[0].Value! == 7, "value/choice endpoint capability reads same A backing");
        proxy.Backing = b; page.Host.Invalidate(); page.Draw(view);
        Program.Check(ReferenceEquals(proxy.BindingOwner, b) && proxy.Value("mode").Read<SqueakVoicePackMode>() == currentB
            && (int)proxy.Choices("choices").Read()[0].Value! == 13, "same-Host backing switch forwards current owner/endpoints together");
        proxy.Set("mode", SqueakVoicePackMode.Fallback); page.Draw(view);
        Program.Check(currentA == SqueakVoicePackMode.Vanilla && currentB == SqueakVoicePackMode.Fallback, "actual access write targets current backing only");
        Program.Check(retainedA.TryWrite(SqueakVoicePackMode.Remix) && currentA == SqueakVoicePackMode.Remix
            && currentB == SqueakVoicePackMode.Fallback, "previously retained endpoint still belongs to A after proxy switch");
    }
}
