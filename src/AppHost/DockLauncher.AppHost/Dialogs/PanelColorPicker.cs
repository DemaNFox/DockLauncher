using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DockLauncher.AppHost.Configuration;
using DockLauncher.BuildingBlocks.Application.Contracts;

namespace DockLauncher.AppHost.Dialogs;

public sealed class PanelColorPicker : IPanelColorPicker
{
    public Task<string?> PickColorAsync(string? currentColor, CancellationToken cancellationToken = default)
    {
        var dialog = new PanelColorPickerWindow(ParseColor(currentColor))
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
        };

        var result = dialog.ShowDialog();
        return Task.FromResult(result == true ? dialog.SelectedHexColor : null);
    }

    private static Color ParseColor(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = value.Trim().TrimStart('#');
            if (normalized.Length == 6
                && int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
            }

            if (normalized.Length == 8
                && int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                return Color.FromArgb(
                    (byte)((argb >> 24) & 0xFF),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
            }
        }

        return Color.FromRgb(27, 38, 55);
    }
}

internal sealed class PanelColorPickerWindow : Window
{
    private const double SpectrumSize = 232;
    private const double SliderHeight = 232;
    private const double SliderWidth = 22;

    private readonly Border _previewFill;
    private readonly Border _spectrumBase;
    private readonly Canvas _spectrumOverlay;
    private readonly Ellipse _spectrumMarker;
    private readonly Canvas _hueOverlay;
    private readonly Canvas _alphaOverlay;
    private readonly Rectangle _hueMarker;
    private readonly Rectangle _alphaMarker;
    private readonly Rectangle _alphaGradient;
    private readonly TextBox _hexTextBox;
    private readonly TextBox _alphaTextBox;
    private readonly TextBox _redTextBox;
    private readonly TextBox _greenTextBox;
    private readonly TextBox _blueTextBox;
    private readonly TextBlock _rgbSummary;

    private bool _isSynchronizing;
    private double _hue;
    private double _saturation;
    private double _value;
    private byte _alpha;

