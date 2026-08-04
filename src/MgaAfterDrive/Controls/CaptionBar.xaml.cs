using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MgaAfterDrive.Theme;

namespace MgaAfterDrive.Controls;

public partial class CaptionBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(CaptionBar),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    public event EventHandler? HideRequested;

    public CaptionBar()
    {
        InitializeComponent();
        Height = AppLayout.CaptionBarHeight;
        IconImage.Source = AppIcons.DefaultImage;
        TitleText.FontFamily = AppFonts.UIFamily;
        TitleText.FontSize = AppFonts.UISize;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CaptionBar bar)
        {
            bar.TitleText.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
        => HideRequested?.Invoke(this, EventArgs.Empty);

    private void CaptionArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && IsDescendantOf(source, HideButton))
        {
            return;
        }

        var window = Window.GetWindow(this);
        if (window is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            window.DragMove();
        }
        catch (InvalidOperationException)
        {
            // ボタン押下直後など DragMove 不能な場合は無視
        }
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}
