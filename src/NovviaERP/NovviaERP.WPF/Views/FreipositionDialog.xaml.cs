using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class FreipositionDialog : Window
    {
        public string Bezeichnung { get; private set; } = "";
        public decimal Menge { get; private set; } = 1;
        public string Einheit { get; private set; } = "Stk";
        public decimal PreisNetto { get; private set; }
        public decimal MwStSatz { get; private set; } = 19m;
        public string Hinweis { get; private set; } = "";

        public FreipositionDialog()
        {
            InitializeComponent();
            txtBezeichnung.Focus();
        }

        private void Hinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBezeichnung.Text))
            {
                MessageBox.Show("Bitte eine Bezeichnung eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBezeichnung.Focus();
                return;
            }

            if (!decimal.TryParse(txtMenge.Text.Replace(".", ","), out var menge) || menge <= 0)
            {
                MessageBox.Show("Bitte eine gültige Menge eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMenge.Focus();
                return;
            }

            if (!decimal.TryParse(txtPreisNetto.Text.Replace(".", ","), out var preis))
            {
                MessageBox.Show("Bitte einen gültigen Preis eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPreisNetto.Focus();
                return;
            }

            Bezeichnung = txtBezeichnung.Text.Trim();
            Menge = menge;
            Einheit = string.IsNullOrWhiteSpace(txtEinheit.Text) ? "Stk" : txtEinheit.Text.Trim();
            PreisNetto = preis;
            Hinweis = txtHinweis.Text?.Trim() ?? "";

            if (cmbMwSt.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                MwStSatz = decimal.Parse(item.Tag.ToString()!);
            }

            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
