using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CrosshairOverlay;

public partial class ConfigWindow : Window
{
    private readonly OverlaySettingsStore _settingsStore;
    private readonly IReadOnlyList<PixelRect> _monitorBounds;
    private readonly List<CheckBox> _monitorCheckBoxes = [];
    private bool _isUpdatingUi;
    private bool _isInitialized;

    public ConfigWindow()
        : this(new OverlaySettingsStore(new SettingsService()), [new PixelRect(0, 0, 1920, 1080)])
    {
    }

    public ConfigWindow(OverlaySettingsStore settingsStore, IReadOnlyList<PixelRect> monitorBounds)
    {
        _settingsStore = settingsStore;
        _monitorBounds = monitorBounds;
        _isUpdatingUi = true;
        InitializeComponent();
        Icon = App.TryCreateTrayIcon();
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

        ApplyLocalization();
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

        ApplyLocalization();
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
    }

    private void BuildMonitorSelectors()
    {
        MonitorSelectorPanel.Children.Clear();
        _monitorCheckBoxes.Clear();

        for (var i = 0; i < _monitorBounds.Count; i++)
        {
            var checkBox = new CheckBox();
            checkBox.IsCheckedChanged += OnAnySettingChanged;
            _monitorCheckBoxes.Add(checkBox);
            MonitorSelectorPanel.Children.Add(checkBox);
        }

        ApplyLocalization();
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
        CenterDotSizeLabel.Text = $"{L("Size")}: {CenterDotSize.Value:0}";
        CenterDotOpacityLabel.Text = $"{L("Opacity")}: {CenterDotOpacity.Value:0.00}";

        DotGridPointSizeLabel.Text = $"{L("PointSize")}: {DotGridPointSize.Value:0}";
        DotGridSpacingLabel.Text = $"{L("PointSpacing")}: {DotGridSpacing.Value:0}";
        DotGridRowsLabel.Text = $"{L("Rows")}: {DotGridRows.Value:0}";
        DotGridColumnsLabel.Text = $"{L("Columns")}: {DotGridColumns.Value:0}";
        DotGridRadiusPointsLabel.Text = $"{L("RadiusPoints")}: {DotGridRadiusPoints.Value:0}";
        DotGridOpacityLabel.Text = $"{L("Opacity")}: {DotGridOpacity.Value:0.00}";

        CrosshairHorizontalLengthLabel.Text = $"{L("HorizontalLength")}: {CrosshairHorizontalLength.Value:0}";
        CrosshairVerticalLengthLabel.Text = $"{L("VerticalLength")}: {CrosshairVerticalLength.Value:0}";
        CrosshairGapLabel.Text = $"{L("Gap")}: {CrosshairGap.Value:0}";
        CrosshairThicknessLabel.Text = $"{L("Thickness")}: {CrosshairThickness.Value:0}";
        CrosshairOpacityLabel.Text = $"{L("Opacity")}: {CrosshairOpacity.Value:0.00}";
    }

    private void ApplyLocalization()
    {
        Title = L("WindowTitle");
        HeaderTitle.Text = L("HeaderTitle");
        GeneralTitle.Text = L("General");
        LanguageLabel.Text = L("Language");
        MonitorsLabel.Text = L("Monitors");
        LanguageSpanishItem.Content = "Español";
        LanguageEnglishItem.Content = "English";

        CenterDotTitle.Text = L("CenterDot");
        EnableCenterDot.Content = L("EnableCenterDot");
        CenterDotShapeLabel.Text = L("Shape");
        CenterDotShapeCircleItem.Content = L("Circle");
        CenterDotShapeSquareItem.Content = L("Square");
        CenterDotColorLabel.Text = L("ColorFormat");

        DotGridTitle.Text = L("DotGrid");
        EnableDotGrid.Content = L("EnableDotGrid");
        DotGridPointShapeLabel.Text = L("PointShape");
        DotGridPointShapeCircleItem.Content = L("Circle");
        DotGridPointShapeSquareItem.Content = L("Square");
        DotGridAreaShapeLabel.Text = L("GridAreaShape");
        DotGridAreaSquareItem.Content = L("Square");
        DotGridAreaCircleItem.Content = L("Circle");
        DotGridColorLabel.Text = L("ColorFormat");

        CrosshairTitle.Text = L("Crosshair");
        EnableCrosshair.Content = L("EnableCrosshair");
        CrosshairColorLabel.Text = L("ColorFormat");

        for (var i = 0; i < _monitorCheckBoxes.Count; i++)
        {
            var bounds = _monitorBounds[i];
            _monitorCheckBoxes[i].Content = $"{L("Monitor")} {i + 1} ({bounds.Width}x{bounds.Height})";
        }
    }

