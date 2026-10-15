using System.Windows;

namespace PCOptimizer.App;

public partial class SelectionReviewWindow : Window
{
    internal SelectionReviewWindow(string title, IReadOnlyList<SoftwareChoice> choices, string notice, string noun = "aplicaciones")
    {
        InitializeComponent();
        Title = title;
        Heading.Text = $"{title} · {choices.Count} {noun}";
        Notice.Text = notice;
        Selection.ItemsSource = choices;
    }

    private void Accept(object sender, RoutedEventArgs e) => DialogResult = true;
}
