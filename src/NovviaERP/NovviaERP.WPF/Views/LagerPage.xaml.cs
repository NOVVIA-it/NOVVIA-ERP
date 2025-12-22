using System.Windows;using System.Windows.Controls;using NovviaERP.Core.Entities;
namespace NovviaERP.WPF.Views{public partial class LagerPage:Page{public LagerPage(){InitializeComponent();Loaded+=async(s,e)=>dgLager.ItemsSource=await App.Db.GetLagerbestaendeAsync();}}}
