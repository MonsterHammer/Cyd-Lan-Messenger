using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace CydLan.Desktop;

/// <summary>Compact color swatch button that opens an interactive HSV color picker popup.</summary>
public sealed class ColorPickerButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty ColorHexProperty = DependencyProperty.Register(
        nameof(ColorHex), typeof(string), typeof(ColorPickerButton),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorHexChanged));

    public static readonly DependencyProperty FallbackBrushProperty = DependencyProperty.Register(
        nameof(FallbackBrush), typeof(Brush), typeof(ColorPickerButton),
        new PropertyMetadata(Brushes.Transparent, OnSwatchVisualChanged));

    public static readonly DependencyProperty EffectiveSwatchBrushProperty = DependencyProperty.Register(
        nameof(EffectiveSwatchBrush), typeof(Brush), typeof(ColorPickerButton),
        new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty HasCustomColorProperty = DependencyProperty.Register(
        nameof(HasCustomColor), typeof(bool), typeof(ColorPickerButton),
        new PropertyMetadata(false));

    public event EventHandler<string>? ColorChanged;

    public string ColorHex
    {
        get => (string)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public Brush FallbackBrush
    {
        get => (Brush)GetValue(FallbackBrushProperty);
        set => SetValue(FallbackBrushProperty, value);
    }

    public Brush EffectiveSwatchBrush
    {
        get => (Brush)GetValue(EffectiveSwatchBrushProperty);
        private set => SetValue(EffectiveSwatchBrushProperty, value);
    }

    public bool HasCustomColor
    {
        get => (bool)GetValue(HasCustomColorProperty);
        private set => SetValue(HasCustomColorProperty, value);
    }

    public ColorPickerButton()
    {
        Cursor = Cursors.Hand;
        Padding = new Thickness(0);
        MinWidth = 44;
        Height = 26;
        VerticalAlignment = VerticalAlignment.Center;
        ToolTip = "Pick color";
        SetResourceReference(FocusVisualStyleProperty, "FocusRing");
        Template = BuildTemplate();
        Click += (_, _) => SharedColorPickerPopup.ToggleFor(this);
        Unloaded += (_, _) => SharedColorPickerPopup.Detach(this);
        UpdateSwatchVisual();
    }

    internal void RaisePickedColor(string normalizedHex)
    {
        ColorHex = normalizedHex;
        ColorChanged?.Invoke(this, normalizedHex);
    }

    private static void OnColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton btn) btn.UpdateSwatchVisual();
    }

    private static void OnSwatchVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton btn) btn.UpdateSwatchVisual();
    }

    private void UpdateSwatchVisual()
    {
        if (AppearanceOptions.TryParseHex(ColorHex, out var color))
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            EffectiveSwatchBrush = brush;
            HasCustomColor = true;
        }
        else
        {
            EffectiveSwatchBrush = FallbackBrush ?? Brushes.Transparent;
            HasCustomColor = false;
        }
    }

    private static ControlTemplate BuildTemplate()
    {
        var template = new ControlTemplate(typeof(ColorPickerButton));

        var outer = new FrameworkElementFactory(typeof(Border));
        outer.Name = "Chrome";
        outer.SetResourceReference(Border.BackgroundProperty, "Surface");
        outer.SetResourceReference(Border.BorderBrushProperty, "Line");
        outer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        outer.SetResourceReference(Border.CornerRadiusProperty, "ControlCorner");
        outer.SetValue(Border.PaddingProperty, new Thickness(4, 3, 6, 3));

        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var swatch = new FrameworkElementFactory(typeof(Border));
        swatch.SetValue(FrameworkElement.WidthProperty, 20.0);
        swatch.SetValue(FrameworkElement.HeightProperty, 18.0);
        swatch.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        swatch.SetResourceReference(Border.BorderBrushProperty, "Line");
        swatch.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        swatch.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(EffectiveSwatchBrushProperty));

        var chevron = new FrameworkElementFactory(typeof(Glyph));
        chevron.SetResourceReference(Glyph.DataProperty, "IconChevronDown");
        chevron.SetValue(Glyph.SizeProperty, 10.0);
        chevron.SetValue(Glyph.StrokeThicknessProperty, 1.9);
        chevron.SetResourceReference(Glyph.ForegroundProperty, "Muted");
        chevron.SetValue(FrameworkElement.MarginProperty, new Thickness(5, 0, 0, 0));
        chevron.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        row.AppendChild(swatch);
        row.AppendChild(chevron);
        outer.AppendChild(row);
        template.VisualTree = outer;

        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        var hoverBrush = new DynamicResourceExtension("Muted");
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, hoverBrush, "Chrome"));
        template.Triggers.Add(hover);

        return template;
    }
}

