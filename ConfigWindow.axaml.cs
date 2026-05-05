using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CrosshairOverlay;

public partial class ConfigWindow : Window
{
    private readonly OverlaySettingsStore _settingsStore;
    private readonly IReadOnlyList<PixelRect> _monitorBounds;
    private readonly List<CheckBox> _monitorCheckBoxes = [];
    private readonly List<SearchSection> _searchSections = [];
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
        InitializeSearchSections();
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

        EnableMotionDetection.IsChecked = settings.EnableMotionDetection;
        MotionRegionPreview.IsChecked = settings.MotionRegionPreview;
        MotionRegionSize.Value = settings.MotionRegionSize;
        MotionSmoothingFrames.Value = settings.MotionSmoothingFrames;
        MotionCancellationIntensity.Value = settings.MotionCancellationIntensity;
        MotionCaptureFps.Value = settings.MotionCaptureFps;
        MotionDeadZonePixels.Value = settings.MotionDeadZonePixels;
        DebugShowMotionCapturePreview.IsChecked = settings.DebugShowMotionCapturePreview;
        var enabledMonitors = new HashSet<int>(settings.EnabledMonitorIndices ?? []);
        for (var i = 0; i < _monitorCheckBoxes.Count; i++)
        {
            _monitorCheckBoxes[i].IsChecked = enabledMonitors.Contains(i);
        }

        ApplyLocalization();
        UpdateLabels();
        UpdateDotGridAreaEditors();
        ApplySearch();

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

            settings.EnableMotionDetection = EnableMotionDetection.IsChecked ?? false;
            settings.MotionRegionPreview = MotionRegionPreview.IsChecked ?? false;
            settings.MotionRegionSize = (int)Math.Round(MotionRegionSize.Value);
            settings.MotionSmoothingFrames = (int)Math.Round(MotionSmoothingFrames.Value);
            settings.MotionCancellationIntensity = MotionCancellationIntensity.Value;
            settings.MotionCaptureFps = (int)Math.Round(MotionCaptureFps.Value);
            settings.MotionDeadZonePixels = MotionDeadZonePixels.Value;
            settings.DebugShowMotionCapturePreview = DebugShowMotionCapturePreview.IsChecked ?? false;

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

