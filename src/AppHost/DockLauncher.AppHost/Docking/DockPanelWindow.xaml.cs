using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DockLauncher.AppHost.Configuration;
using DockLauncher.AppHost.DragDrop;
using DockLauncher.Modules.Panels.Domain;
using System.Runtime.InteropServices;

namespace DockLauncher.AppHost.Docking;

public partial class DockPanelWindow : Window
{
    private const string DockPanelItemDragFormat = "DockLauncher.DockPanelItemId";
    private const double VisibleEdgeThickness = 10;
    private const double EdgeRevealDistance = 42;
    private const double ResizeBorderThickness = 8;
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;

    private double _restingOpacity;
    private readonly DispatcherTimer _autoHideTimer;
    private double _revealedLeft;
    private double _revealedTop;
    private double _collapsedLeft;
    private double _collapsedTop;
    private PanelPosition _panelPosition = PanelPosition.Floating;
    private bool _isRevealed = true;
    private Point? _dockItemDragStart;
    private Guid? _draggedDockItemId;
    private FrameworkElement? _draggedDockElement;
    private Border? _dropTargetBorder;
    private bool _hasOpenContextMenu;
    private double _overflowScrollOffset;
    private HwndSource? _hwndSource;
    private readonly DispatcherTimer _persistSizeTimer;
    private bool _applyingWindowMetrics;

