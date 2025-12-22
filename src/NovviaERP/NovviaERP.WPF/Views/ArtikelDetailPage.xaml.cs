using System;
using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelDetailPage : Page
    {
        private int? _artikelId;

        public ArtikelDetailPage(int? artikelId)
        {
            InitializeComponent();
            _artikelId = artikelId;

            txtTest.Text = artikelId.HasValue
                ? $"Artikel ID: {artikelId}"
                : "Neuer Artikel";
            txtStatus.Text = "Seite erfolgreich geladen!";
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
