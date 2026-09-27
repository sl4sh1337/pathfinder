using System.Windows;

namespace Pathfinder.App.Dialogs;

public enum InputDialogOutcome
{
    Cancelled,
    Accepted,
    DeleteRequested,
}

public sealed record InputDialogResult(InputDialogOutcome Outcome, string Value);

/// <summary>
/// Single-line input with live validation, optionally offering to delete the edited object.
/// Used for vertex names and edge weights.
/// </summary>
public partial class InputDialog : Window
{
    private readonly Func<string, string?> _validate;
    private InputDialogOutcome _outcome = InputDialogOutcome.Cancelled;

    private InputDialog(string title, string prompt, string initialValue, Func<string, string?> validate, bool allowDelete)
    {
        InitializeComponent();
        _validate = validate;
        Title = title;
        PromptText.Text = prompt;
        ValueBox.Text = initialValue;
        DeleteButton.Visibility = allowDelete ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public static InputDialogResult Prompt(
        Window owner,
        string title,
        string prompt,
        string initialValue,
        Func<string, string?> validate,
        bool allowDelete = false)
    {
        var dialog = new InputDialog(title, prompt, initialValue, validate, allowDelete) { Owner = owner };
        dialog.ShowDialog();
        return new InputDialogResult(dialog._outcome, dialog.ValueBox.Text.Trim());
    }

    private void ValueBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var error = _validate(ValueBox.Text.Trim());
        ErrorText.Text = error ?? string.Empty;
        OkButton.IsEnabled = error is null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_validate(ValueBox.Text.Trim()) is not null)
        {
            return;
        }

        _outcome = InputDialogOutcome.Accepted;
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _outcome = InputDialogOutcome.DeleteRequested;
        DialogResult = true;
    }
}
