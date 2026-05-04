using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace CrosshairOverlay;

public partial class ConfigWindow : Window
{
    private readonly OverlaySettingsStore _settingsStore;
    private readonly IReadOnlyList<string> _monitorNames;
    private readonly List<CheckBox> _monitorCheckBoxes = [];
    private bool _isUpdatingUi;
    private bool _isInitialized;

    public ConfigWindow()
        : this(new OverlaySettingsStore(new SettingsService()), ["Monitor 1"])
    {
    }

    public ConfigWindow(OverlaySettingsStore settingsStore, IReadOnlyList<string> monitorNames)
    {
        _settingsStore = settingsStore;
        _monitorNames = monitorNames;
        _isUpdatingUi = true;
        InitializeComponent();
        BuildMonitorSelectors();

        _settingsStore.SettingsChanged += OnStoreSettingsChanged;
        Closed += OnClosed;

        PopulateFromSettings(_settingsStore.Current);
        _isUpdatingUi = false;
        _isInitialized = true;
    }

    private void OnStoreSettingsChanged(object? sender, OverlaySettings settings)
    {
        PopulateFromSettings(settings);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _settingsStore.SettingsChanged -= OnStoreSettingsChanged;
    }

    private void PopulateFromSettings(OverlaySettings settings)
    {
        _isUpdatingUi = true;

        Language.SelectedIndex = string.Equals(settings.Language, "es", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        Title = Language.SelectedIndex == 0 ? "Ajustes de Crosshair Overlay" : "Crosshair Overlay Settings";

        EnableCenterDot.IsChecked = settings.EnableCenterDot;
        CenterDotShape.SelectedIndex = ToShapeIndex(settings.CenterDotShape);
        CenterDotSize.Value = settings.CenterDotSize;
        CenterDotOpacity.Value = settings.CenterDotOpacity;
        CenterDotColor.Text = settings.CenterDotColor;

        EnableDotGrid.IsChecked = settings.EnableDotGrid;
        DotGridPointShape.SelectedIndex = ToShapeIndex(settings.DotGridPointShape);
        DotGridAreaShape.SelectedIndex = ToAreaShapeIndex(settings.DotGridAreaShape);
        DotGridPointSize.Value = settings.DotGridPointSize;
        DotGridSpacing.Value = settings.DotGridSpacing;
        DotGridRows.Value = settings.DotGridRows;
        DotGridColumns.Value = settings.DotGridColumns;
        DotGridRadiusPoints.Value = settings.DotGridRadiusPoints;
        DotGridOpacity.Value = settings.DotGridOpacity;
        DotGridColor.Text = settings.DotGridColor;

        EnableCrosshair.IsChecked = settings.EnableCrosshair;
        CrosshairHorizontalLength.Value = settings.CrosshairHorizontalLength;
        CrosshairVerticalLength.Value = settings.CrosshairVerticalLength;
        CrosshairGap.Value = settings.CrosshairGap;
        CrosshairThickness.Value = settings.CrosshairThickness;
        CrosshairOpacity.Value = settings.CrosshairOpacity;
        CrosshairColor.Text = settings.CrosshairColor;

        var enabledMonitors = new HashSet<int>(settings.EnabledMonitorIndices ?? []);
        for (var i = 0; i < _monitorCheckBoxes.Count; i++)
        {
            _monitorCheckBoxes[i].IsChecked = enabledMonitors.Contains(i);
        }

        UpdateLabels();
        UpdateDotGridAreaEditors();

        _isUpdatingUi = false;
    }

    private void OnDotGridAreaShapeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        UpdateDotGridAreaEditors();
        OnAnySettingChanged(sender, e);
    }

