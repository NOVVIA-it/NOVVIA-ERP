using System.Windows;

namespace NovviaERP.WPF.Views
{
    public partial class InputDialog : Window
    {
        public string? Ergebnis { get; private set; }

        public InputDialog(string titel, string label, string? standardWert = null)
        {
            InitializeComponent();
            Title = titel;
            txtLabel.Text = label;
            txtEingabe.Text = standardWert ?? "";
            txtEingabe.Focus();
            txtEingabe.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Ergebnis = txtEingabe.Text.Trim();
            DialogResult = true;
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
