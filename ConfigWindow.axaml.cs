using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CrosshairOverlay;

public partial class ConfigWindow : Window
{
    private const double PresetPreviewHeight = 154;

    private readonly OverlaySettingsStore _settingsStore;
    private readonly IReadOnlyList<PixelRect> _monitorBounds;
    private readonly List<CheckBox> _monitorCheckBoxes = [];
    private readonly List<SearchSection> _searchSections = [];
    private readonly List<(TextBlock Label, OverlayPreset Preset)> _presetLabels = [];
    private IImage? _presetBackground;
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
        BuildPresets();

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
        DebugAllowConfigWindowCapture.IsChecked = settings.DebugAllowConfigWindowCapture;
        DebugAllowOverlayCapture.IsChecked = settings.DebugAllowOverlayCapture;
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
            settings.DebugAllowConfigWindowCapture = DebugAllowConfigWindowCapture.IsChecked ?? false;
            settings.DebugAllowOverlayCapture = DebugAllowOverlayCapture.IsChecked ?? false;

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

    private void BuildPresets()
    {
        _presetBackground = TryLoadPresetBackground();
        PresetsPanel.Children.Clear();
        _presetLabels.Clear();

        foreach (var preset in OverlayPresets.All)
        {
            PresetsPanel.Children.Add(CreatePresetCard(preset));
        }

        foreach (var userPreset in _settingsStore.Current.UserPresets)
        {
            PresetsPanel.Children.Add(CreateUserPresetCard(userPreset));
        }

        PresetsPanel.Children.Add(CreateNewPresetCard());
    }

    private Control CreatePresetCard(OverlayPreset preset)
    {
        // Build a settings snapshot for the preview by starting from defaults and applying the preset.
        var previewSettings = new OverlaySettings();
        preset.Apply(previewSettings);

        var crosshair = new CrosshairControl
        {
            Width = OverlayPresets.BasePreviewWidth,
            Height = OverlayPresets.BasePreviewHeight,
        };
        crosshair.ApplySettings(previewSettings);

        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = crosshair,
            RenderTransform = new ScaleTransform(0.68, 0.68),
            RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var previewLayer = new Grid();
        if (_presetBackground is not null)
        {
            previewLayer.Children.Add(new Image
            {
                Source = _presetBackground,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            });
        }

        previewLayer.Children.Add(viewbox);

        var previewContainer = new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Height = PresetPreviewHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = previewLayer,
        };

        var nameLabel = new TextBlock
        {
            Text = L(preset.NameKey),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeight.SemiBold,
        };
        _presetLabels.Add((nameLabel, preset));

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(previewContainer);
        stack.Children.Add(nameLabel);

        var normalBg = new SolidColorBrush(new Color(255, 25, 28, 36));
        var hoverBg = new SolidColorBrush(new Color(255, 38, 43, 55));

