# MikuScreenRecorder

A small tray tool for taking screenshots and recording GIFs on Windows. It uses the Win32 API directly and has its own GIF encoder (median cut quantization, optional dithering, LZW). The only framework used is Windows Forms - no external dependencies.

[Deutsche Version weiter unten](#deutsch)

## Features

- Screenshots via hotkey (`Print`)
- GIF recording of a freely selectable area (`Ctrl+Print`)
- Custom GIF encoder without external libraries
- Copy to clipboard and/or save to disk
- Autostart and settings via the tray menu
- Localized UI (English, German), detected from the system language and switchable at any time in the settings

## Requirements

- Windows 10/11
- [.NET Desktop Runtime](https://dotnet.microsoft.com/download) 8, 9 or 10

## Build

```
dotnet build -c Release
```

Or open `MikuScreenRecorder.sln` in Visual Studio.

## Adding a language

1. Copy `MikuScreenRecorder/Resources/Strings.resx` to `Strings.<culture>.resx` (e.g. `Strings.fr.resx`).
2. Translate the values. Save the file as UTF-8.
3. Rebuild. The language appears automatically in the settings.

## License

This project is licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE).

- Use, modify and share for **noncommercial** purposes
- The `Required Notice` (attribution) must be kept with every copy
- **Commercial use only with my explicit permission** - see [COMMERCIAL.md](COMMERCIAL.md)

---

## Deutsch

Ein kleines Tray-Tool für Screenshots und GIF-Aufnahmen unter Windows. Es nutzt die Win32 API direkt und hat einen eigenen GIF-Encoder (Median-Cut-Quantisierung, optionales Dithering, LZW). Als Framework kommt nur Windows Forms zum Einsatz, externe Abhängigkeiten gibt es keine.

### Funktionen

- Screenshots per Hotkey (`Druck`)
- GIF-Aufnahme eines frei wählbaren Bereichs (`Strg+Druck`)
- Eigener GIF-Encoder ohne externe Bibliotheken
- Kopieren in die Zwischenablage und/oder Speichern auf der Festplatte
- Autostart und Einstellungen über das Tray-Menü
- Mehrsprachige Oberfläche (Englisch, Deutsch), erkennt die Systemsprache und ist jederzeit in den Einstellungen umschaltbar

### Voraussetzungen

- Windows 10/11
- [.NET Desktop Runtime](https://dotnet.microsoft.com/download) 8, 9 oder 10

### Sprache hinzufügen

1. `MikuScreenRecorder/Resources/Strings.resx` nach `Strings.<kultur>.resx` kopieren (z. B. `Strings.fr.resx`).
2. Die Werte übersetzen. Die Datei als UTF-8 speichern.
3. Neu bauen. Die Sprache erscheint automatisch in den Einstellungen.

### Lizenz

Dieses Projekt steht unter der [PolyForm Noncommercial License 1.0.0](LICENSE).

- Nutzen, ändern und weitergeben für **nicht-kommerzielle** Zwecke
- Der Hinweis `Required Notice` (Namensnennung) muss bei jeder Weitergabe erhalten bleiben
- **Kommerzielle Nutzung nur mit meiner ausdrücklichen Erlaubnis** - siehe [COMMERCIAL.md](COMMERCIAL.md)

---

(c) HatsuneNemas
