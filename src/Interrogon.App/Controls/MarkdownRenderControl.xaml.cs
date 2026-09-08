using ColorCode;
using ColorCode.Common;
using Interrogon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Windows.ApplicationModel.DataTransfer;

namespace Interrogon.App.Controls;

public sealed partial class MarkdownRenderControl : UserControl
{
    private static readonly RichTextBlockFormatter Formatter = new();

    private const double BaseProseFontSize = 14;
    private const double BaseCodeFontSize = 13;

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownRenderControl),
            new PropertyMetadata(null, OnMarkdownChanged));

    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public static readonly DependencyProperty FontScaleProperty =
        DependencyProperty.Register(
            nameof(FontScale),
            typeof(double),
            typeof(MarkdownRenderControl),
            new PropertyMetadata(1.0, OnMarkdownChanged));

    /// <summary>Multiplier applied to both prose and code font sizes, e.g. 1.5 for 150%.</summary>
    public double FontScale
    {
        get => (double)GetValue(FontScaleProperty);
        set => SetValue(FontScaleProperty, value);
    }

    public MarkdownRenderControl()
    {
        InitializeComponent();
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownRenderControl)d).Render();
    }

    private void Render()
    {
        SegmentsPanel.Children.Clear();

        var segments = MarkdownContent.ParseSegments(Markdown);
        foreach (var segment in segments)
        {
            SegmentsPanel.Children.Add(segment.Kind == MarkdownSegmentKind.Code
                ? BuildCodeBlock(segment.Text, segment.Language)
                : BuildProseBlock(segment.Text));
        }
    }

    private TextBlock BuildProseBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontSize = BaseProseFontSize * FontScale
        };
    }

    private FrameworkElement BuildCodeBlock(string code, string? language)
    {
        var richTextBlock = new RichTextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = BaseCodeFontSize * FontScale,
            IsTextSelectionEnabled = true
        };

        var resolvedLanguage = ResolveLanguage(language);
        if (resolvedLanguage is not null)
        {
            Formatter.FormatRichTextBlock(code, resolvedLanguage, richTextBlock);
        }
        else
        {
            richTextBlock.Blocks.Add(new Paragraph
            {
                Inlines = { new Run { Text = code } }
            });
        }

        var copyButton = new Button
        {
            Content = "Copy",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(8, 2, 8, 2)
        };
        copyButton.Click += (_, _) =>
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(code);
            Clipboard.SetContent(dataPackage);
        };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = language ?? "code",
            Opacity = 0.6,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(copyButton, 1);
        header.Children.Add(copyButton);

        var container = new StackPanel { Spacing = 4 };
        container.Children.Add(header);

        var codeBorder = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = richTextBlock
        };
        container.Children.Add(codeBorder);

        return container;
    }

    private static ILanguage? ResolveLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        return Languages.FindById(language) ?? language.ToLowerInvariant() switch
        {
            "cs" or "csharp" => Languages.CSharp,
            "js" or "javascript" => Languages.JavaScript,
            "ts" or "typescript" => Languages.Typescript,
            "py" or "python" => Languages.Python,
            "xml" => Languages.Xml,
            "html" => Languages.Html,
            "css" => Languages.Css,
            "sql" => Languages.Sql,
            "powershell" or "ps1" => Languages.PowerShell,
            "cpp" or "c++" => Languages.Cpp,
            "java" => Languages.Java,
            "php" => Languages.Php,
            "fsharp" or "f#" => Languages.FSharp,
            _ => null
        };
    }
}
