using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
namespace WeatherWidget;
internal static class UiChrome
{
    private static readonly ControlTemplate FlatTemplate = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}" CornerRadius="2">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </Border>
          <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" Value="0.8"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="BorderBrush" Value="White"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    private static readonly ControlTemplate GripTemplate = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Thumb">
          <Grid Background="Transparent"><Path Data="M2,10 L10,2 M6,10 L10,6" Stroke="White" StrokeThickness="1" Opacity="0.7"/></Grid>
        </ControlTemplate>
        """);
    public static Button FlatButton(string text, string tooltip, RoutedEventHandler handler)
    {
        var button = new Button { Content=text, ToolTip=tooltip, Template=FlatTemplate, Foreground=Brushes.White,
            Background=new SolidColorBrush(Color.FromArgb(50, 8, 23, 36)), BorderBrush=new SolidColorBrush(Color.FromArgb(75,255,255,255)),
            BorderThickness=new Thickness(1), Padding=new Thickness(4,1,4,1), FontSize=9 };
        System.Windows.Automation.AutomationProperties.SetName(button,tooltip); button.Click+=handler; return button;
    }
    public static Thumb ResizeGrip(string tooltip)
    {
        var thumb = new Thumb { Width=12, Height=12, Cursor=Cursors.SizeNWSE, ToolTip=tooltip, Template=GripTemplate };
        System.Windows.Automation.AutomationProperties.SetName(thumb,tooltip); return thumb;
    }
    public static bool IsInteractiveSource(DependencyObject? node)
    {
        while(node != null)
        {
            if(node is ButtonBase or Thumb) return true;
            node=node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
}
