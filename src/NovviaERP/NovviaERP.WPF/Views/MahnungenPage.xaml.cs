using System.Windows;using System.Windows.Controls;
namespace NovviaERP.WPF.Views{public partial class MahnungenPage:Page{public MahnungenPage(){InitializeComponent();Loaded+=async(s,e)=>dgMahnungen.ItemsSource=await App.Db.GetFaelligeRechnungenAsync();}}}