/// <summary>Shared interactive HSV popup shared across all ColorPickerButton instances.</summary>
internal sealed class SharedColorPickerPopup
{
    private const double PadWidth = 196;
    private const double PadHeight = 144;
    private const double HueWidth = 18;

    private static SharedColorPickerPopup? instance;

    internal static bool IsOpen => instance?.popup.IsOpen == true;
    internal static FrameworkElement? ActiveCard => instance?.popup.Child as FrameworkElement;

    private readonly Popup popup;
    private readonly Grid svPad;
    private readonly Border svHueLayer;
    private readonly Ellipse svThumb;
    private readonly Grid hueBar;
    private readonly Border hueThumb;
    private readonly Border previewSwatch;
    private readonly TextBox hexBox;
    private readonly TextBox rBox;
    private readonly TextBox gBox;
    private readonly TextBox bBox;

    private ColorPickerButton? activeTarget;
    private ColorPickerButton? lastClosedTarget;
    private DateTime lastClosedUtc = DateTime.MinValue;
    private double hue;
    private double saturation = 1;
    private double value = 1;
    private bool draggingSv;
    private bool draggingHue;
    private bool syncingControls;

    private SharedColorPickerPopup()
    {
        svHueLayer = new Border { CornerRadius = new CornerRadius(6), Background = Brushes.Red };

        var whiteGrad = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), new Point(0, 0.5), new Point(1, 0.5))
        };
        var blackGrad = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0.5, 0), new Point(0.5, 1))
        };
        var padBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1)
        };
        padBorder.SetResourceReference(Border.BorderBrushProperty, "Line");

        svThumb = new Ellipse
        {
            Width = 12,
            Height = 12,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = .7 }
        };
        var thumbCanvas = new Canvas { IsHitTestVisible = false };
        thumbCanvas.Children.Add(svThumb);

        svPad = new Grid
        {
            Width = PadWidth,
            Height = PadHeight,
            Cursor = Cursors.Cross,
            ClipToBounds = true
        };
        svPad.Children.Add(svHueLayer);
        svPad.Children.Add(whiteGrad);
        svPad.Children.Add(blackGrad);
        svPad.Children.Add(padBorder);
        svPad.Children.Add(thumbCanvas);
        svPad.MouseLeftButtonDown += SvPad_MouseDown;
        svPad.MouseMove += SvPad_MouseMove;
        svPad.MouseLeftButtonUp += SvPad_MouseUp;

        var hueBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops =
            [
                new GradientStop(Color.FromRgb(255, 0, 0), 0.0),
                new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6.0),
                new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6.0),
                new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6.0),
                new GradientStop(Color.FromRgb(255, 0, 0), 1.0)
            ]
        };
        hueBrush.Freeze();

        var hueFill = new Border
        {
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Background = hueBrush
        };
        hueFill.SetResourceReference(Border.BorderBrushProperty, "Line");

        hueThumb = new Border
        {
            Width = HueWidth + 4,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false
        };
        var hueCanvas = new Canvas { IsHitTestVisible = false };
        hueCanvas.Children.Add(hueThumb);

        hueBar = new Grid
        {
            Width = HueWidth,
            Height = PadHeight,
            Margin = new Thickness(10, 0, 0, 0),
            Cursor = Cursors.SizeNS
        };
        hueBar.Children.Add(hueFill);
        hueBar.Children.Add(hueCanvas);
        hueBar.MouseLeftButtonDown += HueBar_MouseDown;
        hueBar.MouseMove += HueBar_MouseMove;
        hueBar.MouseLeftButtonUp += HueBar_MouseUp;

        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        topRow.Children.Add(svPad);
        topRow.Children.Add(hueBar);

        previewSwatch = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };
        previewSwatch.SetResourceReference(Border.BorderBrushProperty, "Line");

        hexBox = new TextBox
        {
            Width = 82,
            MaxLength = 9,
            Padding = new Thickness(7, 4, 7, 4),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        hexBox.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Color picker hex");
        hexBox.KeyDown += HexBox_KeyDown;
        hexBox.LostKeyboardFocus += (_, _) => CommitHexBox();

        rBox = BuildChannelBox("Red", "R");
        gBox = BuildChannelBox("Green", "G");
        bBox = BuildChannelBox("Blue", "B");

        var rgbGrid = new UniformGrid { Columns = 3, Margin = new Thickness(8, 0, 0, 0), Width = 104 };
        rgbGrid.Children.Add(WrapChannel("R", rBox));
        rgbGrid.Children.Add(WrapChannel("G", gBox));
        rgbGrid.Children.Add(WrapChannel("B", bBox));

        var valueRow = new DockPanel { Margin = new Thickness(0, 10, 0, 10) };
        DockPanel.SetDock(previewSwatch, Dock.Left);
        DockPanel.SetDock(rgbGrid, Dock.Right);
        valueRow.Children.Add(previewSwatch);
        valueRow.Children.Add(rgbGrid);
        valueRow.Children.Add(hexBox);

        var popupInstance = new Popup
        {
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true
        };
        popup = popupInstance;

        var resetButton = new System.Windows.Controls.Button
        {
            Content = "Theme",
            Padding = new Thickness(10, 5, 10, 5),
            FontSize = 11.5
        };
        resetButton.SetResourceReference(FrameworkElement.StyleProperty, "Secondary");
        resetButton.Click += (_, _) =>
        {
            activeTarget?.RaisePickedColor("");
            popupInstance.IsOpen = false;
        };

        var doneButton = new System.Windows.Controls.Button
        {
            Content = "Done",
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 11.5
        };
        doneButton.SetResourceReference(FrameworkElement.StyleProperty, "Primary");
        doneButton.Click += (_, _) => popupInstance.IsOpen = false;

        var actionsRow = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(resetButton, Dock.Left);
        DockPanel.SetDock(doneButton, Dock.Right);
        actionsRow.Children.Add(resetButton);
        actionsRow.Children.Add(doneButton);

        var content = new StackPanel();
        content.Children.Add(topRow);
        content.Children.Add(valueRow);
        content.Children.Add(actionsRow);

        var card = new Border
        {
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4, 4, 8, 8),
            Child = content,
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Direction = 270, Opacity = 0.22 }
        };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        card.SetResourceReference(Border.CornerRadiusProperty, "PanelCorner");

        popupInstance.Child = card;
        popupInstance.Closed += (_, _) =>
        {
            draggingSv = false;
            draggingHue = false;
            lastClosedTarget = activeTarget;
            lastClosedUtc = DateTime.UtcNow;
            activeTarget = null;
        };
    }

    public static void ToggleFor(ColorPickerButton target)
    {
        instance ??= new SharedColorPickerPopup();
        if (instance.popup.IsOpen && ReferenceEquals(instance.activeTarget, target))
        {
            instance.popup.IsOpen = false;
            return;
        }
        if (ReferenceEquals(instance.lastClosedTarget, target) && (DateTime.UtcNow - instance.lastClosedUtc).TotalMilliseconds < 180)
        {
            instance.lastClosedTarget = null;
            return;
        }
        instance.Open(target);
    }

    public static void Detach(ColorPickerButton target)
    {
        if (instance is null) return;
        if (ReferenceEquals(instance.activeTarget, target)) instance.popup.IsOpen = false;
        if (ReferenceEquals(instance.lastClosedTarget, target)) instance.lastClosedTarget = null;
    }

    private void Open(ColorPickerButton target)
    {
        activeTarget = target;
        popup.PlacementTarget = target;

        Color initial = ResolveInitialColor(target);
        RgbToHsv(initial, out hue, out saturation, out value);
        UpdateVisualsFromHsv(emit: false);
        popup.IsOpen = true;
    }

    private static Color ResolveInitialColor(ColorPickerButton target)
    {
        if (AppearanceOptions.TryParseHex(target.ColorHex, out var c)) return c;
        if (target.FallbackBrush is SolidColorBrush sb && sb.Color.A > 0) return sb.Color;
        return Color.FromRgb(0x3B, 0x82, 0xF6);
    }

    private TextBox BuildChannelBox(string label, string tag)
    {
        var box = new TextBox
        {
            MaxLength = 3,
            Padding = new Thickness(3, 3, 3, 3),
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            Tag = tag
        };
        box.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
        box.KeyDown += RgbBox_KeyDown;
        box.LostKeyboardFocus += (_, _) => CommitRgbBoxes();
        return box;
    }

    private static StackPanel WrapChannel(string title, TextBox box)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 0, 2, 0) };
        panel.Children.Add(box);
        var caption = new TextBlock
        {
            Text = title,
            FontSize = 9.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        panel.Children.Add(caption);
        return panel;
    }

    private void SvPad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        draggingSv = true;
        svPad.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(svPad));
        e.Handled = true;
    }

    private void SvPad_MouseMove(object sender, MouseEventArgs e)
    {
        if (!draggingSv) return;
        UpdateSvFromPoint(e.GetPosition(svPad));
    }

    private void SvPad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!draggingSv) return;
        draggingSv = false;
        svPad.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateSvFromPoint(Point p)
    {
        saturation = Math.Clamp(p.X / PadWidth, 0, 1);
        value = Math.Clamp(1.0 - p.Y / PadHeight, 0, 1);
        UpdateVisualsFromHsv(emit: true);
    }

    private void HueBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        draggingHue = true;
        hueBar.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(hueBar));
        e.Handled = true;
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!draggingHue) return;
        UpdateHueFromPoint(e.GetPosition(hueBar));
    }

    private void HueBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!draggingHue) return;
        draggingHue = false;
        hueBar.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateHueFromPoint(Point p)
    {
        hue = Math.Clamp(p.Y / PadHeight, 0, 1) * 360.0;
        UpdateVisualsFromHsv(emit: true);
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitHexBox();
        e.Handled = true;
    }

    private void CommitHexBox()
    {
        if (syncingControls) return;
        if (AppearanceOptions.TryParseHex(hexBox.Text, out var color))
        {
            RgbToHsv(color, out hue, out saturation, out value);
            UpdateVisualsFromHsv(emit: true);
        }
        else
        {
            UpdateVisualsFromHsv(emit: false);
        }
    }

    private void RgbBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitRgbBoxes();
        e.Handled = true;
    }

    private void CommitRgbBoxes()
    {
        if (syncingControls) return;
        if (int.TryParse(rBox.Text.Trim(), out int r) &&
            int.TryParse(gBox.Text.Trim(), out int g) &&
            int.TryParse(bBox.Text.Trim(), out int b))
        {
            var color = Color.FromRgb(
                (byte)Math.Clamp(r, 0, 255),
                (byte)Math.Clamp(g, 0, 255),
                (byte)Math.Clamp(b, 0, 255));
            RgbToHsv(color, out hue, out saturation, out value);
            UpdateVisualsFromHsv(emit: true);
        }
        else
        {
            UpdateVisualsFromHsv(emit: false);
        }
    }

    private void UpdateVisualsFromHsv(bool emit)
    {
        syncingControls = true;
        Color pureHue = HsvToRgb(hue, 1, 1);
        var hueBrush = new SolidColorBrush(pureHue);
        hueBrush.Freeze();
        svHueLayer.Background = hueBrush;

        Color current = HsvToRgb(hue, saturation, value);
        var currentBrush = new SolidColorBrush(current);
        currentBrush.Freeze();
        previewSwatch.Background = currentBrush;

        Canvas.SetLeft(svThumb, saturation * PadWidth - svThumb.Width / 2);
        Canvas.SetTop(svThumb, (1.0 - value) * PadHeight - svThumb.Height / 2);
        Canvas.SetLeft(hueThumb, -2);
        Canvas.SetTop(hueThumb, Math.Clamp(hue / 360.0, 0, 1) * PadHeight - hueThumb.Height / 2);

        string hex = $"#{current.R:X2}{current.G:X2}{current.B:X2}";
        hexBox.Text = hex;
        rBox.Text = current.R.ToString();
        gBox.Text = current.G.ToString();
        bBox.Text = current.B.ToString();
        syncingControls = false;

        if (emit) activeTarget?.RaisePickedColor(hex);
    }

    internal static void RgbToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;

        v = max;
        s = max <= 1e-6 ? 0 : d / max;
        if (d <= 1e-6)
        {
            h = 0;
            return;
        }
        double rawH = max == r ? (g - b) / d + (g < b ? 6 : 0)
            : max == g ? (b - r) / d + 2
            : (r - g) / d + 4;
        h = (rawH * 60.0) % 360.0;
        if (h < 0) h += 360.0;
    }

    internal static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double c = v * s;
        double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        double m = v - c;
        double r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);

        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round((r + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((g + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((b + m) * 255), 0, 255));
    }
}