    private void InitializeSearchSections()
    {
        _searchSections.Clear();
        _searchSections.Add(new SearchSection(GeneralTab, GeneralCard, ["general", "language", "idioma", "monitor", "monitors", "monitores", "display", "pantalla"]));
        _searchSections.Add(new SearchSection(AppearanceTab, CenterDotCard, ["center", "dot", "punto", "center dot", "punto central", "shape", "forma", "size", "tamano", "opacity", "opacidad", "color"]));
        _searchSections.Add(new SearchSection(AppearanceTab, DotGridCard, ["grid", "dot grid", "grilla", "puntos", "spacing", "espaciado", "rows", "filas", "columns", "columnas", "radius", "radio", "color"]));
        _searchSections.Add(new SearchSection(AppearanceTab, CrosshairCard, ["crosshair", "mira", "gap", "separacion", "thickness", "grosor", "length", "largo", "color", "opacity"]));
        _searchSections.Add(new SearchSection(MotionTab, MotionDetectionCard, ["motion", "movement", "movimiento", "detection", "deteccion", "capture", "captura", "fps", "smoothing", "suavizado", "dead zone", "zona muerta"]));
        _searchSections.Add(new SearchSection(DebugTab, DebugToolsCard, ["debug", "preview", "herramientas", "depuracion", "captured", "imagen"]));
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private void ApplySearch()
    {
        var query = NormalizeSearch(SearchBox.Text);
        if (string.IsNullOrEmpty(query))
        {
            foreach (var section in _searchSections)
            {
                section.Card.IsVisible = true;
                section.Tab.IsVisible = true;
            }

            SearchHintText.IsVisible = false;
            return;
        }

        var matchesByTab = new Dictionary<TabItem, int>();
        SearchSection? firstMatch = null;
        var totalMatches = 0;

        foreach (var section in _searchSections)
        {
            var matches = MatchesSearch(section.Keywords, query);
            section.Card.IsVisible = matches;
            if (!matches)
            {
                continue;
            }

            if (!matchesByTab.TryAdd(section.Tab, 1))
            {
                matchesByTab[section.Tab]++;
            }

            firstMatch ??= section;
            totalMatches++;
        }

        if (firstMatch is null)
        {
            foreach (var section in _searchSections)
            {
                section.Tab.IsVisible = true;
            }

            SearchHintText.Text = L("SearchNoResults");
            SearchHintText.IsVisible = true;
            return;
        }

        foreach (var section in _searchSections)
        {
            section.Tab.IsVisible = matchesByTab.ContainsKey(section.Tab);
        }

        SettingsTabs.SelectedItem = firstMatch.Tab;
        SearchHintText.Text = string.Format(L("SearchResultsFormat"), totalMatches);
        SearchHintText.IsVisible = true;
    }

    private static string NormalizeSearch(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static bool MatchesSearch(IEnumerable<string> keywords, string query)
    {
        foreach (var keyword in keywords)
        {
            var normalizedKeyword = keyword.ToLowerInvariant();
            if (normalizedKeyword.Contains(query, StringComparison.Ordinal) || query.Contains(normalizedKeyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

        MotionRegionSizeLabel.Text = $"{L("MotionRegionSize")}: {MotionRegionSize.Value:0}";
        MotionSmoothingFramesLabel.Text = $"{L("MotionSmoothingFrames")}: {MotionSmoothingFrames.Value:0}";
        MotionCancellationIntensityLabel.Text = $"{L("MotionCancellationIntensity")}: {MotionCancellationIntensity.Value:0.00}";
        MotionCaptureFpsLabel.Text = $"{L("MotionCaptureFps")}: {MotionCaptureFps.Value:0}";
        MotionDeadZonePixelsLabel.Text = $"{L("MotionDeadZonePixels")}: {MotionDeadZonePixels.Value:0.0}";
    }

    private void ApplyLocalization()
    {
        Title = L("WindowTitle");
        HeaderTitle.Text = L("HeaderTitle");
        HeaderSubtitle.Text = L("HeaderSubtitle");
        SearchLabel.Text = L("SearchLabel");
        SearchBox.PlaceholderText = L("SearchWatermark");
        GeneralTabHeader.Text = L("GeneralTab");
        AppearanceTabHeader.Text = L("AppearanceTab");
        MotionTabHeader.Text = L("MotionTab");
        DebugTabHeader.Text = L("DebugTab");
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

        MotionDetectionTitle.Text = L("MotionDetection");
        MotionDetectionExperimentalLabel.Text = L("Experimental");
        EnableMotionDetection.Content = L("EnableMotionDetection");
        MotionRegionPreview.Content = L("MotionRegionPreview");

        DebugToolsTitle.Text = L("DebugTools");
        DebugShowMotionCapturePreview.Content = L("DebugShowMotionCapturePreview");

        ApplySearch();

        var resetTooltip = L("Reset");
        ToolTip.SetTip(ResetCenterDotButton, resetTooltip);
        ToolTip.SetTip(ResetDotGridButton, resetTooltip);
        ToolTip.SetTip(ResetCrosshairButton, resetTooltip);
        ToolTip.SetTip(ResetMotionDetectionButton, resetTooltip);

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
            (true, "HeaderSubtitle") => "Organiza el overlay por áreas y salta directo a lo que necesitas.",
            (true, "SearchLabel") => "Búsqueda rápida",
            (true, "SearchWatermark") => "Buscar ajustes, por ejemplo: movimiento, color o monitor",
            (true, "GeneralTab") => "General",
            (true, "AppearanceTab") => "Apariencia",
            (true, "MotionTab") => "Movimiento",
            (true, "DebugTab") => "Depuración",
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
            (true, "MotionDetection") => "Detección de movimiento",
            (true, "Experimental") => "(Experimental)",
            (true, "EnableMotionDetection") => "Activar detección de movimiento",
            (true, "MotionRegionPreview") => "Mostrar región de captura",
            (true, "MotionRegionSize") => "Tamaño de región de captura",
            (true, "MotionSmoothingFrames") => "Suavizado de frames",
            (true, "MotionCancellationIntensity") => "Intensidad de cancelación",
            (true, "MotionCaptureFps") => "FPS de captura",
            (true, "MotionDeadZonePixels") => "Zona muerta (px)",
            (true, "DebugTools") => "Herramientas de depuración",
            (true, "DebugShowMotionCapturePreview") => "Mostrar preview de la imagen capturada",
            (true, "SearchNoResults") => "No se encontraron ajustes con esa búsqueda",
            (true, "SearchResultsFormat") => "{0} secciones coinciden",
            (true, "Reset") => "Reiniciar",

            (false, "WindowTitle") => "Crosshair Overlay Settings",
            (false, "HeaderTitle") => "Overlay Settings",
            (false, "HeaderSubtitle") => "Organize the overlay by area and jump directly to what you need.",
            (false, "SearchLabel") => "Quick search",
            (false, "SearchWatermark") => "Search settings, for example: motion, color or monitor",
            (false, "GeneralTab") => "General",
            (false, "AppearanceTab") => "Appearance",
            (false, "MotionTab") => "Motion",
            (false, "DebugTab") => "Debug",
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
            (false, "MotionDetection") => "Motion Detection",
            (false, "Experimental") => "(Experimental)",
            (false, "EnableMotionDetection") => "Enable motion detection",
            (false, "MotionRegionPreview") => "Show capture region",
            (false, "MotionRegionSize") => "Capture region size",
            (false, "MotionSmoothingFrames") => "Frame smoothing",
            (false, "MotionCancellationIntensity") => "Cancellation intensity",
            (false, "MotionCaptureFps") => "Capture FPS",
            (false, "MotionDeadZonePixels") => "Dead zone (px)",
            (false, "DebugTools") => "Debug Tools",
            (false, "DebugShowMotionCapturePreview") => "Show captured motion image preview",
            (false, "SearchNoResults") => "No settings matched that search",
            (false, "SearchResultsFormat") => "{0} matching sections",
            (false, "Reset") => "Reset",

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

    private void OnResetCenterDot(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsStore.Update(s =>
        {
            var d = new OverlaySettings();
            s.CenterDotSize = d.CenterDotSize;
            s.CenterDotShape = d.CenterDotShape;
            s.CenterDotColor = d.CenterDotColor;
            s.CenterDotOpacity = d.CenterDotOpacity;
        });
    }

    private void OnResetDotGrid(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsStore.Update(s =>
        {
            var d = new OverlaySettings();
            s.DotGridPointSize = d.DotGridPointSize;
            s.DotGridPointShape = d.DotGridPointShape;
            s.DotGridAreaShape = d.DotGridAreaShape;
            s.DotGridColor = d.DotGridColor;
            s.DotGridRows = d.DotGridRows;
            s.DotGridColumns = d.DotGridColumns;
            s.DotGridRadiusPoints = d.DotGridRadiusPoints;
            s.DotGridSpacing = d.DotGridSpacing;
            s.DotGridOpacity = d.DotGridOpacity;
        });
    }

    private void OnResetCrosshair(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsStore.Update(s =>
        {
            var d = new OverlaySettings();
            s.CrosshairColor = d.CrosshairColor;
            s.CrosshairOpacity = d.CrosshairOpacity;
            s.CrosshairHorizontalLength = d.CrosshairHorizontalLength;
            s.CrosshairVerticalLength = d.CrosshairVerticalLength;
            s.CrosshairGap = d.CrosshairGap;
            s.CrosshairThickness = d.CrosshairThickness;
        });
    }

    private void OnResetMotionDetection(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settingsStore.Update(s =>
        {
            var d = new OverlaySettings();
            s.MotionRegionSize = d.MotionRegionSize;
            s.MotionRegionPreview = d.MotionRegionPreview;
            s.MotionSmoothingFrames = d.MotionSmoothingFrames;
            s.MotionCancellationIntensity = d.MotionCancellationIntensity;
            s.MotionCaptureFps = d.MotionCaptureFps;
            s.MotionDeadZonePixels = d.MotionDeadZonePixels;
        });
    }

    private sealed record SearchSection(TabItem Tab, Control Card, string[] Keywords);
}
