# NubeFiscal.PdfGenerator

Genera representaciones impresas en PDF de **CFDIs 4.0 del SAT** de México, cumpliendo con todos los requisitos oficiales del Servicio de Administración Tributaria.

[![NuGet](https://img.shields.io/nuget/v/NubeFiscal.PdfGenerator)](https://www.nuget.org/packages/NubeFiscal.PdfGenerator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NubeFiscal.PdfGenerator)](https://www.nuget.org/packages/NubeFiscal.PdfGenerator)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## ¿Qué es una representación impresa de un CFDI?

El SAT establece que toda factura electrónica (CFDI) debe poder representarse en papel o formato digital legible. Esta representación impresa debe contener de forma visible y ordenada todos los datos del comprobante, el código QR de verificación y la leyenda **"Este documento es una representación impresa de un CFDI"**.

<img width="1009" height="746" alt="factura-ejemplo01" src="https://github.com/user-attachments/assets/b8eb6213-dd84-4ebb-9dd5-a4cfa0b2e33d" />

_Ejemplo de representación impresa (diseño oficial SAT)_

---

<details>
<summary><strong>Requisitos oficiales del SAT que cubre este paquete</strong></summary>

<br>

Según el [artículo 29-A del CFF](https://www.sat.gob.mx/minisitio/Factura/solicita_requisitos.htm), la representación impresa debe incluir:

### Datos del Emisor ①
- RFC del emisor
- Nombre o razón social
- Régimen fiscal según la Ley del ISR
- Código postal del domicilio fiscal

### Datos del Receptor ④
- RFC del receptor
- Nombre o razón social
- Régimen fiscal del receptor
- Código postal del domicilio fiscal del receptor
- Uso del CFDI

### Datos del Comprobante ③
- Folio fiscal (UUID) ②
- Código postal, fecha y hora de emisión
- Efecto del comprobante (Ingreso / Egreso / Traslado / Pago / Nómina)
- No. de serie del CSD del emisor ⑧②
- Exportación

### Conceptos ⑤
| Campo | Descripción |
|---|---|
| Clave del producto y/o servicio | Catálogo SAT |
| No. de identificación | SKU o clave interna |
| Cantidad | Con 6 decimales |
| Clave de unidad | Catálogo SAT (H87, E48, etc.) |
| Unidad | Descripción de la unidad |
| Valor unitario | Con 6 decimales |
| Importe | Con 2 decimales |
| Descuento | Cuando aplique |
| Objeto de impuesto | 01 / 02 / 03 / 04 |

### Impuestos por Concepto ⑥
Para cada concepto se detalla:
- Impuesto (ISR / IVA / IEPS)
- Tipo (Traslado / Retención)
- Base, Tipo Factor, Tasa o Cuota, Importe

### Totales ⑦
- Subtotal
- Descuento (si aplica)
- Impuestos Trasladados desglosados por tasa (IVA 16%, 8%, 0%, Exento)
- **Total**

### Sellos y certificación ⑧
- Sello digital del CFDI ②
- Sello digital del SAT
- Cadena original del complemento de certificación ⑧⑤
- No. de serie del certificado SAT ⑧②
- RFC del proveedor de certificación (PAC)
- Fecha y hora de certificación ⑧④
- **Código QR** de verificación en el portal del SAT ⑧①

</details>

---

## Lo que hace único a este paquete

Es el único NuGet con soporte completo para el diseño oficial del SAT CFDI 4.0:

| Tipo de comprobante | Soportado |
|---|---|
| Ingreso (I) | ✅ |
| Egreso (E) | ✅ |
| Traslado (T) | ✅ |
| Pago — Complemento de Pago 2.0 (P) | ✅ |
| Múltiples conceptos con impuestos por concepto | ✅ |
| Comprobantes cancelados con fecha de cancelación | ✅ |
| Exportación (No aplica / Definitiva / Temporal) | ✅ |
| Código QR de verificación SAT | ✅ |
| CFDI 3.3 (compatibilidad) | ✅ |

---

## Instalación

```bash
dotnet add package NubeFiscal.PdfGenerator
```

---

## Uso

### Desde XML crudo

La forma más sencilla: pasas el XML del CFDI y obtienes el PDF.

```csharp
using NubeFiscal.PdfGenerator.Services;

var xmlContent = await File.ReadAllTextAsync("mi-cfdi.xml");
var cfdi       = CfdiXmlParser.FromXml(xmlContent);
var pdfBytes   = PdfBuilder.Construir(cfdi);

await File.WriteAllBytesAsync("factura.pdf", pdfBytes);
```

### Desde base de datos

Si guardas los datos del CFDI en tu BD, construyes el modelo directamente sin necesidad del XML:

```csharp
using NubeFiscal.PdfGenerator.Models;
using NubeFiscal.PdfGenerator.Services;

var cfdi = new CfdiPdfData
{
    UUID                 = "704E83C3-C1CA-41C0-8225-E0AFE484969A",
    RFCEmisor            = "GOHE840111441",
    NombreEmisor         = "JOSE ELIAZAR GOMEZ HERERA",
    RegimenFiscalEmisor  = "621",
    LugarExpedicion      = "07530",
    RFCReceptor          = "WDM890106650",
    NombreReceptor       = "Walolar México S. DE R.L.DE C.V.",
    RegimenFiscalReceptor = "601",
    CodigoPostalReceptor  = "07530",
    UsoCFDI              = "I08",
    FechaEmision         = new DateTime(2019, 6, 27, 20, 7, 1),
    FechaTimbrado        = new DateTime(2019, 6, 27, 20, 11, 11),
    TipoComprobante      = "I",
    Moneda               = "MXN",
    MetodoPago           = "PPD",
    SubTotal             = 55000m,
    TotalImpuestosTrasladados = 8800m,
    Total                = 63800m,
    NoCertificado        = "00001000000413439058",
    NoCertificadoSAT     = "00001000000403258748",
    SelloCFDI            = "AiDHUEggSow8toaoY7t3a4vpcwkI3KxTDHOZrXC/4oaZPXpjin...",
    SelloSAT             = "SkjptLpfv6n1ePflhDfyMyxD6lSnveS6apJ+ZDJmNZrT0znQBepHg...",
    Conceptos =
    [
        new ConceptoPdf
        {
            ClaveProdServ = "56121900",
            Cantidad      = 1,
            ClaveUnidad   = "H87",
            Descripcion   = "Maniobras de Mobiliario",
            ValorUnitario = 55000m,
            Importe       = 55000m,
            ObjetoImp     = "02",
            Traslados     =
            [
                new TrasladoPdf { Impuesto = "002", TasaOCuota = "0.160000", Importe = 8800m }
            ]
        }
    ]
};

var pdfBytes = PdfBuilder.Construir(cfdi);
```

### Enriquecer desde XML cuando ya tienes datos de BD

Si tu registro en BD incluye el XML original pero ya tienes los campos básicos:

```csharp
cfdi.XmlCFDI = xmlGuardadoEnBd;
CfdiXmlParser.EnriquecerDesdeXml(cfdi); // sobrescribe solo lo que parsea del XML
var pdfBytes = PdfBuilder.Construir(cfdi);
```

### Complemento de Pago 2.0

Cuando `TipoComprobante = "P"` y el XML incluye el complemento de pago, se parsea y renderiza automáticamente desde `CfdiXmlParser.FromXml()`. Para construirlo manualmente:

```csharp
cfdi.TipoComprobante = "P";
cfdi.ComplementoPago = new ComplementoPagoPdf
{
    Version = "2.0",
    Totales = new TotalesPagoPdf
    {
        TotalTrasladosBaseIVA16     = 55000m,
        TotalTrasladosImpuestoIVA16 = 8800m,
        MontoTotalPagos             = 63800m
    },
    Pagos =
    [
        new PagoPdf
        {
            FechaPago    = new DateTime(2019, 7, 15),
            FormaDePagoP = "03",  // Transferencia electrónica
            MonedaP      = "MXN",
            Monto        = 63800m,
            DoctoRelacionados =
            [
                new DoctoRelacionadoPdf
                {
                    IdDocumento      = "704E83C3-C1CA-41C0-8225-E0AFE484969A",
                    MonedaDR         = "MXN",
                    NumParcialidad   = 1,
                    ImpSaldoAnt      = 63800m,
                    ImpPagado        = 63800m,
                    ImpSaldoInsoluto = 0m,
                    ObjetoImpDR      = "02"
                }
            ]
        }
    ]
};
```

---

## Catálogos incluidos

El paquete resuelve automáticamente las claves del SAT a su descripción completa:

- **Uso CFDI:** G01, G02, G03, I01–I08, D01–D10, P01, S01, CP01
- **Régimen fiscal:** 601–627 (todos los regímenes vigentes)
- **Forma de pago:** 01–99
- **Método de pago:** PUE, PPD
- **Tipo de comprobante:** I, E, T, P, N
- **Impuestos:** 001 ISR, 002 IVA, 003 IEPS
- **Objeto de impuesto:** 01–04
- **Monedas:** MXN, USD, EUR, CAD, GBP, JPY, CNY y más
- **Exportación:** No aplica / Definitiva / Temporal

---

## Dependencias

| Paquete | Versión | Uso |
|---|---|---|
| [QuestPDF](https://www.questpdf.com/) | 2024.10.4 | Renderizado PDF (licencia Community) |
| [QRCoder](https://github.com/codebude/QRCoder) | 1.6.0 | Código QR de verificación SAT |

---

## ¿Por qué existe este paquete?

Trabajando en un proyecto propio necesitaba generar las representaciones gráficas de CFDIs a partir de sus XMLs. El problema: **ninguna librería gratuita las generaba con el diseño oficial del SAT**. La gran mayoría de las soluciones disponibles te obligan a pagar un servicio externo y esperar que te liberen un API Key para poder usarlo.

Decidí construirlo desde cero, con el diseño correcto, y liberarlo como open source para que cualquier desarrollador en México pueda usarlo sin depender de terceros ni pagar por algo que debería ser libre.

---

## Contribuciones

¡Las PRs son bienvenidas! Si tienes mejoras al diseño, soporte para nuevos complementos, correcciones o cualquier idea, adelante:

1. Haz fork del repositorio
2. Crea tu rama: `git checkout -b mi-mejora`
3. Haz commit de tus cambios: `git commit -m "Descripción de la mejora"`
4. Abre un Pull Request

---

## Licencia

MIT — open source bajo [Nube Fiscal](https://github.com/Nube-Fiscal).