    private void OnAnySettingChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || !_isInitialized)
        {
            return;
        }

        UpdateLabels();

        _settingsStore.Update(settings =>
        {
            settings.Language = Language.SelectedIndex == 0 ? "es" : "en";
            settings.EnableCenterDot = EnableCenterDot.IsChecked ?? false;
            settings.CenterDotShape = FromShapeIndex(CenterDotShape.SelectedIndex);
            settings.CenterDotSize = CenterDotSize.Value;
            settings.CenterDotOpacity = CenterDotOpacity.Value;
            settings.CenterDotColor = CenterDotColor.Text?.Trim() ?? "#FFFFFF";

            settings.EnableDotGrid = EnableDotGrid.IsChecked ?? false;
            settings.DotGridPointShape = FromShapeIndex(DotGridPointShape.SelectedIndex);
            settings.DotGridAreaShape = FromAreaShapeIndex(DotGridAreaShape.SelectedIndex);
            settings.DotGridPointSize = DotGridPointSize.Value;
            settings.DotGridSpacing = DotGridSpacing.Value;
            settings.DotGridRows = (int)Math.Round(DotGridRows.Value);
            settings.DotGridColumns = (int)Math.Round(DotGridColumns.Value);
            settings.DotGridRadiusPoints = (int)Math.Round(DotGridRadiusPoints.Value);
            settings.DotGridOpacity = DotGridOpacity.Value;
            settings.DotGridColor = DotGridColor.Text?.Trim() ?? "#00FF00";

            settings.EnableCrosshair = EnableCrosshair.IsChecked ?? false;
            settings.CrosshairHorizontalLength = CrosshairHorizontalLength.Value;
            settings.CrosshairVerticalLength = CrosshairVerticalLength.Value;
            settings.CrosshairGap = CrosshairGap.Value;
            settings.CrosshairThickness = CrosshairThickness.Value;
            settings.CrosshairOpacity = CrosshairOpacity.Value;
            settings.CrosshairColor = CrosshairColor.Text?.Trim() ?? "#FF0000";

            settings.EnabledMonitorIndices = GetSelectedMonitorIndices();
        });

        Title = Language.SelectedIndex == 0 ? "Ajustes de Crosshair Overlay" : "Crosshair Overlay Settings";
    }

    private void BuildMonitorSelectors()
    {
        MonitorSelectorPanel.Children.Clear();
        _monitorCheckBoxes.Clear();

        for (var i = 0; i < _monitorNames.Count; i++)
        {
            var checkBox = new CheckBox
            {
                Content = _monitorNames[i],
                Foreground = Brushes.White
            };
            checkBox.IsCheckedChanged += OnAnySettingChanged;
            _monitorCheckBoxes.Add(checkBox);
            MonitorSelectorPanel.Children.Add(checkBox);
        }
    }

    private List<int> GetSelectedMonitorIndices()
    {
        var selected = new List<int>();
        for (var i = 0; i < _monitorCheckBoxes.Count; i++)
        {
            if (_monitorCheckBoxes[i].IsChecked == true)
            {
                selected.Add(i);
            }
        }

        return selected;
    }

    private void UpdateLabels()
    {
        CenterDotSizeLabel.Text = $"Size: {CenterDotSize.Value:0}";
        CenterDotOpacityLabel.Text = $"Opacity: {CenterDotOpacity.Value:0.00}";

        DotGridPointSizeLabel.Text = $"Point size: {DotGridPointSize.Value:0}";
        DotGridSpacingLabel.Text = $"Point spacing: {DotGridSpacing.Value:0}";
        DotGridRowsLabel.Text = $"Rows: {DotGridRows.Value:0}";
        DotGridColumnsLabel.Text = $"Columns: {DotGridColumns.Value:0}";
        DotGridRadiusPointsLabel.Text = $"Radius points: {DotGridRadiusPoints.Value:0}";
        DotGridOpacityLabel.Text = $"Opacity: {DotGridOpacity.Value:0.00}";

        CrosshairHorizontalLengthLabel.Text = $"Horizontal length: {CrosshairHorizontalLength.Value:0}";
        CrosshairVerticalLengthLabel.Text = $"Vertical length: {CrosshairVerticalLength.Value:0}";
        CrosshairGapLabel.Text = $"Gap: {CrosshairGap.Value:0}";
        CrosshairThicknessLabel.Text = $"Thickness: {CrosshairThickness.Value:0}";
        CrosshairOpacityLabel.Text = $"Opacity: {CrosshairOpacity.Value:0.00}";
    }

    private void UpdateDotGridAreaEditors()
    {
        var isSquare = DotGridAreaShape.SelectedIndex <= 0;
        DotGridSquarePanel.IsVisible = isSquare;
        DotGridCirclePanel.IsVisible = !isSquare;
    }

    private static int ToShapeIndex(string? shape)
    {
        return string.Equals(shape, "Square", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string FromShapeIndex(int index)
    {
        return index == 1 ? "Square" : "Circle";
    }

    private static int ToAreaShapeIndex(string? shape)
    {
        return string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string FromAreaShapeIndex(int index)
    {
        return index == 1 ? "Circle" : "Square";
    }
}
