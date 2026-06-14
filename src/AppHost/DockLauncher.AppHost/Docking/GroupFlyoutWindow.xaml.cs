using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DockLauncher.AppHost.Configuration;

namespace DockLauncher.AppHost.Docking;

public partial class GroupFlyoutWindow : Window
{
    private bool _closeAnimationStarted;
    private bool _allowClose;

    public GroupFlyoutWindow(GroupFlyoutWindowViewModel viewModel)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this);
        DataContext = viewModel;
        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is GroupFlyoutWindowViewModel viewModel)
        {
            Left = viewModel.Left;
            Top = viewModel.Top;
        }

        BeginOpenAnimation();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        BeginCloseAnimation();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            BeginCloseAnimation();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        BeginCloseAnimation();
    }

    private void BeginOpenAnimation()
    {
        Opacity = 0;
        Height = 0;
        var targetHeight = DataContext is GroupFlyoutWindowViewModel viewModel ? viewModel.WindowHeight : 360;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = easing });
        BeginAnimation(HeightProperty, new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        AnimateChrome(scale: 1, translateY: 0, durationMs: 220, easing);
    }

    private void BeginCloseAnimation()
    {
        if (_closeAnimationStarted)
        {
            return;
        }

        _closeAnimationStarted = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var opacityAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)) { EasingFunction = easing };
        opacityAnimation.Completed += (_, _) =>
        {
            _allowClose = true;
            Close();
        };

        BeginAnimation(OpacityProperty, opacityAnimation);
        BeginAnimation(HeightProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)) { EasingFunction = easing });
        AnimateChrome(scale: 0.97, translateY: 8, durationMs: 150, easing);
    }

    private void AnimateChrome(double scale, double translateY, int durationMs, IEasingFunction easing)
    {
        if (FlyoutChrome.RenderTransform is not TransformGroup transformGroup)
        {
            return;
        }

        var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
        var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (scaleTransform is null || translateTransform is null)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(durationMs);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration) { EasingFunction = easing });
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration) { EasingFunction = easing });
        translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateY, duration) { EasingFunction = easing });
    }
}
