const fs = require('fs');

const filePath = 'C:/NovviaERP/src/NovviaERP/NovviaERP.WPF/Views/BestellungDetailView.xaml.cs';
let content = fs.readFileSync(filePath, 'utf8');

// Fix the method - replace multiline strings with escaped versions
const fixedMethod = `        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null)
            {
                MessageBox.Show("Kein Auftrag geladen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prüfen ob Löschen möglich ist
            var (canDelete, reason) = await _core.CanDeleteAuftragAsync(_bestellung.KBestellung);
            if (!canDelete)
            {
                MessageBox.Show("Auftrag kann nicht gelöscht werden:\\n\\n" + reason, "Löschen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Bestätigung einholen
            var result = MessageBox.Show("Möchten Sie den Auftrag " + _bestellung.CBestellNr + " wirklich löschen?\\n\\nDiese Aktion kann nicht rückgängig gemacht werden!", "Auftrag löschen?", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Löschen durchführen
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
        }`;

// Use regex to find and replace the broken method
const regex = /\s+private async void Loeschen_Click\(object sender, RoutedEventArgs e\)[\s\S]*?\}\s*\n\s*\n\s*private void KundeOeffnen_Click/;
const replacement = fixedMethod + '\n\n        private void KundeOeffnen_Click';

content = content.replace(regex, replacement);

fs.writeFileSync(filePath, content);
console.log('Fixed!');