    public PanelColorPickerWindow(Color initialColor)
    {
        Title = "Pick Panel Color";
        Width = 720;
        Height = 560;
        MinWidth = 700;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        Foreground = Brushes.White;
        WindowDisplayPolicy.Apply(this, new WindowDisplayPolicyOptions(RecenterOnLoad: true));

        var rootBorder = new Border
        {
            Padding = new Thickness(22),
            Background = new LinearGradientBrush(
                Color.FromRgb(15, 23, 42),
                Color.FromRgb(17, 24, 39),
                new Point(0, 0),
                new Point(1, 1))
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(CreateHeader());

        var contentGrid = new Grid
        {
            Margin = new Thickness(0, 18, 0, 0)
        };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300, GridUnitType.Star) });

        var spectrumPanel = CreateSpectrumPanel(out _spectrumBase, out _spectrumOverlay, out _spectrumMarker);
        contentGrid.Children.Add(spectrumPanel);

        var sidePanel = CreateSidePanel(
            out _previewFill,
            out _hueOverlay,
            out _alphaOverlay,
            out _hueMarker,
            out _alphaMarker,
            out _alphaGradient,
            out _hexTextBox,
            out _alphaTextBox,
            out _redTextBox,
            out _greenTextBox,
            out _blueTextBox,
            out _rgbSummary);
        Grid.SetColumn(sidePanel, 2);
        contentGrid.Children.Add(sidePanel);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        var footer = CreateFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        rootBorder.Child = root;
        Content = rootBorder;

        HookEvents();
        SetColor(initialColor);
    }

    public string SelectedHexColor => ToHex(CurrentColor);

    private Color CurrentColor
    {
        get
        {
            var rgb = ColorFromHsv(_hue, _saturation, _value);
            return Color.FromArgb(_alpha, rgb.R, rgb.G, rgb.B);
        }
    }

    private UIElement CreateHeader()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Panel Color",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.76,
            Text = "Hue, alpha and exact values in a compact picker.",
            TextWrapping = TextWrapping.Wrap
        });
        return stack;
    }

    private UIElement CreateSpectrumPanel(out Border spectrumBase, out Canvas spectrumOverlay, out Ellipse spectrumMarker)
    {
        spectrumBase = new Border
        {
            Width = SpectrumSize,
            Height = SpectrumSize,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Colors.Red)
        };

        var whiteOverlay = new Rectangle
        {
            RadiusX = 18,
            RadiusY = 18,
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Colors.White, 0),
                    new(Color.FromArgb(0, 255, 255, 255), 1)
                },
                new Point(0, 0.5),
                new Point(1, 0.5))
        };

        var blackOverlay = new Rectangle
        {
            RadiusX = 18,
            RadiusY = 18,
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(0, 0, 0, 0), 0),
                    new(Colors.Black, 1)
                },
                new Point(0.5, 0),
                new Point(0.5, 1))
        };

        spectrumMarker = new Ellipse
        {
            Width = 16,
            Height = 16,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Color = Colors.Black,
                Opacity = 0.45
            }
        };

        spectrumOverlay = new Canvas
        {
            Width = SpectrumSize,
            Height = SpectrumSize,
            Background = Brushes.Transparent
        };
        spectrumOverlay.Children.Add(spectrumMarker);

        var grid = new Grid
        {
            Width = SpectrumSize,
            Height = SpectrumSize
        };
        grid.Children.Add(spectrumBase);
        grid.Children.Add(whiteOverlay);
        grid.Children.Add(blackOverlay);
        grid.Children.Add(spectrumOverlay);

        var wrapper = new StackPanel();
        wrapper.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
            Text = "Spectrum",
            Opacity = 0.82,
            FontWeight = FontWeights.SemiBold
        });
        wrapper.Children.Add(new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(22),
            Background = new SolidColorBrush(Color.FromArgb(70, 17, 30, 46)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 80, 101, 125)),
            BorderThickness = new Thickness(1),
            Child = grid
        });
        return wrapper;
    }

    private UIElement CreateSidePanel(
        out Border previewFill,
        out Canvas hueOverlay,
        out Canvas alphaOverlay,
        out Rectangle hueMarker,
        out Rectangle alphaMarker,
        out Rectangle alphaGradient,
        out TextBox hexTextBox,
        out TextBox alphaTextBox,
        out TextBox redTextBox,
        out TextBox greenTextBox,
        out TextBox blueTextBox,
        out TextBlock rgbSummary)
    {
        var panel = new StackPanel
        {
            Width = 300
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Preview",
            Opacity = 0.82,
            FontWeight = FontWeights.SemiBold
        });

        previewFill = new Border
        {
            Height = 26,
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 248, 250, 252))
        };

        panel.Children.Add(new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(18),
            Background = CreateCheckerBrush(),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 80, 101, 125)),
            BorderThickness = new Thickness(1),
            Child = previewFill
        });

        rgbSummary = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.72
        };
        panel.Children.Add(rgbSummary);

        var controlsGrid = new Grid
        {
            Margin = new Thickness(0, 18, 0, 0)
        };
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var huePanel = CreateVerticalSlider("Hue", CreateHueBrush(), out hueOverlay, out hueMarker);
        controlsGrid.Children.Add(huePanel);

        var alphaPanel = CreateVerticalSlider("Alpha", CreateCheckerBrush(), out alphaOverlay, out alphaMarker, out alphaGradient);
        Grid.SetColumn(alphaPanel, 1);
        controlsGrid.Children.Add(alphaPanel);

        var valuesPanel = new StackPanel
        {
            Margin = new Thickness(18, 0, 0, 0)
        };
        valuesPanel.Children.Add(new TextBlock
        {
            Text = "Values",
            Opacity = 0.82,
            FontWeight = FontWeights.SemiBold
        });

        hexTextBox = CreateValueTextBox();
        alphaTextBox = CreateValueTextBox();
        redTextBox = CreateValueTextBox();
        greenTextBox = CreateValueTextBox();
        blueTextBox = CreateValueTextBox();

        valuesPanel.Children.Add(CreateField("Hex", hexTextBox, new Thickness(0, 10, 0, 0)));
        valuesPanel.Children.Add(CreateField("Alpha", alphaTextBox, new Thickness(0, 10, 0, 0)));

        var rgbGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        rgbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rgbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        valuesPanel.Children.Add(rgbGrid);

        var redField = CreateField("Red", redTextBox);
        rgbGrid.Children.Add(redField);
        var greenField = CreateField("Green", greenTextBox, new Thickness(10, 0, 0, 0));
        Grid.SetColumn(greenField, 1);
        rgbGrid.Children.Add(greenField);

        valuesPanel.Children.Add(CreateField("Blue", blueTextBox, new Thickness(0, 10, 0, 0)));

        Grid.SetColumn(valuesPanel, 2);
        controlsGrid.Children.Add(valuesPanel);

        panel.Children.Add(controlsGrid);
        return panel;
    }

    private UIElement CreateVerticalSlider(string label, Brush baseBrush, out Canvas overlay, out Rectangle marker)
    {
        return CreateVerticalSlider(label, baseBrush, out overlay, out marker, out _);
    }

    private UIElement CreateVerticalSlider(string label, Brush baseBrush, out Canvas overlay, out Rectangle marker, out Rectangle gradientOverlay)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            Text = label,
            Opacity = 0.74
        });

        var baseRectangle = new Border
        {
            Width = SliderWidth,
            Height = SliderHeight,
            CornerRadius = new CornerRadius(11),
            Background = baseBrush
        };

        gradientOverlay = new Rectangle
        {
            Width = SliderWidth,
            Height = SliderHeight,
            RadiusX = 11,
            RadiusY = 11,
            Fill = Brushes.Transparent
        };

        marker = new Rectangle
        {
            Width = SliderWidth + 8,
            Height = 4,
            RadiusX = 2,
            RadiusY = 2,
            Fill = Brushes.White
        };

        overlay = new Canvas
        {
            Width = SliderWidth + 8,
            Height = SliderHeight,
            Margin = new Thickness(-4, 0, 0, 0),
            Background = Brushes.Transparent
        };
        overlay.Children.Add(marker);

        var grid = new Grid
        {
            Width = SliderWidth + 8,
            Height = SliderHeight
        };
        grid.Children.Add(new Border
        {
            Width = SliderWidth,
            Height = SliderHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Grid
            {
                Children =
                {
                    baseRectangle,
                    gradientOverlay
                }
            }
        });
        grid.Children.Add(overlay);

        panel.Children.Add(grid);
        return panel;
    }

    private UIElement CreateField(string label, TextBox textBox, Thickness? margin = null)
    {
        var panel = new StackPanel
        {
            Margin = margin ?? default
        };
        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 6),
            Text = label,
            Opacity = 0.74
        });
        panel.Children.Add(textBox);
        return panel;
    }

    private UIElement CreateFooter()
    {
        var grid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.68,
            Text = "Color alpha is separate from overall panel opacity."
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = CreateActionButton("Cancel", false);
        cancelButton.IsCancel = true;
        cancelButton.Click += (_, _) => DialogResult = false;

        var okButton = CreateActionButton("Use Color", true);
        okButton.IsDefault = true;
        okButton.Click += (_, _) => DialogResult = true;

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        return grid;
    }

    private void HookEvents()
    {
        _spectrumOverlay.MouseLeftButtonDown += (_, e) => UpdateSpectrumFromMouse(e.GetPosition(_spectrumOverlay));
        _spectrumOverlay.MouseMove += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSpectrumFromMouse(e.GetPosition(_spectrumOverlay));
            }
        };

        _hueOverlay.MouseLeftButtonDown += (_, e) => UpdateHueFromMouse(e.GetPosition(_hueOverlay));
        _hueOverlay.MouseMove += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateHueFromMouse(e.GetPosition(_hueOverlay));
            }
        };

        _alphaOverlay.MouseLeftButtonDown += (_, e) => UpdateAlphaFromMouse(e.GetPosition(_alphaOverlay));
        _alphaOverlay.MouseMove += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateAlphaFromMouse(e.GetPosition(_alphaOverlay));
            }
        };

        _hexTextBox.LostFocus += (_, _) => ApplyHexInput();
        _alphaTextBox.LostFocus += (_, _) => ApplyRgbInput();
        _redTextBox.LostFocus += (_, _) => ApplyRgbInput();
        _greenTextBox.LostFocus += (_, _) => ApplyRgbInput();
        _blueTextBox.LostFocus += (_, _) => ApplyRgbInput();

        _hexTextBox.KeyDown += (_, e) => ExecuteOnEnter(e, ApplyHexInput);
        _alphaTextBox.KeyDown += (_, e) => ExecuteOnEnter(e, ApplyRgbInput);
        _redTextBox.KeyDown += (_, e) => ExecuteOnEnter(e, ApplyRgbInput);
        _greenTextBox.KeyDown += (_, e) => ExecuteOnEnter(e, ApplyRgbInput);
        _blueTextBox.KeyDown += (_, e) => ExecuteOnEnter(e, ApplyRgbInput);
    }

    private void UpdateSpectrumFromMouse(Point point)
    {
        _saturation = Math.Clamp(point.X / SpectrumSize, 0, 1);
        _value = Math.Clamp(1 - (point.Y / SpectrumSize), 0, 1);
        UpdateUiFromCurrentColor();
    }

    private void UpdateHueFromMouse(Point point)
    {
        _hue = Math.Clamp(point.Y / SliderHeight, 0, 1) * 360;
        UpdateUiFromCurrentColor();
    }

    private void UpdateAlphaFromMouse(Point point)
    {
        _alpha = (byte)Math.Clamp((int)Math.Round((1 - Math.Clamp(point.Y / SliderHeight, 0, 1)) * 255), 0, 255);
        UpdateUiFromCurrentColor();
    }

    private void SetColor(Color color)
    {
        _alpha = color.A;
        RgbToHsv(Color.FromRgb(color.R, color.G, color.B), out _hue, out _saturation, out _value);
        UpdateUiFromCurrentColor();
    }

    private void UpdateUiFromCurrentColor()
    {
        _isSynchronizing = true;
        try
        {
            var color = CurrentColor;
            var rgb = Color.FromRgb(color.R, color.G, color.B);
            var hueColor = ColorFromHsv(_hue, 1, 1);

            _previewFill.Background = new SolidColorBrush(color);
            _spectrumBase.Background = new SolidColorBrush(hueColor);
            _alphaGradient.Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(0, rgb.R, rgb.G, rgb.B), 1),
                    new(Color.FromArgb(255, rgb.R, rgb.G, rgb.B), 0)
                },
                new Point(0.5, 0),
                new Point(0.5, 1));

            _rgbSummary.Text = $"ARGB {_alpha}, {color.R}, {color.G}, {color.B}";
            _hexTextBox.Text = ToHex(color);
            _alphaTextBox.Text = _alpha.ToString(CultureInfo.InvariantCulture);
            _redTextBox.Text = color.R.ToString(CultureInfo.InvariantCulture);
            _greenTextBox.Text = color.G.ToString(CultureInfo.InvariantCulture);
            _blueTextBox.Text = color.B.ToString(CultureInfo.InvariantCulture);

            Canvas.SetLeft(_spectrumMarker, Math.Clamp((_saturation * SpectrumSize) - (_spectrumMarker.Width / 2), 0, SpectrumSize - _spectrumMarker.Width));
            Canvas.SetTop(_spectrumMarker, Math.Clamp(((1 - _value) * SpectrumSize) - (_spectrumMarker.Height / 2), 0, SpectrumSize - _spectrumMarker.Height));

            Canvas.SetLeft(_hueMarker, 0);
            Canvas.SetTop(_hueMarker, Math.Clamp(((_hue / 360) * SliderHeight) - (_hueMarker.Height / 2), 0, SliderHeight - _hueMarker.Height));

            Canvas.SetLeft(_alphaMarker, 0);
            Canvas.SetTop(_alphaMarker, Math.Clamp(((1 - (_alpha / 255d)) * SliderHeight) - (_alphaMarker.Height / 2), 0, SliderHeight - _alphaMarker.Height));
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void ApplyHexInput()
    {
        if (_isSynchronizing)
        {
            return;
        }

        var normalized = _hexTextBox.Text.Trim().TrimStart('#');
        if (normalized.Length == 6
            && int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            SetColor(Color.FromArgb(_alpha, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF)));
            return;
        }

        if (normalized.Length == 8
            && int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            SetColor(Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF)));
            return;
        }

        UpdateUiFromCurrentColor();
    }

    private void ApplyRgbInput()
    {
        if (_isSynchronizing)
        {
            return;
        }

        if (TryParseByte(_alphaTextBox.Text, out var alpha)
            && TryParseByte(_redTextBox.Text, out var red)
            && TryParseByte(_greenTextBox.Text, out var green)
            && TryParseByte(_blueTextBox.Text, out var blue))
        {
            SetColor(Color.FromArgb(alpha, red, green, blue));
            return;
        }

        UpdateUiFromCurrentColor();
    }

    private static void ExecuteOnEnter(KeyEventArgs e, Action action)
    {
        if (e.Key == Key.Enter)
        {
            action();
            e.Handled = true;
        }
    }

    private static bool TryParseByte(string value, out byte parsed)
    {
        if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return true;
        }

        parsed = 0;
        return false;
    }

    private static string ToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Brush CreateHueBrush()
    {
        return new LinearGradientBrush(
            new GradientStopCollection
            {
                new(ColorFromHsv(0, 1, 1), 0.0),
                new(ColorFromHsv(60, 1, 1), 1d / 6d),
                new(ColorFromHsv(120, 1, 1), 2d / 6d),
                new(ColorFromHsv(180, 1, 1), 3d / 6d),
                new(ColorFromHsv(240, 1, 1), 4d / 6d),
                new(ColorFromHsv(300, 1, 1), 5d / 6d),
                new(ColorFromHsv(360, 1, 1), 1.0)
            },
            new Point(0.5, 0),
            new Point(0.5, 1));
    }

    private static Brush CreateCheckerBrush()
    {
        var dark = new SolidColorBrush(Color.FromRgb(31, 41, 55));
        var light = new SolidColorBrush(Color.FromRgb(55, 65, 81));

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 12, 12))));
        drawing.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 6, 6))));
        drawing.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(6, 6, 6, 6))));

        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 12, 12),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 12, 12),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
    }

    private static TextBox CreateValueTextBox()
    {
        return new TextBox
        {
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Color.FromRgb(22, 33, 49)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            MinWidth = 96
        };
    }

    private static Button CreateActionButton(string content, bool primary)
    {
        return new Button
        {
            Content = content,
            MinWidth = 112,
            Margin = new Thickness(primary ? 10 : 0, 0, 0, 0),
            Padding = new Thickness(16, 9, 16, 9),
            Background = new SolidColorBrush(primary ? Color.FromRgb(14, 165, 233) : Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(56, 189, 248) : Color.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1)
        };
    }

    private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == red)
        {
            hue = 60 * (((green - blue) / delta) % 6);
        }
        else if (max == green)
        {
            hue = 60 * (((blue - red) / delta) + 2);
        }
        else
        {
            hue = 60 * (((red - green) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        saturation = max == 0 ? 0 : delta / max;
        value = max;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var chroma = value * saturation;
        var segment = hue / 60d;
        var x = chroma * (1 - Math.Abs((segment % 2) - 1));
        var match = value - chroma;

        var (red, green, blue) = segment switch
        {
            >= 0 and < 1 => (chroma, x, 0d),
            >= 1 and < 2 => (x, chroma, 0d),
            >= 2 and < 3 => (0d, chroma, x),
            >= 3 and < 4 => (0d, x, chroma),
            >= 4 and < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromRgb(
            (byte)Math.Round((red + match) * 255),
            (byte)Math.Round((green + match) * 255),
            (byte)Math.Round((blue + match) * 255));
    }
}
