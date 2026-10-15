using System.Windows;

namespace PCOptimizer.App;

public partial class SelectionReviewWindow : Window
{
    internal SelectionReviewWindow(string title, IReadOnlyList<SoftwareChoice> choices, string notice)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = $"{title} · {choices.Count} aplicaciones";
        Notice.Text = notice;
        Selection.ItemsSource = choices;
    }

    private void Accept(object sender, RoutedEventArgs e) => DialogResult = true;
}