        var card = new Border
        {
            Background = normalBg,
            BorderBrush = new SolidColorBrush(new Color(255, 52, 58, 68)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(3),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = stack,
        };

        card.PointerEntered += (_, _) => card.Background = hoverBg;
        card.PointerExited += (_, _) => card.Background = normalBg;
        card.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _settingsStore.Update(preset.Apply);
            }
        };

        return card;
    }

    private static IImage? TryLoadPresetBackground()
    {
        try
        {
            var uri = new Uri("avares://CrosshairOverlay/assets/presets/preset-bg.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private Control CreateUserPresetCard(UserPreset userPreset)
    {
        var id = userPreset.Id;

        var previewSettings = OverlayPresets.ToPreviewSettings(userPreset.Values);
        var crosshair = new CrosshairControl
        {
            Width = OverlayPresets.BasePreviewWidth,
            Height = OverlayPresets.BasePreviewHeight,
        };
        crosshair.ApplySettings(previewSettings);

        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = crosshair,
            RenderTransform = new ScaleTransform(0.68, 0.68),
            RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var previewLayer = new Grid();
        if (_presetBackground is not null)
        {
            previewLayer.Children.Add(new Image
            {
                Source = _presetBackground,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            });
        }
        previewLayer.Children.Add(viewbox);

        var previewContainer = new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Height = PresetPreviewHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = previewLayer,
        };
        previewContainer.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _settingsStore.ApplyUserPreset(id);
            }
        };

        // --- Flyout with menu / rename / delete-confirm states ---
        var flyoutContent = new StackPanel { Spacing = 4, MinWidth = 180, Margin = new Thickness(2) };
        var flyout = new Flyout { Content = flyoutContent };

        void ShowMenu()
        {
            flyoutContent.Children.Clear();
            var overwriteBtn = new Button
            {
                Content = L("Preset_Action_Overwrite"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 2),
            };
            var renameBtn = new Button
            {
                Content = L("Preset_Action_Rename"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 2),
            };
            var deleteBtn = new Button
            {
                Content = L("Preset_Action_Delete"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            overwriteBtn.Click += (_, _) =>
            {
                flyout.Hide();
                _settingsStore.OverwriteUserPreset(id);
                BuildPresets();
            };
            renameBtn.Click += (_, _) => ShowRename();
            deleteBtn.Click += (_, _) => ShowDeleteConfirm();
            flyoutContent.Children.Add(overwriteBtn);
            flyoutContent.Children.Add(renameBtn);
            flyoutContent.Children.Add(deleteBtn);
        }

        void ShowRename()
        {
            flyoutContent.Children.Clear();
            var nameBox = new TextBox
            {
                Text = userPreset.Name,
                MaxLength = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var confirmBtn = new Button { Content = "✓", Padding = new Thickness(8, 4) };
            var cancelBtn = new Button { Content = "✗", Padding = new Thickness(8, 4) };
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4,
                Margin = new Thickness(0, 4, 0, 0),
            };
            btnRow.Children.Add(confirmBtn);
            btnRow.Children.Add(cancelBtn);
            void Confirm()
            {
                var newName = nameBox.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(newName)) return;
                flyout.Hide();
                _settingsStore.RenameUserPreset(id, newName);
                BuildPresets();
            }
            nameBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) Confirm();
                else if (e.Key == Key.Escape) ShowMenu();
            };
            confirmBtn.Click += (_, _) => Confirm();
            cancelBtn.Click += (_, _) => ShowMenu();
            flyoutContent.Children.Add(nameBox);
            flyoutContent.Children.Add(btnRow);
            nameBox.AttachedToVisualTree += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
        }

        void ShowDeleteConfirm()
        {
            flyoutContent.Children.Clear();
            var confirmText = new TextBlock
            {
                Text = L("Preset_Delete_Confirm"),
                Foreground = new SolidColorBrush(new Color(255, 220, 80, 80)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200,
                Margin = new Thickness(0, 0, 0, 6),
            };
            var confirmBtn = new Button { Content = "✓", Padding = new Thickness(8, 4) };
            var cancelBtn = new Button { Content = "✗", Padding = new Thickness(8, 4) };
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4,
            };
            btnRow.Children.Add(confirmBtn);
            btnRow.Children.Add(cancelBtn);
            confirmBtn.Click += (_, _) =>
            {
                flyout.Hide();
                _settingsStore.DeleteUserPreset(id);
                BuildPresets();
            };
            cancelBtn.Click += (_, _) => ShowMenu();
            flyoutContent.Children.Add(confirmText);
            flyoutContent.Children.Add(btnRow);
        }

        ShowMenu();

        // Options button (•••) that opens the flyout
        var optionsBtn = new Button
        {
            Content = "•••",
            Padding = new Thickness(6, 2),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        FlyoutBase.SetAttachedFlyout(optionsBtn, flyout);
        optionsBtn.Click += (_, _) =>
        {
            ShowMenu();
            FlyoutBase.ShowAttachedFlyout(optionsBtn);
        };

        // Bottom row: name left, options button right
        var nameLabel = new TextBlock
        {
            Text = userPreset.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 4, 0),
        };
        var bottomRow = new Grid();
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(nameLabel, 0);
        Grid.SetColumn(optionsBtn, 1);
        bottomRow.Children.Add(nameLabel);
        bottomRow.Children.Add(optionsBtn);

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(previewContainer);
        stack.Children.Add(bottomRow);

        var normalBg = new SolidColorBrush(new Color(255, 25, 28, 36));
        var hoverBg = new SolidColorBrush(new Color(255, 38, 43, 55));

        var card = new Border
        {
            Background = normalBg,
            BorderBrush = new SolidColorBrush(new Color(255, 52, 58, 68)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(3),
            Child = stack,
        };
        card.PointerEntered += (_, _) => card.Background = hoverBg;
        card.PointerExited += (_, _) => card.Background = normalBg;

        return card;
    }

    private Control CreateNewPresetCard()
    {
        var plusText = new TextBlock
        {
            Text = "+",
            FontSize = 36,
            FontWeight = FontWeight.Light,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var plusContainer = new Border
        {
            Height = PresetPreviewHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = plusText,
        };
        var label = new TextBlock
        {
            Text = L("Preset_NewCard_Placeholder"),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(plusContainer);
        stack.Children.Add(label);

        // Flyout with name input
        var nameBox = new TextBox
        {
            PlaceholderText = L("Preset_NewCard_NameHint"),
            MaxLength = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var confirmBtn = new Button { Content = "✓", Padding = new Thickness(8, 4) };
        var cancelBtn = new Button { Content = "✗", Padding = new Thickness(8, 4) };
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0),
        };
        btnRow.Children.Add(confirmBtn);
        btnRow.Children.Add(cancelBtn);
        var flyoutContent = new StackPanel { Spacing = 4, MinWidth = 180, Margin = new Thickness(2) };
        flyoutContent.Children.Add(nameBox);
        flyoutContent.Children.Add(btnRow);
        var flyout = new Flyout { Content = flyoutContent };

        void Confirm()
        {
            var name = nameBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) return;
            nameBox.Text = "";
            flyout.Hide();
            _settingsStore.CreateUserPreset(name);
            BuildPresets();
        }
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Confirm();
            else if (e.Key == Key.Escape) { nameBox.Text = ""; flyout.Hide(); }
        };
        confirmBtn.Click += (_, _) => Confirm();
        cancelBtn.Click += (_, _) => { nameBox.Text = ""; flyout.Hide(); };

        var normalBg = new SolidColorBrush(new Color(255, 18, 22, 30));
        var hoverBg = new SolidColorBrush(new Color(255, 30, 35, 46));

        var card = new Border
        {
            Background = normalBg,
            BorderBrush = new SolidColorBrush(new Color(255, 52, 58, 68)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(3),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = stack,
        };
        FlyoutBase.SetAttachedFlyout(card, flyout);
        card.PointerEntered += (_, _) => card.Background = hoverBg;
        card.PointerExited += (_, _) => card.Background = normalBg;
        card.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                nameBox.Text = "";
                FlyoutBase.ShowAttachedFlyout(card);
            }
        };
        flyout.Opened += (_, _) => nameBox.Focus();

        return card;
    }

    private void InitializeSearchSections()
    {
        _searchSections.Clear();
        _searchSections.Add(new SearchSection(GeneralTab, GeneralCard, ["general", "language", "idioma", "monitor", "monitors", "monitores", "display", "pantalla"]));
        _searchSections.Add(new SearchSection(GeneralTab, PresetsCard, ["preset", "presets", "preestablecido", "preestablecidos", "plantilla", "plantillas", "ajustes"]));
        _searchSections.Add(new SearchSection(AppearanceTab, CenterDotCard, ["center", "dot", "punto", "center dot", "punto central", "shape", "forma", "size", "tamano", "opacity", "opacidad", "color"]));
        _searchSections.Add(new SearchSection(AppearanceTab, DotGridCard, ["grid", "dot grid", "grilla", "puntos", "spacing", "espaciado", "rows", "filas", "columns", "columnas", "radius", "radio", "color"]));
        _searchSections.Add(new SearchSection(AppearanceTab, CrosshairCard, ["crosshair", "mira", "gap", "separacion", "thickness", "grosor", "length", "largo", "color", "opacity"]));
        _searchSections.Add(new SearchSection(MotionTab, MotionDetectionCard, ["motion", "movement", "movimiento", "detection", "deteccion", "capture", "captura", "fps", "smoothing", "suavizado", "dead zone", "zona muerta"]));
        _searchSections.Add(new SearchSection(DebugTab, DebugToolsCard, ["debug", "preview", "herramientas", "depuracion", "captured", "imagen", "overlay", "overlays", "config", "capture", "captura"]));
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
        DebugAllowConfigWindowCapture.Content = L("DebugAllowConfigWindowCapture");
        DebugAllowOverlayCapture.Content = L("DebugAllowOverlayCapture");

        PresetsTitle.Text = L("Presets");
        foreach (var (label, preset) in _presetLabels)
        {
            label.Text = L(preset.NameKey);
        }

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
            (true, "DebugAllowConfigWindowCapture") => "Permitir capturar la ventana de configuración",
            (true, "DebugAllowOverlayCapture") => "Permitir capturar los overlays",
            (true, "Presets") => "Preestablecidos",
            (true, "Preset_default-crosshair") => "Mira",
            (true, "Preset_default-dotgrid") => "Grilla de puntos",
            (true, "Preset_default-centerdot") => "Punto central",
            (true, "Preset_grid-sniper") => "Francotirador",
            (true, "Preset_Action_Overwrite") => "Actualizar con config actual",
            (true, "Preset_Action_Rename") => "Renombrar",
            (true, "Preset_Action_Delete") => "Eliminar",
            (true, "Preset_Delete_Confirm") => "¿Eliminar este preset?",
            (true, "Preset_NewCard_Placeholder") => "Nuevo preset",
            (true, "Preset_NewCard_NameHint") => "Nombre del preset",
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
            (false, "DebugAllowConfigWindowCapture") => "Allow capturing the config window",
            (false, "DebugAllowOverlayCapture") => "Allow capturing overlays",
            (false, "Presets") => "Presets",
            (false, "Preset_default-crosshair") => "Crosshair",
            (false, "Preset_default-dotgrid") => "Dot Grid",
            (false, "Preset_default-centerdot") => "Center Dot",
            (false, "Preset_grid-sniper") => "Grid Sniper",
            (false, "Preset_Action_Overwrite") => "Update with current config",
            (false, "Preset_Action_Rename") => "Rename",
            (false, "Preset_Action_Delete") => "Delete",
            (false, "Preset_Delete_Confirm") => "Delete this preset?",
            (false, "Preset_NewCard_Placeholder") => "New preset",
            (false, "Preset_NewCard_NameHint") => "Preset name",
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
