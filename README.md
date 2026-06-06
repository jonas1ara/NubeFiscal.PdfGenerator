# NubeFiscal.PdfGenerator

Genera representaciones impresas en PDF de CFDIs 4.0 del SAT de México.

<img width="1009" height="746" alt="factura-ejemplo01" src="https://github.com/user-attachments/assets/b8eb6213-dd84-4ebb-9dd5-a4cfa0b2e33d" />

_Representación gráfica proporcionada por el SAT_

**El único paquete NuGet con soporte completo para el diseño oficial del SAT:**
- Comprobantes de **Ingreso, Egreso y Traslado**
- **Complemento de Pago 2.0** (recepción de pagos, documentos relacionados, parcialidades)
- **Múltiples conceptos** con desglose de impuestos por concepto
- Código QR de verificación SAT
- Comprobantes cancelados con fecha de cancelación
- Exportación (No aplica / Definitiva / Temporal)

## Instalación

```bash
dotnet add package NubeFiscal.PdfGenerator
```

## Uso

### Desde XML crudo

```csharp
using NubeFiscal.PdfGenerator.Services;

var xmlContent = File.ReadAllText("mi-cfdi.xml");
var cfdi       = CfdiXmlParser.FromXml(xmlContent);
var pdfBytes   = PdfBuilder.Construir(cfdi);

File.WriteAllBytes("factura.pdf", pdfBytes);
```

### Desde base de datos (poblando el modelo manualmente)

```csharp
using NubeFiscal.PdfGenerator.Models;
using NubeFiscal.PdfGenerator.Services;

// Traes los datos de tu BD y mapeas al modelo
var cfdi = new CfdiPdfData
{
    UUID            = "A1B2C3D4-...",
    RFCEmisor       = "XAXX010101000",
    NombreEmisor    = "MI EMPRESA SA DE CV",
    RFCReceptor     = "GODE561231GR8",
    NombreReceptor  = "CLIENTE EJEMPLO",
    FechaEmision    = DateTime.Parse("2025-03-15T10:30:00"),
    FechaTimbrado   = DateTime.Parse("2025-03-15T10:31:55"),
    TipoComprobante = "I",
    SubTotal        = 1000m,
    Total           = 1160m,
    // ... resto de campos
    Conceptos =
    [
        new ConceptoPdf
        {
            ClaveProdServ = "43232408",
            Descripcion   = "Servicio de consultoría",
            Cantidad      = 1,
            ValorUnitario = 1000m,
            Importe       = 1000m,
            Traslados     = [ new TrasladoPdf { Impuesto = "002", TasaOCuota = "0.160000", Importe = 160m } ]
        }
    ]
};

// Si tienes el XML disponible, enriquece automáticamente
// CfdiXmlParser.EnriquecerDesdeXml(cfdi);

var pdfBytes = PdfBuilder.Construir(cfdi);
```

### Complemento de Pago

El complemento de pago se parsea automáticamente desde el XML cuando `TipoDeComprobante = "P"`. Si construyes el modelo manualmente, rellena `cfdi.ComplementoPago`:

```csharp
cfdi.TipoComprobante = "P";
cfdi.ComplementoPago = new ComplementoPagoPdf
{
    Version = "2.0",
    Totales = new TotalesPagoPdf { MontoTotalPagos = 1160m, ... },
    Pagos   =
    [
        new PagoPdf
        {
            FechaPago    = DateTime.Parse("2025-04-01"),
            FormaDePagoP = "03",
            MonedaP      = "MXN",
            Monto        = 1160m,
            DoctoRelacionados =
            [
                new DoctoRelacionadoPdf
                {
                    IdDocumento = "A1B2C3D4-...",
                    ImpPagado   = 1160m,
                    // ...
                }
            ]
        }
    ]
};
```

## Dependencias

- [QuestPDF](https://www.questpdf.com/) — renderizado PDF (licencia Community)
- [QRCoder](https://github.com/codebude/QRCoder) — código QR de verificación SAT

## Licencia

MIT — open source bajo NubeFiscal.
