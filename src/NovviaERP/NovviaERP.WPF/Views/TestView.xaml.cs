using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class TestView : UserControl
    {
        public TestView()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("BUTTON FUNKTIONIERT!", "Erfolg");
        }
    }
}
