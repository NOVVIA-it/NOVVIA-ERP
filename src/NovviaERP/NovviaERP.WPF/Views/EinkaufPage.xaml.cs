using System.Windows;using System.Windows.Controls;
namespace NovviaERP.WPF.Views{public partial class EinkaufPage:Page{public EinkaufPage(){InitializeComponent();Loaded+=async(s,e)=>dgEinkauf.ItemsSource=await App.Db.GetLieferantenAsync();}}}
