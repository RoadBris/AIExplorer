using System.Windows;
using System.Windows.Input;

namespace AIExplorer.Dialogs;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog(
        Window owner,
        string title,
        string description,
        string initialValue = "")
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        PromptTitle.Text = title;
        PromptDescription.Text = description;
        ValueTextBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    public string Value => ValueTextBox.Text.Trim();

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueTextBox.Text))
        {
            MessageBox.Show(
                this,
                "값을 입력해 주세요.",
                "AI 탐색기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        ConfirmButton_Click(sender, e);
        e.Handled = true;
    }
}
