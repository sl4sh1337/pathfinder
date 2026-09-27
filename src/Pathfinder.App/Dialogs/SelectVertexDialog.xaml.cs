using System.Windows;
using Pathfinder.Core.Model;

namespace Pathfinder.App.Dialogs;

/// <summary>Asks for the source vertex before an algorithm run.</summary>
public partial class SelectVertexDialog : Window
{
    private SelectVertexDialog(string algorithmName, Graph graph, int? preselectedId)
    {
        InitializeComponent();
        AlgorithmText.Text = algorithmName;
        var vertices = graph.Vertices.Values.OrderBy(v => v.Name, StringComparer.CurrentCulture).ToList();
        VertexBox.ItemsSource = vertices;
        VertexBox.SelectedItem = vertices.FirstOrDefault(v => v.Id == preselectedId) ?? vertices.FirstOrDefault();
        Loaded += (_, _) => VertexBox.Focus();
    }

    /// <summary>Returns the chosen vertex id, or <c>null</c> if the user cancelled.</summary>
    public static int? Prompt(Window owner, string algorithmName, Graph graph, int? preselectedId)
    {
        var dialog = new SelectVertexDialog(algorithmName, graph, preselectedId) { Owner = owner };
        return dialog.ShowDialog() == true && dialog.VertexBox.SelectedItem is Vertex vertex ? vertex.Id : null;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (VertexBox.SelectedItem is Vertex)
        {
            DialogResult = true;
        }
    }
}
