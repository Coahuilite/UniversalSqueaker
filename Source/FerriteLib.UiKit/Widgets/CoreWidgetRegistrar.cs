namespace FerriteLib.UiKit.Widgets;

/// <summary>Registers the library's neutral core widgets in the shared registry's core scope.</summary>
public static class CoreWidgetRegistrar
{
    public static void RegisterAll()
    {
        WidgetRegistry.Register(WidgetRegistry.CoreScope, ChromeBannerWidget.Kind, () => new ChromeBannerWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, ChromeFooterWidget.Kind, () => new ChromeFooterWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, EmptyStateWidget.Kind, () => new EmptyStateWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, InputModeCardWidget.Kind, () => new InputModeCardWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, InputModeRowWidget.Kind, () => new InputModeRowWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, SectionHeaderWidget.Kind, () => new SectionHeaderWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, SliderNumberFieldWidget.Kind, () => new SliderNumberFieldWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, DropdownWidget.Kind, () => new DropdownWidget());
        WidgetRegistry.Register(WidgetRegistry.CoreScope, StepperSliderWidget.Kind, () => new StepperSliderWidget());
    }
}
