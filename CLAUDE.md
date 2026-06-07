# NubeFiscal.PdfGenerator — Contexto para Claude

## Pendientes

- [ ] Subir el código fuente a GitHub bajo la organización `NubeFiscal/NubeFiscal.PdfGenerator`
- [ ] Reservar el prefijo `NubeFiscal.` en NuGet (escudo azul verificado) — se solicita abriendo una issue en `github.com/NuGet/NuGetGallery`
- [ ] Publicar `1.0.2` en nuget.org con los cambios acumulados: README actualizado, sección desplegable de requisitos SAT, licencia MIT incluida en el paquete, README movido a la carpeta del proyecto, soporte de retenciones (ISR/IVA) en CfdiXmlParser y PdfBuilder

## Estructura del proyecto

```
Tmp2/
  LICENSE                          ← MIT 2026 NubeFiscal
  README.md                        ← versión anterior, ya no se usa
  NubeFiscal.PdfGenerator/
    NubeFiscal.PdfGenerator.csproj ← class library, net10.0, PackageId = NubeFiscal.PdfGenerator
    README.md                      ← README activo (el que va al NuGet y GitHub)
    Models/
      CfdiPdfData.cs               ← grafo completo de modelos CFDI
    Services/
      CfdiXmlParser.cs             ← parser XML (FromXml, EnriquecerDesdeXml)
      PdfBuilder.cs                ← renderizador QuestPDF (Construir)
  nupkg/
    NubeFiscal.PdfGenerator.1.0.0.nupkg
    NubeFiscal.PdfGenerator.1.0.1.nupkg
    NubeFiscal.PdfGenerator.1.0.2.nupkg  ← versión actual (retenciones en parser y PDF)
```

## API pública

```csharp
// Desde XML
var cfdi     = CfdiXmlParser.FromXml(xmlString);
var pdfBytes = PdfBuilder.Construir(cfdi);

// Desde BD (modelo manual)
var cfdi     = new CfdiPdfData { UUID = "...", ... };
var pdfBytes = PdfBuilder.Construir(cfdi);

// Enriquecer un registro de BD con su XML
cfdi.XmlCFDI = xmlGuardadoEnBd;
CfdiXmlParser.EnriquecerDesdeXml(cfdi);
```

## Lo que NO está en el paquete (quedó en FiscalApi.PdfGenerator)

- Acceso a base de datos (`GetCfdisParaGenerar`, `ActualizarPdf`, etc.)
- Connection string hardcodeada con credenciales de producción
- `CfdiPdfService` — lógica de guardado en disco específica de la app
- `Program.cs` — runner CLI

## Proyecto consumidor

`C:\Users\adria\GitHub\HMG\HMG.API\HMG.API\` usa este paquete vía `PackageReference` en `HMGAPI.csproj`. El único punto de uso es `AuditoriaCFDIService.cs` — método `SubirXmls`.
