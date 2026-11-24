# Notices and attributions

The source code of this project is released under the MIT license (see [LICENSE](LICENSE)). This file lists what is **not** covered by it.

Not affiliated with, endorsed or sponsored by Wizards of the Coast. Dungeons & Dragons and D&D are trademarks of Wizards of the Coast.

## SRD 5.2.1 content (glossary) — CC-BY-4.0, not MIT

The packs in `src/DmCompanion.Core/Glossary/Packs/*.json` (and derived data, such as the SrdImport test fixtures) contain text from the System Reference Document 5.2.1. This content is **not** under the MIT license of the code: it is licensed under Creative Commons Attribution 4.0 International.

**The SRD text has been modified.** It is a Spanish translation made by a third party (the compendio-srd project, see below), reformatted by this project into JSON (structured blocks and spans, an index and per-category packs) with the English name of each entry kept as `nameEn`. It is not the original English text published by Wizards of the Coast.

### English

This work includes material from the System Reference Document 5.2.1 (“SRD 5.2.1”) by Wizards of the Coast LLC, available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative Commons Attribution 4.0 International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.

### Español

Esta obra incluye material procedente del documento de referencia del sistema 5.2.1 (“SRD 5.2.1”) de Wizards of the Coast LLC, disponible en https://www.dndbeyond.com/srd. La licencia sobre el SRD 5.2.1 se concede de acuerdo con la licencia internacional de atribución/reconocimiento 4.0 de Creative Commons, disponible en https://creativecommons.org/licenses/by/4.0/legalcode.

## Source of the data: compendio-srd (MIT)

The texts were imported from **compendio-srd** (https://github.com/Ismaelbase/compendio-srd), commit `2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc`, folder `datos/` (language `es`). They are regenerated with `dotnet run --project tools/DmCompanion.SrdImport -- <datos folder> src/DmCompanion.Core/Glossary/Packs`.

compendio-srd is distributed under the MIT license:

```
MIT License

Copyright (c) 2026 Ismael Sevilla

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Third-party components included in the repository

| Component | Where | License | License file |
|---|---|---|---|
| EB Garamond (font) | `src/DmCompanion.UI/wwwroot/fonts/` | SIL OFL 1.1 | `fonts/LICENSE-eb-garamond.txt` |
| Source Sans 3 (font) | `src/DmCompanion.UI/wwwroot/fonts/` | SIL OFL 1.1 | `fonts/LICENSE-source-sans-3.txt` |
| Lucide (icons) | `src/DmCompanion.UI/wwwroot/icons/` | ISC | `fonts/LICENSE-lucide.txt` |
| Konva 9.3.18 | `src/DmCompanion.Screens/wwwroot/lib/konva/` | MIT | `lib/konva/LICENSE` |
| pdf.js (`pdfjs-dist` 6.5.438, Mozilla) | `src/DmCompanion.Screens/wwwroot/lib/pdfjs/` | Apache-2.0 | `lib/pdfjs/LICENSE` |
| Liberation Sans (in pdf.js `standard_fonts/`) | `lib/pdfjs/standard_fonts/` | GPL v2 with font exception (Liberation 1.x license text as shipped by pdf.js; newer Liberation releases are OFL) | `standard_fonts/LICENSE_LIBERATION` |
| Foxit fonts (in pdf.js `standard_fonts/`) | `lib/pdfjs/standard_fonts/` | BSD-style (PDFium authors) | `standard_fonts/LICENSE_FOXIT` |
| Adobe CMaps (in pdf.js `cmaps/`) | `lib/pdfjs/cmaps/` | BSD-style (Adobe) | `cmaps/LICENSE` |
| OpenJPEG, JBIG2 and QCMS decoders (in pdf.js `wasm/`) | `lib/pdfjs/wasm/` | BSD-2-Clause / BSD-style / MIT / Apache-2.0 | `wasm/LICENSE_*` |
| compendio-srd (data source) | see above | MIT | reproduced above |

Only `build/pdf.min.mjs`, `build/pdf.worker.min.mjs`, `standard_fonts/`, `cmaps/` and `wasm/` of pdf.js are copied; the QuickJS engine (used only by PDF scripting, disabled) is not included. NuGet dependencies (for example YamlDotNet and Markdig) are restored at build time and are not part of the repository.
