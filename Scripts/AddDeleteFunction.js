const fs = require('fs');

// Add delete button to XAML
const xamlPath = 'C:/NovviaERP/src/NovviaERP/NovviaERP.WPF/Views/BestellungDetailView.xaml';
let xaml = fs.readFileSync(xamlPath, 'utf8');

const deleteButton = `<Button x:Name="btnLoeschen" Content="Löschen" Click="Loeschen_Click" Padding="12,8" Margin="0,0,5,0"
                            Background="#dc3545" Foreground="White" BorderThickness="0"
                            ToolTip="Auftrag löschen (nur möglich wenn kein Lieferschein/Rechnung)"/>
                    `;

// Add delete button before Abbrechen button
if (!xaml.includes('btnLoeschen')) {
    xaml = xaml.replace(
        /(<Button x:Name="btnSpeichern"[^>]+\/>\s*\r?\n\s*)(<Button Content="Abbrechen")/,
        '$1' + deleteButton + '$2'
    );
    fs.writeFileSync(xamlPath, xaml);
    console.log('XAML updated');
} else {
    console.log('XAML already has delete button');
}

// Add click handler to code-behind
const csPath = 'C:/NovviaERP/src/NovviaERP/NovviaERP.WPF/Views/BestellungDetailView.xaml.cs';
let cs = fs.readFileSync(csPath, 'utf8');

const handler = `

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null)
            {
                MessageBox.Show("Kein Auftrag geladen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (canDelete, reason) = await _core.CanDeleteAuftragAsync(_bestellung.KBestellung);
            if (!canDelete)
            {
                MessageBox.Show("Auftrag kann nicht gelöscht werden:\\n\\n" + reason, "Löschen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Möchten Sie den Auftrag " + _bestellung.CBestellNr + " wirklich löschen?\\n\\nDiese Aktion kann nicht rückgängig gemacht werden!", "Auftrag löschen?", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var (success, message) = await _core.DeleteAuftragAsync(_bestellung.KBestellung);

            if (success)
            {
                MessageBox.Show(message, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                Zurueck_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Fehler beim Löschen:\\n\\n" + message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
`;

// Insert after Zurueck_Click method
if (!cs.includes('Loeschen_Click')) {
    cs = cs.replace(
        /(private void Zurueck_Click\(object sender, RoutedEventArgs e\)\s*\{[^}]+main\.ShowContent\(liste\);\s*\}\s*\})\s*(\r?\n\s*private void KundeOeffnen_Click)/,
        '$1' + handler + '\n$2'
    );
    fs.writeFileSync(csPath, cs);
    console.log('Code-behind updated');
} else {
    console.log('Code-behind already has handler');
}

console.log('Done!');