    public DockPanelWindow(DockPanelWindowViewModel viewModel)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this, new WindowDisplayPolicyOptions(MaxWidthRatio: 1, MaxHeightRatio: 1, ClampLocation: false));
        DataContext = viewModel;
        ApplyInitialWindowMetrics(viewModel);
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        MouseMove += OnMouseMove;
        PanelShell.PreviewMouseLeftButtonDown += OnPanelChromePreviewMouseLeftButtonDown;
        Drop += OnDrop;
        DragOver += OnDragOver;
        AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler(OnAnyContextMenuOpening), handledEventsToo: true);
        AddHandler(ContextMenuClosingEvent, new ContextMenuEventHandler(OnAnyContextMenuClosing), handledEventsToo: true);
        StateChanged += OnStateChanged;
        SizeChanged += OnSizeChanged;
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        _persistSizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _persistSizeTimer.Tick += OnPersistSizeTimerTick;
        Closed += (_, _) =>
        {
            _autoHideTimer.Stop();
            _persistSizeTimer.Stop();
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _hwndSource?.AddHook(WndProc);
        ApplyTopmostState();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var screenPoint = new Point(GetSignedLoWord(lParam), GetSignedHiWord(lParam));
        var point = PointFromScreen(screenPoint);
        var hit = ResolveResizeHitTest(point);
        if (hit == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hit);
    }

    private int ResolveResizeHitTest(Point point)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var onLeft = point.X <= ResizeBorderThickness;
        var onRight = point.X >= width - ResizeBorderThickness;
        var onTop = point.Y <= ResizeBorderThickness;
        var onBottom = point.Y >= height - ResizeBorderThickness;

        if (onTop && onLeft)
        {
            return HtTopLeft;
        }

        if (onTop && onRight)
        {
            return HtTopRight;
        }

        if (onBottom && onLeft)
        {
            return HtBottomLeft;
        }

        if (onBottom && onRight)
        {
            return HtBottomRight;
        }

        if (onLeft)
        {
            return HtLeft;
        }

        if (onRight)
        {
            return HtRight;
        }

        if (onTop)
        {
            return HtTop;
        }

        return onBottom ? HtBottom : 0;
    }

    private static short GetSignedLoWord(IntPtr value)
    {
        return unchecked((short)((long)value & 0xFFFF));
    }

    private static short GetSignedHiWord(IntPtr value)
    {
        return unchecked((short)(((long)value >> 16) & 0xFFFF));
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        WindowState = WindowState.Normal;
        if (DataContext is DockPanelWindowViewModel viewModel)
        {
            ApplyWindowState(viewModel, preserveRevealState: true);
        }
    }

    public void UpdateViewModel(DockPanelWindowViewModel viewModel)
    {
        DataContext = viewModel;
        ApplyOverflowOffset(viewModel, immediate: true);
        ApplyInitialWindowMetrics(viewModel);

        if (IsLoaded)
        {
            ApplyWindowState(viewModel, preserveRevealState: true);
        }
    }

    public void EnsureRuntimeVisible()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
        }

        if (DataContext is DockPanelWindowViewModel viewModel)
        {
            ApplyTopmostState(viewModel);
            if (IsLoaded)
            {
                ApplyWindowState(viewModel, preserveRevealState: true);
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DockPanelWindowViewModel viewModel)
        {
            ApplyOverflowOffset(viewModel, immediate: true);
            ApplyWindowState(viewModel, preserveRevealState: false);
            Dispatcher.BeginInvoke(() => ApplyOverflowOffset(viewModel, immediate: true), DispatcherPriority.Loaded);
        }
    }

    private void OnOverflowViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not DockPanelWindowViewModel viewModel || !viewModel.OverflowActive)
        {
            return;
        }

        var currentOffset = viewModel.IsHorizontalOrientation
            ? OverflowScrollViewer.HorizontalOffset
            : OverflowScrollViewer.VerticalOffset;
        var maxScrollOffset = viewModel.IsHorizontalOrientation
            ? OverflowScrollViewer.ScrollableWidth
            : OverflowScrollViewer.ScrollableHeight;
        var delta = e.Delta < 0 ? viewModel.ItemExtent : -viewModel.ItemExtent;
        var nextOffset = Math.Clamp(currentOffset + delta, 0, maxScrollOffset);
        if (Math.Abs(nextOffset - currentOffset) < 0.5)
        {
            e.Handled = true;
            return;
        }

        _overflowScrollOffset = nextOffset;
        ApplyOverflowOffset(viewModel, immediate: true);
        e.Handled = true;
    }

    private void ApplyOverflowOffset(DockPanelWindowViewModel viewModel, bool immediate)
    {
        if (viewModel.IsHorizontalOrientation)
        {
            _overflowScrollOffset = Math.Min(_overflowScrollOffset, OverflowScrollViewer.ScrollableWidth);
            OverflowScrollViewer.ScrollToHorizontalOffset(_overflowScrollOffset);
        }
        else
        {
            _overflowScrollOffset = Math.Min(_overflowScrollOffset, OverflowScrollViewer.ScrollableHeight);
            OverflowScrollViewer.ScrollToVerticalOffset(_overflowScrollOffset);
        }

        UpdateOverflowIndicators(viewModel);
    }

    private void UpdateOverflowIndicators(DockPanelWindowViewModel viewModel)
    {
        var currentOffset = viewModel.IsHorizontalOrientation
            ? OverflowScrollViewer.HorizontalOffset
            : OverflowScrollViewer.VerticalOffset;
        var maxScrollOffset = viewModel.IsHorizontalOrientation
            ? OverflowScrollViewer.ScrollableWidth
            : OverflowScrollViewer.ScrollableHeight;
        var startOpacity = currentOffset <= 0.5 ? 0.28 : 1;
        var endOpacity = currentOffset >= maxScrollOffset - 0.5 ? 0.28 : 1;
        OverflowStartIndicator.Opacity = startOpacity;
        OverflowTopIndicator.Opacity = startOpacity;
        OverflowEndIndicator.Opacity = endOpacity;
        OverflowBottomIndicator.Opacity = endOpacity;
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimatePanelHover(isHovered: true);

        if (DataContext is DockPanelWindowViewModel viewModel && viewModel.AutoHideEnabled && _panelPosition != PanelPosition.Floating)
        {
            ApplyRevealedState();
        }
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimatePanelHover(isHovered: false);
        ResetDockMagnification();

        if (DataContext is DockPanelWindowViewModel viewModel && viewModel.AutoHideEnabled && _panelPosition != PanelPosition.Floating)
        {
            if (!viewModel.HasOpenFlyouts && !_hasOpenContextMenu && !IsCursorNearPanelOrEdge())
            {
                ApplyCollapsedState();
            }
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        UpdateDockMagnification(e.GetPosition(this));
    }

    private void AnimatePanelHover(bool isHovered)
    {
        var duration = TimeSpan.FromMilliseconds(isHovered ? 150 : 120);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var targetOverlayOpacity = isHovered ? 0.36 : 0;
        PanelHoverOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(targetOverlayOpacity, duration) { EasingFunction = easing });
    }

    private void OnAnyContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _hasOpenContextMenu = true;
        if (DataContext is DockPanelWindowViewModel viewModel && viewModel.AutoHideEnabled && _panelPosition != PanelPosition.Floating)
        {
            ApplyRevealedState();
        }
    }

    private void OnAnyContextMenuClosing(object sender, ContextMenuEventArgs e)
    {
        _hasOpenContextMenu = false;
    }

    private void ApplyInitialWindowMetrics(DockPanelWindowViewModel viewModel)
    {
        StopWindowAnimations();
        ApplyResizeLimits(viewModel);
        _applyingWindowMetrics = true;
        try
        {
            Width = viewModel.WindowWidth;
            Height = viewModel.WindowHeight;
            Left = viewModel.Left;
            Top = viewModel.Top;
            ApplyTopmostState(viewModel);
        }
        finally
        {
            _applyingWindowMetrics = false;
        }

        var autoHideActive = viewModel.AutoHideEnabled && viewModel.Position != PanelPosition.Floating;
        Opacity = autoHideActive ? Math.Min(viewModel.Opacity, 0.3) : viewModel.Opacity;
    }

    private void ApplyWindowState(DockPanelWindowViewModel viewModel, bool preserveRevealState)
    {
        StopWindowAnimations();
        ApplyResizeLimits(viewModel);
        _applyingWindowMetrics = true;
        try
        {
            Width = viewModel.WindowWidth;
            Height = viewModel.WindowHeight;
            ApplyTopmostState(viewModel);
        }
        finally
        {
            _applyingWindowMetrics = false;
        }
        _panelPosition = viewModel.Position;

        var autoHideActive = viewModel.AutoHideEnabled && _panelPosition != PanelPosition.Floating;
        _restingOpacity = autoHideActive ? Math.Min(viewModel.Opacity, 0.3) : viewModel.Opacity;
        _revealedLeft = viewModel.Left;
        _revealedTop = viewModel.Top;
        UpdateAutoHideAnchors();

        if (autoHideActive)
        {
            _autoHideTimer.Start();

            if (preserveRevealState && (IsMouseOver || viewModel.HasOpenFlyouts || _hasOpenContextMenu || _isRevealed || IsCursorNearPanelOrEdge()))
            {
                _isRevealed = true;
                Left = _revealedLeft;
                Top = _revealedTop;
                Opacity = viewModel.Opacity;
            }
            else
            {
                ApplyCollapsedState(immediate: true);
            }
        }
        else
        {
            _autoHideTimer.Stop();
            _isRevealed = true;
            Left = viewModel.Left;
            Top = viewModel.Top;
            Opacity = _restingOpacity;
        }

    }

    private void ApplyResizeLimits(DockPanelWindowViewModel viewModel)
    {
        MinWidth = Math.Max(56, viewModel.ItemSlotWidth + (viewModel.EffectivePanelPadding.Left + viewModel.EffectivePanelPadding.Right) + 6);
        MinHeight = Math.Max(72, viewModel.ItemExtent + (viewModel.EffectivePanelPadding.Top + viewModel.EffectivePanelPadding.Bottom) + viewModel.OverflowPrimaryGutter + 6);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_applyingWindowMetrics || !IsLoaded || DataContext is not DockPanelWindowViewModel)
        {
            return;
        }

        _persistSizeTimer.Stop();
        _persistSizeTimer.Start();
    }

    private async void OnPersistSizeTimerTick(object? sender, EventArgs e)
    {
        _persistSizeTimer.Stop();
        if (DataContext is not DockPanelWindowViewModel viewModel || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        await viewModel.UpdatePanelPositionAsync(_panelPosition, Left, Top, ActualWidth, ActualHeight);
    }

    private void StopWindowAnimations()
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
    }

    private void ApplyTopmostState()
    {
        if (DataContext is DockPanelWindowViewModel viewModel)
        {
            ApplyTopmostState(viewModel);
        }
    }

    private void ApplyTopmostState(DockPanelWindowViewModel viewModel)
    {
        Topmost = viewModel.IsTopmost;
        if (!viewModel.IsTopmost)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetWindowPos(
                handle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
        }
    }

    private static DoubleAnimation CreateWindowAnimation(double target, TimeSpan duration, IEasingFunction easing)
    {
        return new DoubleAnimation(target, duration)
        {
            EasingFunction = easing
        };
    }

    private async void OnPanelChromePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin
            || FindAncestor<Button>(origin) is not null
            || FindAncestor<ScrollBar>(origin) is not null
            || FindAncestorWithDataContext<DockPanelItemViewModel>(origin) is not null
            || !IsPanelChromeDragHit(e.GetPosition(PanelShell)))
        {
            return;
        }

        if (DataContext is DockPanelWindowViewModel { IsLocked: true })
        {
            return;
        }

        e.Handled = true;
        var initialLeft = Left;
        var initialTop = Top;
        var wasFloating = _panelPosition == PanelPosition.Floating;

        try
        {
            DragMove();
        }
        catch
        {
            return;
        }

        if (Math.Abs(Left - initialLeft) < 0.5 && Math.Abs(Top - initialTop) < 0.5)
        {
            return;
        }

        if (DataContext is DockPanelWindowViewModel viewModel)
        {
            _panelPosition = PanelPosition.Floating;
            _revealedLeft = Left;
            _revealedTop = Top;
            UpdateAutoHideAnchors();
            await viewModel.UpdatePanelPositionAsync(
                PanelPosition.Floating,
                Left,
                Top,
                wasFloating ? ActualWidth : null,
                wasFloating ? ActualHeight : null);
        }
    }

    private bool IsPanelChromeDragHit(Point point)
    {
        var edge = PanelChrome.Padding;
        var leftEdge = Math.Max(edge.Left, VisibleEdgeThickness);
        var topEdge = Math.Max(edge.Top, VisibleEdgeThickness);
        var rightEdge = Math.Max(edge.Right, VisibleEdgeThickness);
        var bottomEdge = Math.Max(edge.Bottom, VisibleEdgeThickness);

        return point.X <= leftEdge
            || point.X >= PanelShell.ActualWidth - rightEdge
            || point.Y <= topEdge
            || point.Y >= PanelShell.ActualHeight - bottomEdge;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedPathExtractor.HasPaths(e.Data)
            ? DragDropEffects.Copy
            : e.Data.GetDataPresent(DockPanelItemDragFormat)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var droppedPaths = DroppedPathExtractor.ExtractPaths(e.Data);
        if (droppedPaths.Count > 0 && DataContext is DockPanelWindowViewModel viewModel)
        {
            e.Handled = true;
            await viewModel.AddDroppedPathsAsync(droppedPaths);
            return;
        }

        if (TryGetDraggedDockItemId(e, out var draggedItemId)
            && DataContext is DockPanelWindowViewModel dockViewModel)
        {
            e.Handled = true;
            await dockViewModel.MoveItemWithinPanelAsync(draggedItemId, targetItemId: null);
        }
    }

    private void OnDockItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DockPanelItemViewModel item })
        {
            return;
        }

        _dockItemDragStart = e.GetPosition(this);
        _draggedDockItemId = item.Id;
    }

    private void OnDockItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_dockItemDragStart is null
            || _draggedDockItemId is null
            || e.LeftButton != MouseButtonState.Pressed
            || sender is not FrameworkElement { DataContext: DockPanelItemViewModel item } element
            || item.Id != _draggedDockItemId.Value)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (Math.Abs(currentPosition.X - _dockItemDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - _dockItemDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(DockPanelItemDragFormat, _draggedDockItemId.Value.ToString("D"));
        _draggedDockElement = element;
        element.Opacity = 0.46;

        try
        {
            System.Windows.DragDrop.DoDragDrop(element, dataObject, DragDropEffects.Move);
        }
        finally
        {
            element.Opacity = 1;
            ClearDropTargetVisual();
            _dockItemDragStart = null;
            _draggedDockItemId = null;
            _draggedDockElement = null;
        }
    }

    private void OnDockItemDragOver(object sender, DragEventArgs e)
    {
        if (DroppedPathExtractor.HasPaths(e.Data))
        {
            ClearDropTargetVisual();
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (TryGetDraggedDockItemId(e, out var draggedItemId)
            && sender is FrameworkElement { DataContext: DockPanelItemViewModel targetItem }
            && draggedItemId != targetItem.Id)
        {
            e.Effects = DragDropEffects.Move;
            UpdateDropTargetVisual(sender as Border);
            e.Handled = true;
            return;
        }

        ClearDropTargetVisual();
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDockItemDrop(object sender, DragEventArgs e)
    {
        var droppedPaths = DroppedPathExtractor.ExtractPaths(e.Data);
        if (droppedPaths.Count > 0 && DataContext is DockPanelWindowViewModel dockViewModel)
        {
            e.Handled = true;
            ClearDropTargetVisual();
            await dockViewModel.AddDroppedPathsAsync(droppedPaths);
            return;
        }

        if (!TryGetDraggedDockItemId(e, out var draggedItemId)
            || sender is not FrameworkElement { DataContext: DockPanelItemViewModel targetItem }
            || DataContext is not DockPanelWindowViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        var insertionTargetItemId = ResolveInsertionTargetItemId(draggedItemId, targetItem, sender, e, viewModel);
        ClearDropTargetVisual();
        await viewModel.MoveItemWithinPanelAsync(draggedItemId, insertionTargetItemId);
    }

    private static Guid? ResolveInsertionTargetItemId(
        Guid draggedItemId,
        DockPanelItemViewModel targetItem,
        object dropSender,
        DragEventArgs e,
        DockPanelWindowViewModel viewModel)
    {
        if (dropSender is not FrameworkElement targetElement)
        {
            return targetItem.Id;
        }

        var pointer = e.GetPosition(targetElement);
        var insertAfterTarget = viewModel.ItemsOrientation == Orientation.Horizontal
            ? pointer.X >= targetElement.ActualWidth / 2
            : pointer.Y >= targetElement.ActualHeight / 2;

        if (!insertAfterTarget)
        {
            return targetItem.Id;
        }

        var targetIndex = IndexOfItem(viewModel.Items, targetItem.Id);
        if (targetIndex < 0)
        {
            return targetItem.Id;
        }

        for (var index = targetIndex + 1; index < viewModel.Items.Count; index++)
        {
            var nextItemId = viewModel.Items[index].Id;
            if (nextItemId != draggedItemId)
            {
                return nextItemId;
            }
        }

        return null;
    }

    private static int IndexOfItem(IReadOnlyList<DockPanelItemViewModel> items, Guid itemId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Id == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    private void UpdateDropTargetVisual(Border? border)
    {
        if (_dropTargetBorder == border)
        {
            return;
        }

        ClearDropTargetVisual();
        _dropTargetBorder = border;
        if (_dropTargetBorder is not null)
        {
            _dropTargetBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(190, 56, 189, 248));
            _dropTargetBorder.BorderThickness = new Thickness(1.5);
        }
    }

    private void ClearDropTargetVisual()
    {
        if (_dropTargetBorder is null)
        {
            return;
        }

        _dropTargetBorder.BorderBrush = Brushes.Transparent;
        _dropTargetBorder.BorderThickness = new Thickness(0);
        _dropTargetBorder = null;
    }

    private static bool TryGetDraggedDockItemId(DragEventArgs e, out Guid itemId)
    {
        if (e.Data.GetDataPresent(DockPanelItemDragFormat)
            && e.Data.GetData(DockPanelItemDragFormat) is string rawItemId
            && Guid.TryParse(rawItemId, out itemId))
        {
            return true;
        }

        itemId = Guid.Empty;
        return false;
    }

    private PanelPosition CalculateNearestPosition()
    {
        var workArea = SystemParameters.WorkArea;
        var distances = new Dictionary<PanelPosition, double>
        {
            [PanelPosition.Left] = Math.Abs(Left - workArea.Left),
            [PanelPosition.Right] = Math.Abs(workArea.Right - (Left + Width)),
            [PanelPosition.Top] = Math.Abs(Top - workArea.Top),
            [PanelPosition.Bottom] = Math.Abs(workArea.Bottom - (Top + Height))
        };

        var nearest = distances.MinBy(pair => pair.Value);
        return nearest.Value <= 80 ? nearest.Key : PanelPosition.Floating;
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is not DockPanelWindowViewModel viewModel || !viewModel.AutoHideEnabled)
        {
            return;
        }

        if (_isRevealed)
        {
            var nearestPosition = CalculateNearestPosition();
            if (nearestPosition != _panelPosition && nearestPosition != PanelPosition.Floating)
            {
                _panelPosition = nearestPosition;
                _revealedLeft = Left;
                _revealedTop = Top;
                UpdateAutoHideAnchors();
            }
        }

        if (_panelPosition == PanelPosition.Floating)
        {
            return;
        }

        if (viewModel.HasOpenFlyouts || _hasOpenContextMenu || IsCursorNearPanelOrEdge())
        {
            ApplyRevealedState();
        }
        else
        {
            ApplyCollapsedState();
        }
    }

    private bool IsCursorNearPanelOrEdge()
    {
        var workArea = SystemParameters.WorkArea;
        var currentBounds = new Rect(Left, Top, Width, Height);
        var cursor = GetCursorPosition();
        return DockAutoHideBehavior.ShouldReveal(_panelPosition, workArea, currentBounds, cursor, EdgeRevealDistance);
    }

    private void UpdateAutoHideAnchors()
    {
        var workArea = SystemParameters.WorkArea;
        var screenArea = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var currentBounds = new Rect(_revealedLeft, _revealedTop, Width, Height);
        _collapsedLeft = DockAutoHideBehavior.GetCollapsedLeft(_panelPosition, workArea, currentBounds, VisibleEdgeThickness);
        _collapsedTop = DockAutoHideBehavior.GetCollapsedTop(
            _panelPosition,
            workArea,
            screenArea,
            currentBounds,
            VisibleEdgeThickness,
            collapseBehindSystemTaskbar: true);
    }

    private void ApplyRevealedState()
    {
        if (DataContext is not DockPanelWindowViewModel viewModel || _isRevealed)
        {
            return;
        }

        _isRevealed = true;
        AnimateTo(_revealedLeft, _revealedTop, viewModel.Opacity, 150);
    }

    private void ApplyCollapsedState(bool immediate = false)
    {
        if (DataContext is not DockPanelWindowViewModel viewModel)
        {
            return;
        }

        _isRevealed = false;
        if (immediate)
        {
            Left = _collapsedLeft;
            Top = _collapsedTop;
            Opacity = _restingOpacity;
            return;
        }

        AnimateTo(_collapsedLeft, _collapsedTop, _restingOpacity, 180);
    }

    private void AnimateTo(double targetLeft, double targetTop, double targetOpacity, int durationMs)
    {
        BeginAnimation(LeftProperty, new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(durationMs)));
        BeginAnimation(TopProperty, new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(durationMs)));
        BeginAnimation(OpacityProperty, new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(durationMs)));
    }

    private void UpdateDockMagnification(Point cursorPosition)
    {
        var orientation = DataContext is DockPanelWindowViewModel viewModel
            ? viewModel.ItemsOrientation
            : Orientation.Horizontal;

        foreach (var button in FindDockItemButtons())
        {
            var transform = EnsureMutableScaleTransform(button);
            if (transform is null)
            {
                continue;
            }

            var center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), this);
            var axisDistance = DockMagnificationBehavior.ComputeAxisDistance(
                orientation,
                center.X,
                center.Y,
                cursorPosition.X,
                cursorPosition.Y);
            var scale = DockMagnificationBehavior.ComputeScale(axisDistance);
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    private void ResetDockMagnification()
    {
        foreach (var button in FindDockItemButtons())
        {
            var transform = EnsureMutableScaleTransform(button);
            if (transform is null)
            {
                continue;
            }

            transform.ScaleX = 1;
            transform.ScaleY = 1;
        }
    }

    private static Point GetCursorPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private IEnumerable<Button> FindDockItemButtons()
    {
        return FindVisualChildren<Button>(this)
            .Where(button => button.DataContext is DockPanelItemViewModel);
    }

    private static ScaleTransform? EnsureMutableScaleTransform(Button button)
    {
        if (button.RenderTransform is ScaleTransform scaleTransform)
        {
            if (scaleTransform.IsFrozen)
            {
                scaleTransform = scaleTransform.Clone();
                button.RenderTransform = scaleTransform;
            }

            return scaleTransform;
        }

        var transform = new ScaleTransform(1, 1);
        button.RenderTransform = transform;
        return transform;
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T match)
            {
                return match;
            }

            dependencyObject = System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private static T? FindAncestorWithDataContext<T>(DependencyObject? dependencyObject)
        where T : class
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is FrameworkElement { DataContext: T match })
            {
                return match;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