    private string L(string key)
    {
        return (IsSpanish, key) switch
        {
            (true, "WindowTitle") => "Ajustes de Crosshair Overlay",
            (true, "HeaderTitle") => "Ajustes de Overlay",
            (true, "General") => "General",
            (true, "Language") => "Idioma",
            (true, "Monitors") => "Monitores",
            (true, "CenterDot") => "Punto central",
            (true, "EnableCenterDot") => "Activar punto central",
            (true, "Shape") => "Forma",
            (true, "Circle") => "Circulo",
            (true, "Square") => "Cuadrado",
            (true, "ColorFormat") => "Color (#RRGGBB o #AARRGGBB)",
            (true, "DotGrid") => "Grilla de puntos",
            (true, "EnableDotGrid") => "Activar grilla de puntos",
            (true, "PointShape") => "Forma del punto",
            (true, "GridAreaShape") => "Forma del area de la grilla",
            (true, "Crosshair") => "Mira",
            (true, "EnableCrosshair") => "Activar mira",
            (true, "Size") => "Tamano",
            (true, "Opacity") => "Opacidad",
            (true, "PointSize") => "Tamano del punto",
            (true, "PointSpacing") => "Espaciado entre puntos",
            (true, "Rows") => "Filas",
            (true, "Columns") => "Columnas",
            (true, "RadiusPoints") => "Puntos de radio",
            (true, "HorizontalLength") => "Largo horizontal",
            (true, "VerticalLength") => "Largo vertical",
            (true, "Gap") => "Separacion",
            (true, "Thickness") => "Grosor",
            (true, "Monitor") => "Monitor",

            (false, "WindowTitle") => "Crosshair Overlay Settings",
            (false, "HeaderTitle") => "Overlay Settings",
            (false, "General") => "General",
            (false, "Language") => "Language",
            (false, "Monitors") => "Monitors",
            (false, "CenterDot") => "Center Dot",
            (false, "EnableCenterDot") => "Enable center dot",
            (false, "Shape") => "Shape",
            (false, "Circle") => "Circle",
            (false, "Square") => "Square",
            (false, "ColorFormat") => "Color (#RRGGBB or #AARRGGBB)",
            (false, "DotGrid") => "Dot Grid",
            (false, "EnableDotGrid") => "Enable dot grid",
            (false, "PointShape") => "Point shape",
            (false, "GridAreaShape") => "Grid area shape",
            (false, "Crosshair") => "Crosshair",
            (false, "EnableCrosshair") => "Enable crosshair",
            (false, "Size") => "Size",
            (false, "Opacity") => "Opacity",
            (false, "PointSize") => "Point size",
            (false, "PointSpacing") => "Point spacing",
            (false, "Rows") => "Rows",
            (false, "Columns") => "Columns",
            (false, "RadiusPoints") => "Radius points",
            (false, "HorizontalLength") => "Horizontal length",
            (false, "VerticalLength") => "Vertical length",
            (false, "Gap") => "Gap",
            (false, "Thickness") => "Thickness",
            (false, "Monitor") => "Monitor",

            _ => key
        };
    }

    private bool IsSpanish => Language.SelectedIndex == 0;

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
