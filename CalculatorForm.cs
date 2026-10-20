using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Calculator;

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    private readonly bool _compositeChildren;

    public BufferedTableLayoutPanel()
        : this(false)
    {
    }

    public BufferedTableLayoutPanel(bool compositeChildren)
    {
        _compositeChildren = compositeChildren;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_compositeChildren) parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }

}

internal sealed class OutlinedLabel : Label
{
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool OutlineText { get; set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OutlineColor { get; set; } = Color.FromArgb(235, 18, 14, 20);

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!OutlineText)
        {
            base.OnPaint(e);
            return;
        }

        base.OnPaintBackground(e);
        if (string.IsNullOrEmpty(Text)) return;

        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;
        var bounds = ClientRectangle;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            if (offsetX == 0 && offsetY == 0) continue;
            TextRenderer.DrawText(e.Graphics, Text, Font,
                new Rectangle(bounds.X + offsetX, bounds.Y + offsetY, bounds.Width, bounds.Height), OutlineColor, flags);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

public sealed class CalculatorForm : Form
{
    private static readonly Regex NumericTokenRegex = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private readonly ExpressionEvaluator _evaluator = new();
    private readonly Bitmap _makimaBackdrop;
    private readonly Bitmap _tigakkiBackdrop;
    private readonly Bitmap _cardBackdrop;
    private readonly Bitmap _forestBackdrop;
    private readonly Bitmap _chainsBackdrop;
    private readonly Bitmap _joJoBackdrop;
    private readonly Bitmap _sunsetCatsBackdrop;
    private Icon? _windowIcon;
    private readonly OutlinedLabel _expressionLabel;
    private readonly OutlinedLabel _resultLabel;
    private readonly TableLayoutPanel _displayPanel;
    private readonly TableLayoutPanel _buttonGrid;
    private readonly TableLayoutPanel _scientificGrid;
    private readonly ToolTip _scientificToolTip;
    private readonly Button _scientificToggleButton;
    private bool _scientificPanelVisible;
    private readonly Button _pinButton;
    private readonly ToolTip _pinToolTip;
    private readonly Button _styleButton;
    private readonly ToolTip _styleToolTip;
    private readonly ContextMenuStrip _styleMenu;
    private readonly ToolStripMenuItem _themeMenuItem;
    private readonly ToolStripMenuItem _lightThemeItem;
    private readonly ToolStripMenuItem _darkThemeItem;
    private readonly ToolStripMenuItem _standardStyleItem;
    private readonly ToolStripMenuItem _makimaStyleItem;
    private readonly ToolStripMenuItem _tigakkiStyleItem;
    private readonly ToolStripMenuItem _cardStyleItem;
    private readonly ToolStripMenuItem _forestStyleItem;
    private readonly ToolStripMenuItem _chainsStyleItem;
    private readonly ToolStripMenuItem _joJoStyleItem;
    private readonly ToolStripMenuItem _sunsetCatsStyleItem;
    private AppTheme _activeTheme;
    private AppStyle _activeStyle;
    private string _expression = string.Empty;
    private bool _showingResult;

