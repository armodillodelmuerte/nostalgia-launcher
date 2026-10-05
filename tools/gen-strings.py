"""Builds src/Nostalgia.Launcher/Resources/Strings.resx from strings.de.json (the editable source, UTF-8).

Run after changing the JSON:  python tools/gen-strings.py
"""
import json
import pathlib
from xml.sax.saxutils import escape

root = pathlib.Path(__file__).resolve().parent.parent / "src" / "Nostalgia.Launcher" / "Resources"
data = json.loads((root / "strings.de.json").read_text(encoding="utf-8"))
head = """<?xml version="1.0" encoding="utf-8"?>
<!-- GENERATED from strings.de.json by tools/gen-strings.py - edit the JSON, not this file. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""
body = "".join(f'  <data name="{k}" xml:space="preserve"><value>{escape(v)}</value></data>\n' for k, v in data.items())
(root / "Strings.resx").write_text(head + body + "</root>\n", encoding="utf-8", newline="\n")
print(f"{len(data)} strings -> Strings.resx")
