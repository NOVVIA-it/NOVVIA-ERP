using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class TestPage : Page
    {
        public TestPage()
        {
            InitializeComponent();
        }

        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("BUTTON FUNKTIONIERT!", "Test");
        }
    }
}