    private static readonly string ThemeSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Calculator",
        "theme.txt");
    private static readonly string StyleSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Calculator",
        "style.txt");

    public CalculatorForm()
    {
        _activeTheme = ReadInitialTheme();
        _activeStyle = ReadInitialStyle();
        _makimaBackdrop = CreateMakimaBackdrop();
        _tigakkiBackdrop = CreateTigakkiBackdrop();
        _cardBackdrop = CreateImageBackdrop("Calculator.Assets.Card.png", Color.FromArgb(18, 13, 18));
        _forestBackdrop = CreateImageBackdrop("Calculator.Assets.Forest.png", Color.FromArgb(23, 31, 28), verticalOffset: -130);
        _chainsBackdrop = CreateImageBackdrop("Calculator.Assets.Chains.png", Color.FromArgb(13, 13, 17));
        _joJoBackdrop = CreateImageBackdrop("Calculator.Assets.JoJo.png", Color.FromArgb(25, 17, 31), verticalOffset: -85);
        _sunsetCatsBackdrop = CreateContainedImageBackdrop("Calculator.Assets.SunsetCats.png",
            Color.FromArgb(246, 190, 153), Color.FromArgb(191, 104, 75));
        Text = "Калькулятор";
        Name = "CalculatorForm";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(330, 480);
        Size = new Size(420, 610);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(245, 247, 250);

        var root = new BufferedTableLayoutPanel(compositeChildren: true)
        {
            Name = "rootLayout",
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _displayPanel = new BufferedTableLayoutPanel
        {
            Name = "displayPanel",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4, 6, 4, 10)
        };
        _displayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _displayPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _displayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        _displayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        _displayPanel.Paint += DrawStyleAccents;

        var displayHeader = new FlowLayoutPanel
        {
            Name = "displayHeader",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        _pinButton = new Button
        {
            Name = "alwaysOnTopButton",
            Text = "📌",
            Size = new Size(42, 40),
            MinimumSize = new Size(42, 40),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(232, 235, 240),
            ForeColor = Color.FromArgb(35, 42, 52),
            Font = new Font("Segoe UI Emoji", 13F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter,
            TabStop = false,
            AccessibleRole = AccessibleRole.CheckButton,
            AccessibleName = "Закрепить окно поверх всех окон",
            AccessibleDescription = "Переключает режим отображения поверх остальных окон"
        };
        _pinButton.FlatAppearance.BorderSize = 0;
        _pinToolTip = new ToolTip();
        _pinToolTip.SetToolTip(_pinButton, "Закрепить окно поверх всех окон");
        _pinButton.Click += ToggleAlwaysOnTop;
        displayHeader.Controls.Add(_pinButton);

        _styleButton = new Button
        {
            Name = "appearanceProfileButton",
            Size = new Size(42, 40),
            Margin = new Padding(6, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleName = "Настройки оформления"
        };
        _styleButton.FlatAppearance.BorderSize = 0;
        _styleButton.Paint += DrawStyleButtonIcon;
        _styleToolTip = new ToolTip();
        _styleToolTip.SetToolTip(_styleButton, "Тема и стиль оформления");
        _styleMenu = new ContextMenuStrip();
        _themeMenuItem = new ToolStripMenuItem("Цветовая тема");
        _lightThemeItem = new ToolStripMenuItem("Светлая");
        _darkThemeItem = new ToolStripMenuItem("Тёмная");
        _standardStyleItem = new ToolStripMenuItem("Стандартный");
        _makimaStyleItem = new ToolStripMenuItem("Макима");
        _tigakkiStyleItem = new ToolStripMenuItem("Фурикомпаратор");
        _cardStyleItem = new ToolStripMenuItem("Красная карта");
        _forestStyleItem = new ToolStripMenuItem("Лесной портрет");
        _chainsStyleItem = new ToolStripMenuItem("Цепи");
        _joJoStyleItem = new ToolStripMenuItem("ДжоДжо");
        _sunsetCatsStyleItem = new ToolStripMenuItem("Кошки на закате");
        _themeMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _lightThemeItem, _darkThemeItem });
        _styleMenu.Items.Add(_themeMenuItem);
        _styleMenu.Items.Add(new ToolStripSeparator());
        _styleMenu.Items.Add(_standardStyleItem);
        _styleMenu.Items.Add(_makimaStyleItem);
        _styleMenu.Items.Add(_tigakkiStyleItem);
        _styleMenu.Items.Add(new ToolStripSeparator());
        _styleMenu.Items.Add(_cardStyleItem);
        _styleMenu.Items.Add(_forestStyleItem);
        _styleMenu.Items.Add(_chainsStyleItem);
        _styleMenu.Items.Add(_joJoStyleItem);
        _styleMenu.Items.Add(_sunsetCatsStyleItem);
        _lightThemeItem.Click += (_, _) => SelectTheme(AppTheme.Light);
        _darkThemeItem.Click += (_, _) => SelectTheme(AppTheme.Dark);
        _standardStyleItem.Click += (_, _) => SelectStyle(AppStyle.Standard);
        _makimaStyleItem.Click += (_, _) => SelectStyle(AppStyle.Makima);
        _tigakkiStyleItem.Click += (_, _) => SelectStyle(AppStyle.Tigakki);
        _cardStyleItem.Click += (_, _) => SelectStyle(AppStyle.Card);
        _forestStyleItem.Click += (_, _) => SelectStyle(AppStyle.Forest);
        _chainsStyleItem.Click += (_, _) => SelectStyle(AppStyle.Chains);
        _joJoStyleItem.Click += (_, _) => SelectStyle(AppStyle.JoJo);
        _sunsetCatsStyleItem.Click += (_, _) => SelectStyle(AppStyle.SunsetCats);
        _styleButton.Click += (_, _) => _styleMenu.Show(_styleButton, new Point(0, _styleButton.Height));
        displayHeader.Controls.Add(_styleButton);

        _expressionLabel = new OutlinedLabel
        {
            Name = "expressionLabel",
            Dock = DockStyle.Fill,
            Text = "0",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 22F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(35, 42, 52),
            AutoEllipsis = true,
            AccessibleName = "Текущее выражение"
        };
        _resultLabel = new OutlinedLabel
        {
            Name = "resultLabel",
            Dock = DockStyle.Fill,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(95, 105, 117),
            AutoEllipsis = true,
            AccessibleName = "Результат вычисления"
        };
        _displayPanel.Controls.Add(displayHeader, 0, 0);
        _displayPanel.Controls.Add(_expressionLabel, 0, 1);
        _displayPanel.Controls.Add(_resultLabel, 0, 2);
        root.Controls.Add(_displayPanel, 0, 0);

        _buttonGrid = new BufferedTableLayoutPanel
        {
            Name = "buttonGrid",
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 7,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = BackColor
        };
        for (var column = 0; column < 4; column++)
            _buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (var row = 0; row < 7; row++)
            _buttonGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 7));
        root.Controls.Add(_buttonGrid, 0, 2);

        _scientificGrid = new BufferedTableLayoutPanel
        {
            Name = "scientificButtonGrid",
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0, 3, 0, 0),
            BackColor = BackColor,
            Visible = false
        };
        for (var column = 0; column < 5; column++)
            _scientificGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        _scientificGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_scientificGrid, 0, 1);
        AddScientificButton("sin", 0);
        AddScientificButton("cos", 1);
        AddScientificButton("x²", 2);
        AddScientificButton("x!", 3);
        AddScientificButton("√", 4);

        _scientificToolTip = new ToolTip();
        _scientificToggleButton = new Button
        {
            Name = "scientificToggleButton",
            Text = "⋯",
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 17F, FontStyle.Regular, GraphicsUnit.Point),
            TabStop = false,
            AccessibleRole = AccessibleRole.CheckButton,
            AccessibleName = "Показать дополнительные функции",
            Tag = ButtonStyle.Operator
        };
        _scientificToggleButton.FlatAppearance.BorderSize = 0;
        _scientificToggleButton.Click += (_, _) => ToggleScientificPanel();
        _buttonGrid.Controls.Add(_scientificToggleButton, 0, 1);

        AddButton("C", 0, 0, ButtonAction.Clear, ButtonStyle.Utility);
        AddButton("⌫", 1, 0, ButtonAction.Backspace, ButtonStyle.Utility);
        AddButton("(", 2, 0, ButtonAction.Insert, ButtonStyle.Utility);
        AddButton(")", 3, 0, ButtonAction.Insert, ButtonStyle.Utility);
        AddButton("xʸ", 1, 1, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("±", 2, 1, ButtonAction.ToggleSign, ButtonStyle.Utility);
        AddButton("%", 3, 1, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("7", 0, 2, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("8", 1, 2, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("9", 2, 2, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("÷", 3, 2, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("4", 0, 3, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("5", 1, 3, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("6", 2, 3, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("×", 3, 3, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("1", 0, 4, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("2", 1, 4, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("3", 2, 4, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("−", 3, 4, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("00", 0, 5, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("0", 1, 5, ButtonAction.Insert, ButtonStyle.Number);
        AddButton(",", 2, 5, ButtonAction.Insert, ButtonStyle.Number);
        AddButton("+", 3, 5, ButtonAction.Insert, ButtonStyle.Operator);
        AddButton("=", 0, 6, ButtonAction.Equals, ButtonStyle.Equals, columnSpan: 4);

        KeyDown += HandleKeyDown;
        KeyPress += HandleKeyPress;
        FormClosed += (_, _) =>
        {
            _makimaBackdrop.Dispose();
            _tigakkiBackdrop.Dispose();
            _cardBackdrop.Dispose();
            _forestBackdrop.Dispose();
            _chainsBackdrop.Dispose();
            _joJoBackdrop.Dispose();
            _sunsetCatsBackdrop.Dispose();
            _windowIcon?.Dispose();
        };
        HandleCreated += (_, _) => ApplyWindowChrome();
        ApplyTheme();
    }

    private void AddButton(string caption, int column, int row, ButtonAction action, ButtonStyle style, int columnSpan = 1)
    {
        var button = new Button
        {
            Name = caption switch
            {
                "⌫" => "backspaceButton",
                "±" => "signButton",
                "=" => "equalsButton",
                "C" => "clearButton",
                _ => "button" + caption
            },
            Text = caption,
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", caption == "=" ? 16F : 14F, FontStyle.Regular, GraphicsUnit.Point),
            TabStop = false,
            AccessibleRole = AccessibleRole.PushButton,
            Tag = style
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 232, 241);
        button.BackColor = style switch
        {
            ButtonStyle.Equals => Color.FromArgb(35, 112, 210),
            ButtonStyle.Operator => Color.FromArgb(228, 237, 248),
            ButtonStyle.Utility => Color.FromArgb(232, 235, 240),
            _ => Color.White
        };
        button.ForeColor = style == ButtonStyle.Equals ? Color.White : Color.FromArgb(35, 42, 52);
        if (style == ButtonStyle.Equals) button.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 94, 183);

        button.Click += (_, _) => ApplyAction(action, caption);
        _buttonGrid.Controls.Add(button, column, row);
        if (columnSpan > 1) _buttonGrid.SetColumnSpan(button, columnSpan);
    }

    private void AddScientificButton(string caption, int column)
    {
        var button = new Button
        {
            Name = "scientific" + caption,
            Text = caption,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
            TabStop = false,
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleName = caption switch
            {
                "sin" => "Синус угла в градусах",
                "cos" => "Косинус угла в градусах",
                "x²" => "Возвести выражение в квадрат",
                "x!" => "Факториал",
                "√" => "Квадратный корень",
                _ => caption
            },
            Tag = ButtonStyle.Operator
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => ApplyAction(ButtonAction.Insert, caption);
        _scientificGrid.Controls.Add(button, column, 0);
    }

    private void ToggleScientificPanel()
    {
        _scientificPanelVisible = !_scientificPanelVisible;
        var root = (TableLayoutPanel)_scientificGrid.Parent!;
        SuspendLayout();
        root.SuspendLayout();
        try
        {
            root.RowStyles[1].Height = _scientificPanelVisible ? 58 : 0;
            root.RowStyles[1].SizeType = SizeType.Absolute;
            _scientificGrid.Visible = _scientificPanelVisible;
            Height += _scientificPanelVisible ? 58 : -58;
            _scientificToggleButton.Text = _scientificPanelVisible ? "⌃" : "⋯";
        }
        finally
        {
            root.ResumeLayout(performLayout: true);
            ResumeLayout(performLayout: true);
        }
        var description = _scientificPanelVisible
            ? "Скрыть дополнительные функции"
            : "Показать дополнительные функции";
        _scientificToggleButton.AccessibleName = description;
        _scientificToolTip.SetToolTip(_scientificToggleButton, description);
    }

    private void ToggleAlwaysOnTop(object? sender, EventArgs e)
    {
        TopMost = !TopMost;
        _pinButton.AccessibleName = TopMost
            ? "Открепить окно от режима поверх всех окон"
            : "Закрепить окно поверх всех окон";
        _pinToolTip.SetToolTip(_pinButton, TopMost
            ? "Открепить окно от режима поверх всех окон"
            : "Закрепить окно поверх всех окон");
        ApplyTheme();
    }

    private void SelectTheme(AppTheme theme)
    {
        _activeTheme = theme;
        ApplyTheme();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemeSettingsPath)!);
            File.WriteAllText(ThemeSettingsPath, _activeTheme.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(
                this,
                "Профиль применён, но сохранить выбор не удалось:\n" + ex.Message,
                "Настройки калькулятора",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SelectStyle(AppStyle style)
    {
        _activeStyle = style;
        ApplyTheme();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StyleSettingsPath)!);
            File.WriteAllText(StyleSettingsPath, _activeStyle.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, "Стиль применён, но сохранить выбор не удалось:\n" + ex.Message,
                "Настройки калькулятора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTheme()
    {
        var palette = CurrentPalette;
        BackColor = palette.Background;
        BackgroundImage = _activeStyle switch
        {
            AppStyle.Makima => _makimaBackdrop,
            AppStyle.Tigakki => _tigakkiBackdrop,
            AppStyle.Card => _cardBackdrop,
            AppStyle.Forest => _forestBackdrop,
            AppStyle.Chains => _chainsBackdrop,
            AppStyle.JoJo => _joJoBackdrop,
            AppStyle.SunsetCats => _sunsetCatsBackdrop,
            _ => null
        };
        BackgroundImageLayout = ImageLayout.Zoom;
        ForeColor = palette.Text;
        _expressionLabel.OutlineText = _activeStyle != AppStyle.Standard;
        _resultLabel.OutlineText = _activeStyle != AppStyle.Standard;
        ApplyThemeToControl(this, palette);

        var themeName = _activeTheme == AppTheme.Light ? "светлая" : "тёмная";
        _styleButton.Text = string.Empty;
        _styleButton.BackColor = palette.UtilityButton;
        _styleButton.ForeColor = palette.Text;
        _styleButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
        _styleButton.FlatAppearance.BorderSize = 0;
        _styleButton.FlatAppearance.BorderColor = palette.Accent;
        var styleName = _activeStyle switch
        {
            AppStyle.Makima => "Макима",
            AppStyle.Tigakki => "Фурикомпаратор",
            AppStyle.Card => "Красная карта",
            AppStyle.Forest => "Лесной портрет",
            AppStyle.Chains => "Цепи",
            AppStyle.JoJo => "ДжоДжо",
            AppStyle.SunsetCats => "Кошки на закате",
            _ => "Стандартный"
        };
        _styleButton.AccessibleName = $"Настройки оформления: {themeName} тема, стиль «{styleName}». Нажмите, чтобы изменить.";
        _styleToolTip.SetToolTip(_styleButton, $"Тема: {themeName}. Стиль: {styleName}. Нажмите, чтобы изменить.");
        _styleButton.Invalidate();
        _styleMenu.BackColor = palette.Background;
        _styleMenu.ForeColor = palette.Text;
        _themeMenuItem.BackColor = palette.Background;
        _themeMenuItem.ForeColor = palette.Text;
        _lightThemeItem.BackColor = palette.Background;
        _lightThemeItem.ForeColor = palette.Text;
        _darkThemeItem.BackColor = palette.Background;
        _darkThemeItem.ForeColor = palette.Text;
        _lightThemeItem.Checked = _activeTheme == AppTheme.Light;
        _darkThemeItem.Checked = _activeTheme == AppTheme.Dark;
        _standardStyleItem.BackColor = palette.Background;
        _standardStyleItem.ForeColor = palette.Text;
        _makimaStyleItem.BackColor = palette.Background;
        _makimaStyleItem.ForeColor = palette.Text;
        _tigakkiStyleItem.BackColor = palette.Background;
        _tigakkiStyleItem.ForeColor = palette.Text;
        _cardStyleItem.BackColor = palette.Background;
        _cardStyleItem.ForeColor = palette.Text;
        _forestStyleItem.BackColor = palette.Background;
        _forestStyleItem.ForeColor = palette.Text;
        _chainsStyleItem.BackColor = palette.Background;
        _chainsStyleItem.ForeColor = palette.Text;
        _joJoStyleItem.BackColor = palette.Background;
        _joJoStyleItem.ForeColor = palette.Text;
        _sunsetCatsStyleItem.BackColor = palette.Background;
        _sunsetCatsStyleItem.ForeColor = palette.Text;
        _standardStyleItem.Checked = _activeStyle == AppStyle.Standard;
        _makimaStyleItem.Checked = _activeStyle == AppStyle.Makima;
        _tigakkiStyleItem.Checked = _activeStyle == AppStyle.Tigakki;
        _cardStyleItem.Checked = _activeStyle == AppStyle.Card;
        _forestStyleItem.Checked = _activeStyle == AppStyle.Forest;
        _chainsStyleItem.Checked = _activeStyle == AppStyle.Chains;
        _joJoStyleItem.Checked = _activeStyle == AppStyle.JoJo;
        _sunsetCatsStyleItem.Checked = _activeStyle == AppStyle.SunsetCats;

        _pinButton.BackColor = TopMost ? palette.EqualsButton : palette.UtilityButton;
        _pinButton.ForeColor = TopMost ? Color.White : palette.Text;
        _pinButton.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
        _pinButton.FlatAppearance.BorderSize = 0;
        _pinButton.FlatAppearance.BorderColor = palette.Accent;
        _displayPanel.Invalidate();
        UpdateWindowIcon();
        ApplyWindowChrome();
    }

    private void ApplyWindowChrome()
    {
        if (!IsHandleCreated) return;

        var palette = CurrentPalette;
        var captionColor = palette.Background;
        var borderColor = palette.HasAccents ? palette.Accent : palette.Background;
        var useDarkTitleBar = _activeStyle != AppStyle.Standard || _activeTheme == AppTheme.Dark;

        SetDwmAttribute(20, useDarkTitleBar ? 1 : 0); // DWMWA_USE_IMMERSIVE_DARK_MODE
        SetDwmAttribute(34, ToColorRef(borderColor)); // DWMWA_BORDER_COLOR
        SetDwmAttribute(35, ToColorRef(captionColor)); // DWMWA_CAPTION_COLOR
        SetDwmAttribute(36, ToColorRef(palette.Text)); // DWMWA_TEXT_COLOR
    }

    private void SetDwmAttribute(int attribute, int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(Handle, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Keep the normal Windows title bar on systems without Desktop Window Manager.
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows versions ignore custom title bar colors.
        }
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);

    private void UpdateWindowIcon()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            if (_activeStyle == AppStyle.Makima)
            {
                using var badge = CreateRoundedRectangle(new RectangleF(1.5F, 1.5F, 29, 29), 7);
                using var badgeFill = new LinearGradientBrush(
                    new Rectangle(1, 1, 30, 30), Color.FromArgb(83, 35, 48), Color.FromArgb(23, 14, 19), 45F);
                using var badgeBorder = new Pen(Color.FromArgb(215, 177, 103), 1.3F);
                using var innerRing = new Pen(Color.FromArgb(95, 215, 177, 103), 0.8F);
                graphics.FillPath(badgeFill, badge);
                graphics.DrawPath(badgeBorder, badge);
                graphics.DrawEllipse(innerRing, 5, 5, 22, 22);

                using var eye = new GraphicsPath();
                eye.AddBezier(4, 16, 9, 9, 23, 9, 28, 16);
                eye.AddBezier(28, 16, 23, 23, 9, 23, 4, 16);
                using var sclera = new SolidBrush(Color.FromArgb(246, 230, 207));
                using var outline = new Pen(Color.FromArgb(47, 18, 24), 2.2F);
                using var iris = new SolidBrush(Color.FromArgb(232, 112, 35));
                using var irisRing = new Pen(Color.FromArgb(123, 33, 37), 1.8F);
                using var goldRing = new Pen(Color.FromArgb(255, 190, 72), 1.2F);
                using var pupil = new SolidBrush(Color.FromArgb(31, 17, 21));
                graphics.FillPath(sclera, eye);
                graphics.DrawPath(outline, eye);
                var clipState = graphics.Save();
                graphics.SetClip(eye);
                graphics.FillEllipse(iris, 11, 8, 10, 17);
                graphics.DrawEllipse(irisRing, 12, 9, 8, 15);
                graphics.DrawEllipse(goldRing, 13, 11, 6, 11);
                graphics.FillEllipse(pupil, 14.5F, 10, 3, 13);
                graphics.Restore(clipState);
                using var lashes = new Pen(Color.FromArgb(36, 17, 22), 2.1F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                graphics.DrawLine(lashes, 8, 11, 5, 8);
                graphics.DrawLine(lashes, 13, 9, 12, 6);
                graphics.DrawLine(lashes, 19, 9, 20, 6);
                graphics.DrawLine(lashes, 24, 11, 27, 8);
            }
            else if (_activeStyle == AppStyle.Tigakki)
            {
                using var badge = CreateRoundedRectangle(new RectangleF(1.5F, 1.5F, 29, 29), 7);
                using var badgeFill = new LinearGradientBrush(
                    new Rectangle(1, 1, 30, 30), Color.FromArgb(57, 48, 61), Color.FromArgb(24, 21, 29), 45F);
                using var badgeBorder = new Pen(Color.FromArgb(236, 151, 67), 1.3F);
                graphics.FillPath(badgeFill, badge);
                graphics.DrawPath(badgeBorder, badge);

                using var tire = new SolidBrush(Color.FromArgb(30, 26, 35));
                using var tireEdge = new Pen(Color.FromArgb(103, 87, 105), 1.2F);
                using var rim = new SolidBrush(Color.FromArgb(239, 143, 56));
                using var rimEdge = new Pen(Color.FromArgb(255, 198, 113), 1.1F);
                using var hub = new SolidBrush(Color.FromArgb(249, 231, 220));
                graphics.FillEllipse(tire, 5, 5, 22, 22);
                graphics.DrawEllipse(tireEdge, 5, 5, 22, 22);
                graphics.FillEllipse(rim, 9, 9, 14, 14);
                graphics.DrawEllipse(rimEdge, 9, 9, 14, 14);
                graphics.FillEllipse(hub, 13, 13, 6, 6);
            }
            else
            {
                var palette = CurrentPalette;
                var iconStart = _activeStyle == AppStyle.Standard ? Color.FromArgb(65, 96, 137) : palette.Background;
                var iconEnd = _activeStyle == AppStyle.Standard ? Color.FromArgb(25, 37, 56) : palette.Surface;
                var iconBorder = _activeStyle == AppStyle.Standard ? Color.FromArgb(177, 204, 232) : palette.Accent;
                using var badge = CreateRoundedRectangle(new RectangleF(1.5F, 1.5F, 29, 29), 7);
                using var badgeFill = new LinearGradientBrush(
                    new Rectangle(1, 1, 30, 30), iconStart, iconEnd, 45F);
                using var badgeBorder = new Pen(iconBorder, 1.3F);
                graphics.FillPath(badgeFill, badge);
                graphics.DrawPath(badgeBorder, badge);

                using var body = CreateRoundedRectangle(new RectangleF(8, 4, 16, 24), 3);
                using var bodyFill = new SolidBrush(Color.FromArgb(244, 239, 225));
                using var outline = new Pen(Color.FromArgb(22, 34, 51), 1.4F);
                graphics.FillPath(bodyFill, body);
                graphics.DrawPath(outline, body);
                using var display = new SolidBrush(_activeStyle == AppStyle.Standard ? Color.FromArgb(34, 103, 170) : palette.EqualsButton);
                graphics.FillRectangle(display, 11, 7, 10, 5);
                using var keyBlue = new SolidBrush(_activeStyle == AppStyle.Standard ? Color.FromArgb(91, 139, 190) : palette.Accent);
                using var keyRed = new SolidBrush(Color.FromArgb(190, 75, 73));
                foreach (var key in new[]
                {
                    new PointF(11, 17), new PointF(16, 17), new PointF(21, 17),
                    new PointF(11, 21), new PointF(16, 21), new PointF(21, 21)
                })
                    graphics.FillEllipse(keyBlue, key.X - 1.3F, key.Y - 1.3F, 2.6F, 2.6F);
                graphics.FillEllipse(keyBlue, 11 - 1.3F, 25 - 1.3F, 2.6F, 2.6F);
                graphics.FillEllipse(keyBlue, 16 - 1.3F, 25 - 1.3F, 2.6F, 2.6F);
                graphics.FillEllipse(keyRed, 21 - 1.3F, 25 - 1.3F, 2.6F, 2.6F);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            var icon = (Icon)Icon.FromHandle(handle).Clone();
            var previousIcon = _windowIcon;
            _windowIcon = icon;
            Icon = icon;
            previousIcon?.Dispose();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawStyleButtonIcon(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var palette = CurrentPalette;
        using var fill = new SolidBrush(_activeStyle == AppStyle.Tigakki
            ? Color.FromArgb(229, 132, 42)
            : palette.EqualsButton);
        using var outline = new Pen(palette.HasAccents ? palette.Accent : palette.Text, 1.5F);
        if (_activeStyle == AppStyle.Makima)
        {
            using var eye = new GraphicsPath();
            eye.AddBezier(6, 19, 10, 12, 22, 12, 26, 19);
            eye.AddBezier(26, 19, 22, 26, 10, 26, 6, 19);
            using var sclera = new SolidBrush(Color.FromArgb(246, 230, 207));
            using var eyeOutline = new Pen(Color.FromArgb(51, 20, 25), 1.4F);
            using var iris = new SolidBrush(Color.FromArgb(221, 93, 42));
            using var irisRing = new Pen(Color.FromArgb(122, 38, 34), 1.2F);
            using var goldRing = new Pen(Color.FromArgb(244, 172, 69), 1F);
            using var pupil = new SolidBrush(Color.FromArgb(31, 17, 21));
            graphics.FillPath(sclera, eye);
            graphics.DrawPath(eyeOutline, eye);
            var oldClip = graphics.Clip;
            graphics.SetClip(eye);
            graphics.FillEllipse(iris, 12, 13, 8, 12);
            graphics.DrawEllipse(irisRing, 12.5F, 13.5F, 7, 11);
            graphics.DrawEllipse(goldRing, 13.5F, 15, 5, 8);
            graphics.FillEllipse(pupil, 15, 15.5F, 2.5F, 7);
            graphics.Clip = oldClip;
            oldClip.Dispose();
            using var lashPen = new Pen(Color.FromArgb(36, 17, 22), 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(lashPen, 8, 14, 6, 12);
            graphics.DrawLine(lashPen, 12, 12, 11, 9);
            graphics.DrawLine(lashPen, 17, 12, 17, 9);
            graphics.DrawLine(lashPen, 22, 13, 24, 11);
        }
        else if (_activeStyle == AppStyle.Tigakki)
        {
            using var leftEar = new GraphicsPath();
            leftEar.AddPolygon(new[] { new PointF(10, 13), new PointF(10, 5), new PointF(16, 12) });
            using var rightEar = new GraphicsPath();
            rightEar.AddPolygon(new[] { new PointF(20, 12), new PointF(27, 5), new PointF(25, 15) });
            graphics.FillPath(fill, leftEar);
            graphics.FillPath(fill, rightEar);
            graphics.DrawPath(outline, leftEar);
            graphics.DrawPath(outline, rightEar);

            using var tire = new SolidBrush(Color.FromArgb(34, 28, 38));
            using var tireEdge = new Pen(Color.FromArgb(239, 143, 56), 1.4F);
            using var rim = new SolidBrush(Color.FromArgb(239, 143, 56));
            using var hub = new SolidBrush(Color.FromArgb(249, 231, 220));
            graphics.FillEllipse(tire, 8, 11, 20, 20);
            graphics.DrawEllipse(tireEdge, 8, 11, 20, 20);
            graphics.FillEllipse(rim, 12, 15, 12, 12);
            graphics.FillEllipse(hub, 16, 19, 4, 4);
            using var eyePen = new Pen(Color.FromArgb(255, 206, 116), 1.2F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(eyePen, 12, 16, 15, 17);
            graphics.DrawLine(eyePen, 21, 17, 24, 16);
        }
        else if (_activeStyle == AppStyle.Card)
        {
            using var cardPath = new GraphicsPath();
            cardPath.AddPolygon(new[]
            {
                new PointF(10, 11), new PointF(23, 8), new PointF(27, 27), new PointF(14, 30)
            });
            using var cardFill = new SolidBrush(Color.FromArgb(246, 233, 226));
            using var cardEdge = new Pen(Color.FromArgb(151, 38, 55), 1.6F);
            graphics.FillPath(cardFill, cardPath);
            graphics.DrawPath(cardEdge, cardPath);
            using var cardMark = new SolidBrush(Color.FromArgb(190, 39, 61));
            graphics.FillPolygon(cardMark, new[]
            {
                new PointF(18.5F, 13), new PointF(21, 17), new PointF(18.5F, 21), new PointF(16, 17)
            });
        }
        else if (_activeStyle == AppStyle.Forest)
        {
            using var leaf = new GraphicsPath();
            leaf.AddBezier(7, 27, 7, 13, 16, 7, 26, 8);
            leaf.AddBezier(26, 8, 27, 19, 20, 29, 7, 27);
            using var leafFill = new SolidBrush(Color.FromArgb(176, 203, 137));
            using var leafEdge = new Pen(Color.FromArgb(42, 78, 51), 1.5F);
            graphics.FillPath(leafFill, leaf);
            graphics.DrawPath(leafEdge, leaf);
            using var vein = new Pen(Color.FromArgb(42, 78, 51), 1.3F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawBezier(vein, 8, 26, 13, 21, 18, 15, 25, 9);
        }
        else if (_activeStyle == AppStyle.Chains)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(18, 20);
            graphics.RotateTransform(-42);
            using var chainPen = new Pen(Color.FromArgb(231, 224, 215), 3F)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawEllipse(chainPen, -11, -5, 15, 10);
            graphics.DrawEllipse(chainPen, -2, -5, 15, 10);
            graphics.Restore(state);
        }
        else if (_activeStyle == AppStyle.JoJo)
        {
            const float centerX = 18F;
            const float centerY = 20F;
            using var star = new GraphicsPath();
            var points = new PointF[10];
            for (var point = 0; point < points.Length; point++)
            {
                var radius = point % 2 == 0 ? 12F : 5.2F;
                var angle = -Math.PI / 2 + point * Math.PI / 5;
                points[point] = new PointF(
                    centerX + (float)Math.Cos(angle) * radius,
                    centerY + (float)Math.Sin(angle) * radius);
            }
            star.AddPolygon(points);
            using var starFill = new SolidBrush(Color.FromArgb(155, 76, 219));
            using var starEdge = new Pen(Color.FromArgb(232, 196, 255), 1.6F);
            graphics.FillPath(starFill, star);
            graphics.DrawPath(starEdge, star);
        }
        else if (_activeStyle == AppStyle.SunsetCats)
        {
            using var earFill = new SolidBrush(Color.FromArgb(205, 102, 72));
            using var faceFill = new SolidBrush(Color.FromArgb(255, 220, 190));
            using var catOutline = new Pen(Color.FromArgb(103, 52, 48), 1.4F);
            using var leftEar = new GraphicsPath();
            leftEar.AddPolygon(new[] { new PointF(10, 18), new PointF(11, 8), new PointF(18, 14) });
            using var rightEar = new GraphicsPath();
            rightEar.AddPolygon(new[] { new PointF(19, 14), new PointF(26, 8), new PointF(26, 19) });
            graphics.FillPath(earFill, leftEar);
            graphics.FillPath(earFill, rightEar);
            graphics.DrawPath(catOutline, leftEar);
            graphics.DrawPath(catOutline, rightEar);
            graphics.FillEllipse(faceFill, 10, 13, 16, 16);
            graphics.DrawEllipse(catOutline, 10, 13, 16, 16);
            using var eyeBrush = new SolidBrush(Color.FromArgb(91, 46, 45));
            graphics.FillEllipse(eyeBrush, 14, 19, 2, 3);
            graphics.FillEllipse(eyeBrush, 20, 19, 2, 3);
            using var noseBrush = new SolidBrush(Color.FromArgb(197, 92, 84));
            graphics.FillEllipse(noseBrush, 17, 23, 2.5F, 2);
        }
        else
        {
            const float x = 12F;
            const float y = 8F;
            const float width = 13F;
            const float height = 24F;
            using var body = new GraphicsPath();
            body.AddArc(x, y, 4, 4, 180, 90);
            body.AddArc(x + width - 4, y, 4, 4, 270, 90);
            body.AddArc(x + width - 4, y + height - 4, 4, 4, 0, 90);
            body.AddArc(x, y + height - 4, 4, 4, 90, 90);
            body.CloseFigure();
            using var bodyFill = new SolidBrush(palette.Surface);
            graphics.FillPath(bodyFill, body);
            graphics.DrawPath(outline, body);

            using var display = new SolidBrush(palette.EqualsButton);
            graphics.FillRectangle(display, 14, 11, 9, 4);
            using var keyBrush = new SolidBrush(palette.Text);
            foreach (var key in new[]
            {
                new PointF(15, 19), new PointF(19, 19), new PointF(23, 19),
                new PointF(15, 23), new PointF(19, 23), new PointF(23, 23),
                new PointF(15, 27), new PointF(19, 27), new PointF(23, 27)
            })
                graphics.FillEllipse(keyBrush, key.X - 1, key.Y - 1, 2, 2);
        }

        var themeCenter = new PointF(31, 11);
        using var themeBrush = new SolidBrush(palette.Text);
        if (_activeTheme == AppTheme.Light)
        {
            using var rayPen = new Pen(palette.Text, 1.2F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (var ray = 0; ray < 8; ray++)
            {
                var angle = ray * Math.PI / 4;
                graphics.DrawLine(rayPen,
                    themeCenter.X + (float)Math.Cos(angle) * 4.8F,
                    themeCenter.Y + (float)Math.Sin(angle) * 4.8F,
                    themeCenter.X + (float)Math.Cos(angle) * 7F,
                    themeCenter.Y + (float)Math.Sin(angle) * 7F);
            }
            graphics.FillEllipse(themeBrush, themeCenter.X - 2.6F, themeCenter.Y - 2.6F, 5.2F, 5.2F);
        }
        else
        {
            graphics.FillEllipse(themeBrush, themeCenter.X - 5F, themeCenter.Y - 5F, 10F, 10F);
            using var cutout = new SolidBrush(_styleButton.BackColor);
            graphics.FillEllipse(cutout, themeCenter.X - 0.5F, themeCenter.Y - 7F, 9F, 9F);
        }
    }

    private void DrawStyleAccents(object? sender, PaintEventArgs e)
    {
        var palette = CurrentPalette;
        if (!palette.HasAccents) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var y = _displayPanel.ClientSize.Height - 7;
        using var ringPen = new Pen(Color.FromArgb(145, palette.Accent), 1.2F);
        g.DrawEllipse(ringPen, 22, y - 4, 8, 8);
        g.DrawEllipse(ringPen, 29, y - 4, 8, 8);
    }

    private static Bitmap CreateMakimaBackdrop()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Calculator.Assets.Makima.png")
            ?? throw new InvalidOperationException("Не удалось загрузить изображение стиля «Макима».");
        using var original = Image.FromStream(stream);

        const int backdropWidth = 500;
        const int backdropHeight = 726;
        var backdrop = new Bitmap(backdropWidth, backdropHeight, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(backdrop);
        graphics.Clear(Color.FromArgb(20, 15, 19));
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Crop the dark gray frame baked into the source image so it cannot form a seam
        // against the calculator's own dark backdrop.
        var imageCrop = new Rectangle(5, 5, original.Width - 27, original.Height - 10);
        var imageWidth = (int)Math.Round(backdropHeight * (double)imageCrop.Width / imageCrop.Height);
        var imageLeft = (backdropWidth - imageWidth) / 2;

        using (var attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(new ColorMatrix(new[]
            {
                new[] { 0.98f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0.83f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0.82f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0.93f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f }
            }));
            graphics.DrawImage(original,
                new Rectangle(imageLeft, 0, imageWidth, backdrop.Height),
                imageCrop.X, imageCrop.Y, imageCrop.Width, imageCrop.Height,
                GraphicsUnit.Pixel, attributes);
        }

        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(18, 72, backdrop.Width - 36, 510);
        using var glow = new PathGradientBrush(glowPath)
        {
            CenterColor = Color.FromArgb(58, 196, 42, 57),
            SurroundColors = new[] { Color.FromArgb(0, 196, 42, 57) }
        };
        graphics.FillPath(glow, glowPath);
        return backdrop;
    }

    private static Bitmap CreateTigakkiBackdrop()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Calculator.Assets.Tigakki.png")
            ?? throw new InvalidOperationException("Не удалось загрузить изображение темы «Фурикомпаратор».");
        using var original = Image.FromStream(stream);

        const int backdropWidth = 500;
        const int backdropHeight = 726;
        var backdrop = new Bitmap(backdropWidth, backdropHeight, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(backdrop);
        graphics.Clear(Color.FromArgb(24, 21, 29));
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var crop = new Rectangle(4, 4, original.Width - 8, original.Height - 8);
        var scale = Math.Max(backdropWidth / (float)crop.Width, backdropHeight / (float)crop.Height);
        var imageWidth = (int)Math.Ceiling(crop.Width * scale);
        var imageHeight = (int)Math.Ceiling(crop.Height * scale);
        var imageBounds = new Rectangle((backdropWidth - imageWidth) / 2, (backdropHeight - imageHeight) / 2, imageWidth, imageHeight);

        graphics.DrawImage(original, imageBounds, crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel);
        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(44, 78, backdrop.Width - 88, 510);
        using var glow = new PathGradientBrush(glowPath)
        {
            CenterColor = Color.FromArgb(30, 239, 143, 56),
            SurroundColors = new[] { Color.FromArgb(0, 239, 143, 56) }
        };
        graphics.FillPath(glow, glowPath);
        return backdrop;
    }

    private static Bitmap CreateImageBackdrop(string resourceName, Color baseColor, int verticalOffset = 0)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Не удалось загрузить фоновое изображение: {resourceName}.");
        using var original = Image.FromStream(stream);

        const int width = 500;
        const int height = 726;
        var backdrop = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(backdrop);
        graphics.Clear(baseColor);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var scale = Math.Max(width / (float)original.Width, height / (float)original.Height);
        var imageWidth = (int)Math.Ceiling(original.Width * scale);
        var imageHeight = (int)Math.Ceiling(original.Height * scale);
        var bounds = new Rectangle((width - imageWidth) / 2, (height - imageHeight) / 2 + verticalOffset, imageWidth, imageHeight);
        graphics.DrawImage(original, bounds, 0, 0, original.Width, original.Height, GraphicsUnit.Pixel);
        return backdrop;
    }

    private static Bitmap CreateContainedImageBackdrop(string resourceName, Color topColor, Color bottomColor)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Не удалось загрузить фоновое изображение: {resourceName}.");
        using var original = Image.FromStream(stream);

        const int width = 500;
        const int height = 726;
        var backdrop = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(backdrop);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var background = new LinearGradientBrush(new Rectangle(0, 0, width, height), topColor, bottomColor, 90F))
            graphics.FillRectangle(background, 0, 0, width, height);

        // Fit the full landscape artwork across the top of the calculator canvas so
        // all three characters remain visible instead of cropping to its center.
        var scale = Math.Min(width / (float)original.Width, height / (float)original.Height);
        var imageWidth = (int)Math.Round(original.Width * scale);
        var imageHeight = (int)Math.Round(original.Height * scale);
        var imageBounds = new Rectangle((width - imageWidth) / 2, 0, imageWidth, imageHeight);
        graphics.DrawImage(original, imageBounds, 0, 0, original.Width, original.Height, GraphicsUnit.Pixel);
        return backdrop;
    }

    private void ApplyThemeToControl(Control control, ThemePalette palette)
    {
        if (control == _displayPanel)
        {
            control.BackColor = palette.HasAccents ? Color.Transparent : palette.Surface;
        }
        else if (control.Name == "displayHeader")
        {
            control.BackColor = palette.HasAccents ? Color.Transparent : palette.Surface;
        }
        else if (control is TableLayoutPanel or Panel)
        {
            control.BackColor = palette.HasAccents && control.Name is "rootLayout" or "buttonGrid" or "scientificButtonGrid"
                ? Color.Transparent
                : palette.Background;
        }
        else if (control == _expressionLabel)
        {
            control.BackColor = palette.HasAccents ? Color.Transparent : palette.DisplayBackground;
            control.ForeColor = palette.DisplayText;
        }
        else if (control == _resultLabel)
        {
            control.BackColor = palette.HasAccents ? Color.Transparent : palette.DisplayBackground;
            control.ForeColor = GetResultColor(palette);
        }
        else if (control is Button button && button.Tag is ButtonStyle style)
        {
            button.BackColor = style switch
            {
                ButtonStyle.Equals => palette.EqualsButton,
                ButtonStyle.Operator => palette.OperatorButton,
                ButtonStyle.Utility => palette.UtilityButton,
                _ => palette.NumberButton
            };
            button.ForeColor = style == ButtonStyle.Equals ? Color.White : palette.Text;
            button.FlatAppearance.MouseOverBackColor = style == ButtonStyle.Equals
                ? palette.EqualsHover
                : palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = style == ButtonStyle.Equals
                ? palette.EqualsHover
                : palette.ButtonPressed;
            button.FlatAppearance.BorderSize = palette.HasAccents ? 1 : 0;
            button.FlatAppearance.BorderColor = palette.Accent;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child, palette);
        }
    }

    private Color GetResultColor(ThemePalette palette)
    {
        if (_resultLabel.Text.StartsWith("= ", StringComparison.Ordinal) || _resultLabel.Text.Contains(" = ", StringComparison.Ordinal))
            return palette.Success;
        if (!string.IsNullOrWhiteSpace(_resultLabel.Text))
            return palette.Error;
        return palette.SecondaryText;
    }

    private static AppTheme ReadInitialTheme()
    {
        try
        {
            if (File.Exists(ThemeSettingsPath))
            {
                var savedTheme = File.ReadAllText(ThemeSettingsPath).Trim();
                if (Enum.TryParse<AppTheme>(savedTheme, ignoreCase: true, out var theme))
                    return theme;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(
                "Не удалось прочитать сохранённую тему. Будет использована тема Windows.\n" + ex.Message,
                "Настройки калькулятора",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var lightMode = key?.GetValue("AppsUseLightTheme");
            if (lightMode is int value && value == 0)
                return AppTheme.Dark;
        }
        catch (System.Security.SecurityException)
        {
            // Если системную настройку нельзя прочитать, используем светлый профиль.
        }

        return AppTheme.Light;
    }

    private static AppStyle ReadInitialStyle()
    {
        try
        {
            if (File.Exists(StyleSettingsPath) &&
                Enum.TryParse<AppStyle>(File.ReadAllText(StyleSettingsPath).Trim(), true, out var style))
                return style;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show("Не удалось прочитать сохранённый стиль. Будет использован стандартный.\n" + ex.Message,
                "Настройки калькулятора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return AppStyle.Standard;
    }

    private ThemePalette CurrentPalette
    {
        get
        {
            if (_activeStyle == AppStyle.SunsetCats)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(48, 34, 39), Color.FromArgb(64, 45, 48), Color.FromArgb(255, 239, 224),
                        Color.FromArgb(207, 169, 151), Color.FromArgb(89, 59, 59), Color.FromArgb(107, 67, 63),
                        Color.FromArgb(138, 75, 61), Color.FromArgb(177, 67, 50), Color.FromArgb(202, 83, 58),
                        Color.FromArgb(248, 193, 122), Color.FromArgb(255, 143, 130),
                        Color.FromArgb(67, 45, 48), Color.FromArgb(255, 242, 226), true, Color.FromArgb(239, 160, 104))
                    : new ThemePalette(
                        Color.FromArgb(242, 190, 153), Color.FromArgb(249, 215, 195), Color.FromArgb(65, 39, 42),
                        Color.FromArgb(124, 80, 68), Color.FromArgb(251, 226, 210), Color.FromArgb(240, 199, 179),
                        Color.FromArgb(233, 171, 141), Color.FromArgb(171, 73, 56), Color.FromArgb(195, 91, 68),
                        Color.FromArgb(117, 83, 47), Color.FromArgb(169, 54, 61),
                        Color.FromArgb(249, 228, 216), Color.FromArgb(67, 42, 44), true, Color.FromArgb(162, 74, 59));

            if (_activeStyle == AppStyle.Card)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(18, 13, 18), Color.FromArgb(35, 25, 31), Color.FromArgb(250, 236, 237),
                        Color.FromArgb(210, 174, 180), Color.FromArgb(45, 31, 39), Color.FromArgb(63, 38, 47),
                        Color.FromArgb(94, 35, 46), Color.FromArgb(171, 29, 51), Color.FromArgb(202, 44, 66),
                        Color.FromArgb(248, 181, 184), Color.FromArgb(255, 112, 122),
                        Color.FromArgb(39, 27, 33), Color.FromArgb(255, 244, 242), true, Color.FromArgb(202, 56, 72))
                    : new ThemePalette(
                        Color.FromArgb(235, 219, 218), Color.FromArgb(247, 235, 232), Color.FromArgb(47, 29, 36),
                        Color.FromArgb(111, 77, 82), Color.FromArgb(239, 222, 221), Color.FromArgb(222, 199, 202),
                        Color.FromArgb(239, 203, 202), Color.FromArgb(184, 42, 58), Color.FromArgb(211, 58, 72),
                        Color.FromArgb(131, 52, 60), Color.FromArgb(172, 47, 55),
                        Color.FromArgb(250, 235, 230), Color.FromArgb(255, 246, 240), true, Color.FromArgb(185, 48, 64));

            if (_activeStyle == AppStyle.Forest)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(23, 31, 28), Color.FromArgb(34, 44, 39), Color.FromArgb(244, 238, 214),
                        Color.FromArgb(197, 196, 170), Color.FromArgb(43, 54, 48), Color.FromArgb(56, 68, 58),
                        Color.FromArgb(70, 80, 57), Color.FromArgb(176, 133, 52), Color.FromArgb(206, 159, 68),
                        Color.FromArgb(242, 203, 111), Color.FromArgb(242, 123, 104),
                        Color.FromArgb(53, 60, 48), Color.FromArgb(255, 246, 224), true, Color.FromArgb(219, 177, 83))
                    : new ThemePalette(
                        Color.FromArgb(214, 220, 199), Color.FromArgb(233, 232, 212), Color.FromArgb(39, 48, 39),
                        Color.FromArgb(91, 102, 81), Color.FromArgb(233, 231, 210), Color.FromArgb(211, 216, 195),
                        Color.FromArgb(216, 218, 186), Color.FromArgb(182, 131, 43), Color.FromArgb(208, 153, 55),
                        Color.FromArgb(111, 104, 54), Color.FromArgb(164, 64, 52),
                        Color.FromArgb(242, 240, 219), Color.FromArgb(255, 247, 225), true, Color.FromArgb(175, 127, 44));

            if (_activeStyle == AppStyle.Chains)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(13, 13, 17), Color.FromArgb(29, 29, 35), Color.FromArgb(246, 243, 242),
                        Color.FromArgb(197, 189, 193), Color.FromArgb(39, 39, 46), Color.FromArgb(54, 53, 61),
                        Color.FromArgb(69, 60, 67), Color.FromArgb(127, 69, 82), Color.FromArgb(154, 87, 101),
                        Color.FromArgb(239, 210, 212), Color.FromArgb(248, 121, 130),
                        Color.FromArgb(42, 40, 45), Color.FromArgb(255, 248, 245), true, Color.FromArgb(200, 166, 176))
                    : new ThemePalette(
                        Color.FromArgb(221, 219, 220), Color.FromArgb(239, 236, 235), Color.FromArgb(39, 35, 39),
                        Color.FromArgb(101, 94, 99), Color.FromArgb(232, 229, 229), Color.FromArgb(210, 206, 210),
                        Color.FromArgb(225, 216, 219), Color.FromArgb(151, 82, 96), Color.FromArgb(177, 101, 114),
                        Color.FromArgb(112, 76, 83), Color.FromArgb(164, 54, 62),
                        Color.FromArgb(244, 240, 238), Color.FromArgb(255, 248, 245), true, Color.FromArgb(149, 106, 117));

            if (_activeStyle == AppStyle.JoJo)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(25, 17, 31), Color.FromArgb(42, 31, 49), Color.FromArgb(248, 235, 214),
                        Color.FromArgb(204, 183, 205), Color.FromArgb(52, 39, 61), Color.FromArgb(68, 48, 71),
                        Color.FromArgb(89, 53, 75), Color.FromArgb(174, 125, 36), Color.FromArgb(207, 157, 48),
                        Color.FromArgb(242, 213, 128), Color.FromArgb(241, 112, 130),
                        Color.FromArgb(43, 31, 50), Color.FromArgb(255, 246, 231), true, Color.FromArgb(220, 175, 69))
                    : new ThemePalette(
                        Color.FromArgb(75, 59, 83), Color.FromArgb(93, 75, 101), Color.FromArgb(255, 242, 223),
                        Color.FromArgb(220, 201, 220), Color.FromArgb(84, 66, 93), Color.FromArgb(109, 83, 109),
                        Color.FromArgb(129, 78, 107), Color.FromArgb(176, 128, 37), Color.FromArgb(208, 159, 47),
                        Color.FromArgb(255, 222, 128), Color.FromArgb(255, 137, 146),
                        Color.FromArgb(68, 51, 79), Color.FromArgb(255, 246, 231), true, Color.FromArgb(229, 187, 74));

            if (_activeStyle == AppStyle.Tigakki)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(24, 21, 29), Color.FromArgb(42, 36, 47), Color.FromArgb(246, 235, 226),
                        Color.FromArgb(202, 188, 181), Color.FromArgb(48, 41, 55), Color.FromArgb(61, 52, 66),
                        Color.FromArgb(89, 57, 49), Color.FromArgb(211, 105, 43), Color.FromArgb(235, 132, 55),
                        Color.FromArgb(255, 207, 126), Color.FromArgb(255, 130, 125),
                        Color.FromArgb(246, 232, 223), Color.FromArgb(255, 246, 235), true, Color.FromArgb(239, 153, 65))
                    : new ThemePalette(
                        Color.FromArgb(231, 219, 220), Color.FromArgb(243, 232, 228), Color.FromArgb(49, 39, 49),
                        Color.FromArgb(111, 96, 105), Color.FromArgb(238, 226, 223), Color.FromArgb(221, 207, 211),
                        Color.FromArgb(238, 213, 193), Color.FromArgb(224, 119, 49), Color.FromArgb(199, 91, 34),
                        Color.FromArgb(139, 83, 34), Color.FromArgb(177, 59, 59),
                        Color.FromArgb(255, 241, 231), Color.FromArgb(255, 246, 235), true, Color.FromArgb(218, 128, 53));

            if (_activeStyle == AppStyle.Makima)
                return _activeTheme == AppTheme.Dark
                    ? new ThemePalette(
                        Color.FromArgb(19, 15, 19), Color.FromArgb(37, 23, 29), Color.FromArgb(244, 225, 197),
                        Color.FromArgb(201, 174, 143), Color.FromArgb(74, 39, 48), Color.FromArgb(57, 31, 39),
                        Color.FromArgb(91, 42, 54), Color.FromArgb(126, 27, 48), Color.FromArgb(157, 39, 61),
                        Color.FromArgb(215, 185, 126), Color.FromArgb(238, 111, 111),
                        Color.FromArgb(242, 232, 211), Color.FromArgb(250, 236, 214), true, Color.FromArgb(194, 157, 86))
                    : new ThemePalette(
                        Color.FromArgb(43, 28, 33), Color.FromArgb(61, 38, 43), Color.FromArgb(248, 230, 205),
                        Color.FromArgb(207, 179, 149), Color.FromArgb(83, 43, 51), Color.FromArgb(65, 37, 43),
                        Color.FromArgb(101, 47, 59), Color.FromArgb(137, 31, 53), Color.FromArgb(166, 45, 66),
                        Color.FromArgb(220, 190, 132), Color.FromArgb(245, 125, 120),
                        Color.FromArgb(244, 235, 216), Color.FromArgb(250, 236, 214), true, Color.FromArgb(207, 169, 96));

            return _activeTheme == AppTheme.Dark
                ? new ThemePalette(
            Color.FromArgb(25, 27, 32), Color.FromArgb(37, 40, 47), Color.FromArgb(240, 242, 245),
            Color.FromArgb(174, 182, 193), Color.FromArgb(44, 48, 57), Color.FromArgb(55, 60, 70),
            Color.FromArgb(48, 58, 74), Color.FromArgb(54, 111, 195), Color.FromArgb(42, 94, 169),
            Color.FromArgb(87, 207, 139), Color.FromArgb(255, 125, 125), Color.FromArgb(37, 40, 47),
            Color.FromArgb(240, 242, 245), false, Color.FromArgb(25, 27, 32))
                : new ThemePalette(
            Color.FromArgb(245, 247, 250), Color.White, Color.FromArgb(35, 42, 52),
            Color.FromArgb(95, 105, 117), Color.White, Color.FromArgb(232, 235, 240),
            Color.FromArgb(228, 237, 248), Color.FromArgb(35, 112, 210), Color.FromArgb(24, 94, 183),
            Color.FromArgb(28, 126, 73), Color.FromArgb(190, 45, 45), Color.White,
            Color.FromArgb(35, 42, 52), false, Color.FromArgb(245, 247, 250));
        }
    }

    private void ApplyAction(ButtonAction action, string caption)
    {
        switch (action)
        {
            case ButtonAction.Clear:
                _expression = string.Empty;
                _showingResult = false;
                _resultLabel.ForeColor = CurrentPalette.SecondaryText;
                _resultLabel.Text = string.Empty;
                break;
            case ButtonAction.Backspace:
                if (_showingResult) _showingResult = false;
                if (_expression.Length > 0) _expression = _expression[..^1];
                _resultLabel.Text = string.Empty;
                _resultLabel.ForeColor = CurrentPalette.SecondaryText;
                break;
            case ButtonAction.ToggleSign:
                var hadResult = _showingResult;
                ToggleSign();
                if (hadResult)
                {
                    _resultLabel.Text = string.Empty;
                    _resultLabel.ForeColor = CurrentPalette.SecondaryText;
                }
                break;
            case ButtonAction.Equals:
                Calculate();
                return;
            case ButtonAction.Insert:
                if (caption is "sin" or "cos" or "x²" or "x!" or "√")
                {
                    if (caption is "sin" or "cos")
                        _expression = _expression.Length == 0
                            ? caption + "("
                            : caption + "(" + _expression + ")";
                    else if (caption == "x²" && _expression.Length > 0)
                        _expression = "(" + _expression + ")^2";
                    else if (caption == "x!" && _expression.Length > 0)
                        _expression = "(" + _expression + ")!";
                    else if (caption == "√")
                        _expression = _expression.Length == 0 ? "√(" : "√(" + _expression + ")";

                    _showingResult = false;
                    _resultLabel.Text = string.Empty;
                    _resultLabel.ForeColor = CurrentPalette.SecondaryText;
                    break;
                }

                if (_showingResult && (char.IsAsciiDigit(caption[0]) || caption is "," or "." or "("))
                    _expression = string.Empty;
                _showingResult = false;
                _expression += NormalizeButtonText(caption);
                _resultLabel.Text = string.Empty;
                _resultLabel.ForeColor = CurrentPalette.SecondaryText;
                break;
        }

        RefreshDisplay();
    }

    private void ToggleSign()
    {
        if (_expression.Length == 0) return;

        if ((_expression.Contains('e') || _expression.Contains('E')) &&
            double.TryParse(_expression.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var scientificValue) &&
            double.IsFinite(scientificValue))
        {
            var negated = -scientificValue;
            _expression = negated == 0d ? "0" : negated.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            _showingResult = false;
            return;
        }

        if (_showingResult) _showingResult = false;

        var end = _expression.Length;
        if (_expression[end - 1] == ')')
        {
            var depth = 0;
            var open = -1;
            for (var i = end - 1; i >= 0; i--)
            {
                if (_expression[i] == ')') depth++;
                else if (_expression[i] == '(' && --depth == 0) { open = i; break; }
            }
            if (open >= 0)
            {
                if (open > 0 && _expression[open - 1] == '-') _expression = _expression.Remove(open - 1, 1);
                else _expression = _expression.Insert(open, "-");
            }
            return;
        }

        var start = end;
        while (start > 0 && (char.IsAsciiDigit(_expression[start - 1]) || _expression[start - 1] is '.' or ',')) start--;
        if (start == end) return;

        var isUnaryMinus = start > 0 && _expression[start - 1] == '-' &&
            (start == 1 || _expression[start - 2] is '+' or '-' or '*' or '/' or '(');
        _expression = isUnaryMinus ? _expression.Remove(start - 1, 1) : _expression.Insert(start, "-");
    }

    private void Calculate()
    {
        if (_showingResult) return;

        try
        {
            var sourceExpression = _expression;
            var result = _evaluator.Evaluate(_expression);
            var resultText = _evaluator.FormatResult(result);
            _expression = result == 0d ? "0" : result.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            _resultLabel.Text = FormatExpressionForDisplay(sourceExpression) + " = " + FormatExpressionForDisplay(resultText);
            _resultLabel.ForeColor = CurrentPalette.Success;
            _showingResult = true;
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or ArithmeticException or ArgumentException)
        {
            _resultLabel.Text = ex.Message;
            _resultLabel.ForeColor = CurrentPalette.Error;
            _showingResult = false;
        }

        RefreshDisplay(updatePreview: false);
    }

    private void RefreshDisplay(bool updatePreview = true)
    {
        _expressionLabel.Text = _expression.Length == 0 ? "0" : FormatExpressionForDisplay(_expression);
        if (updatePreview && !_showingResult) UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(_expression))
        {
            _resultLabel.Text = string.Empty;
            _resultLabel.ForeColor = CurrentPalette.SecondaryText;
            return;
        }

        try
        {
            var value = _evaluator.Evaluate(_expression);
            _resultLabel.Text = "= " + FormatExpressionForDisplay(_evaluator.FormatResult(value));
            _resultLabel.ForeColor = CurrentPalette.Success;
        }
        catch (FormatException)
        {
            // Незавершённое выражение — обычный этап ввода, ждём следующего символа.
            _resultLabel.Text = string.Empty;
            _resultLabel.ForeColor = CurrentPalette.SecondaryText;
        }
        catch (DivideByZeroException ex)
        {
            _resultLabel.Text = ex.Message;
            _resultLabel.ForeColor = CurrentPalette.Error;
        }
        catch (ArithmeticException ex)
        {
            _resultLabel.Text = ex.Message;
            _resultLabel.ForeColor = CurrentPalette.Error;
        }
    }

    private static string FormatExpressionForDisplay(string expression) => NumericTokenRegex.Replace(expression, match =>
    {
        var token = match.Value;
        var decimalIndex = token.IndexOfAny([',', '.']);
        var integerLength = decimalIndex < 0 ? token.Length : decimalIndex;
        if (integerLength <= 3 || (integerLength > 1 && token[0] == '0')) return token;

        var grouped = new StringBuilder(token.Length + integerLength / 3);
        for (var index = 0; index < integerLength; index++)
        {
            if (index > 0 && (integerLength - index) % 3 == 0) grouped.Append(' ');
            grouped.Append(token[index]);
        }
        if (decimalIndex >= 0) grouped.Append(token.AsSpan(decimalIndex));
        return grouped.ToString();
    });

    private static string NormalizeButtonText(string caption) => caption switch
    {
        "×" => "*",
        "÷" => "/",
        "−" => "-",
        "xʸ" => "^",
        "x!" => "!",
        "x²" => "^2",
        _ => caption
    };

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Calculate();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            ApplyAction(ButtonAction.Clear, "C");
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Back)
        {
            ApplyAction(ButtonAction.Backspace, "⌫");
            e.Handled = true;
        }
    }

    private void HandleKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsAsciiDigit(e.KeyChar) || e.KeyChar is '.' or ',' or '+' or '-' or '*' or '/' or '^' or '%' or '!' or '(' or ')' or '√' or 'e' or 'E')
        {
            var text = e.KeyChar switch { '.' => ",", _ => e.KeyChar.ToString() };
            ApplyAction(ButtonAction.Insert, text);
            e.Handled = true;
        }
    }

    private enum AppTheme { Light, Dark }
    private enum AppStyle { Standard, Makima, Tigakki, Card, Forest, Chains, JoJo, SunsetCats }
    private enum ButtonAction { Insert, Clear, Backspace, ToggleSign, Equals }
    private enum ButtonStyle { Number, Operator, Utility, Equals }

    private sealed record ThemePalette(
        Color Background,
        Color Surface,
        Color Text,
        Color SecondaryText,
        Color NumberButton,
        Color UtilityButton,
        Color OperatorButton,
        Color EqualsButton,
        Color EqualsHover,
        Color Success,
        Color Error,
        Color DisplayBackground,
        Color DisplayText,
        bool HasAccents,
        Color Accent)
    {
        public Color ButtonHover => Color.FromArgb(
            Math.Min(255, Background.R + 16),
            Math.Min(255, Background.G + 16),
            Math.Min(255, Background.B + 16));

        public Color ButtonPressed => HasAccents
            ? Color.FromArgb(Math.Max(0, Background.R - 10), Math.Max(0, Background.G - 8), Math.Max(0, Background.B - 8))
            : Color.FromArgb(Math.Max(0, Background.R - 18), Math.Max(0, Background.G - 18), Math.Max(0, Background.B - 18));
    }
}
