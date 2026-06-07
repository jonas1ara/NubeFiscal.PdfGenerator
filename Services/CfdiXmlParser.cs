using System.Xml.Linq;
using NubeFiscal.PdfGenerator.Models;

namespace NubeFiscal.PdfGenerator.Services;

/// <summary>
/// Parsea el XML de un CFDI 4.0 (o 3.3) y devuelve un <see cref="CfdiPdfData"/>
/// listo para pasarle a <see cref="PdfBuilder.Construir"/>.
/// </summary>
public static class CfdiXmlParser
{
    /// <summary>
    /// Construye un <see cref="CfdiPdfData"/> a partir del contenido XML de un CFDI.
    /// </summary>
    /// <param name="xmlContent">Contenido XML completo del CFDI (string).</param>
    /// <returns>Modelo poblado con todos los campos necesarios para generar el PDF.</returns>
    public static CfdiPdfData FromXml(string xmlContent)
    {
        var cfdi = new CfdiPdfData
        {
            XmlCFDI       = xmlContent,
            RFCRazonSocial = string.Empty,
            AnioMes       = DateTime.Today.ToString("yyyy-MM"),
            TipoDescarga  = "Emitido"
        };

        ParseXml(cfdi);

        if (string.IsNullOrWhiteSpace(cfdi.RFCRazonSocial))
            cfdi.RFCRazonSocial = cfdi.RFCReceptor ?? cfdi.RFCEmisor ?? string.Empty;

        if (cfdi.FechaEmision != default)
            cfdi.AnioMes = cfdi.FechaEmision.ToString("yyyy-MM");

        return cfdi;
    }

    /// <summary>
    /// Rellena los campos parseados de XML en un <see cref="CfdiPdfData"/> existente.
    /// Útil cuando el registro ya viene de base de datos y solo falta parsear el XML.
    /// </summary>
    public static void EnriquecerDesdeXml(CfdiPdfData cfdi)
    {
        if (!string.IsNullOrWhiteSpace(cfdi.XmlCFDI))
            ParseXml(cfdi);
    }

    private static void ParseXml(CfdiPdfData cfdi)
    {
        try
        {
            var doc  = XDocument.Parse(cfdi.XmlCFDI!);
            var root = doc.Root;
            if (root is null) return;

            XNamespace ns = root.Name.Namespace;
            if (ns == XNamespace.None)
            {
                ns = "http://www.sat.gob.mx/cfd/4";
                if (root.Element(ns + "Emisor") is null)
                    ns = "http://www.sat.gob.mx/cfd/3";
            }

            XNamespace tfdNs = "http://www.sat.gob.mx/TimbreFiscalDigital";

            cfdi.VersionCfdi     = root.Attribute("Version")?.Value;
            cfdi.SelloCFDI       = root.Attribute("Sello")?.Value;
            cfdi.NoCertificado   = root.Attribute("NoCertificado")?.Value;
            cfdi.LugarExpedicion = root.Attribute("LugarExpedicion")?.Value;
            cfdi.Exportacion     = root.Attribute("Exportacion")?.Value;
            cfdi.Serie           = root.Attribute("Serie")?.Value;
            cfdi.Folio           = root.Attribute("Folio")?.Value;
            cfdi.Moneda          = root.Attribute("Moneda")?.Value;
            cfdi.TipoComprobante = root.Attribute("TipoDeComprobante")?.Value
                                   ?? root.Attribute("TipoComprobante")?.Value;

            if (DateTime.TryParse(root.Attribute("Fecha")?.Value, out var fechaEmision))
                cfdi.FechaEmision = fechaEmision;

            TryDecimal(root.Attribute("Total")?.Value,   out var total);   cfdi.Total    = total;
            TryDecimal(root.Attribute("SubTotal")?.Value, out var subT);   cfdi.SubTotal  = subT;
            TryDecimal(root.Attribute("Descuento")?.Value, out var desc);  cfdi.Descuento = desc;

            cfdi.FormaPago  = root.Attribute("FormaPago")?.Value;
            cfdi.MetodoPago = root.Attribute("MetodoPago")?.Value;

            var emisor = root.Element(ns + "Emisor");
            if (emisor != null)
            {
                cfdi.RFCEmisor           = emisor.Attribute("Rfc")?.Value;
                cfdi.NombreEmisor        = emisor.Attribute("Nombre")?.Value;
                cfdi.RegimenFiscalEmisor = emisor.Attribute("RegimenFiscal")?.Value;
                cfdi.CodigoPostalEmisor  = root.Attribute("LugarExpedicion")?.Value;
            }

            var receptor = root.Element(ns + "Receptor");
            if (receptor != null)
            {
                cfdi.RFCReceptor           = receptor.Attribute("Rfc")?.Value;
                cfdi.NombreReceptor        = receptor.Attribute("Nombre")?.Value;
                cfdi.RegimenFiscalReceptor = receptor.Attribute("RegimenFiscalReceptor")?.Value;
                cfdi.UsoCFDI               = receptor.Attribute("UsoCFDI")?.Value;
                cfdi.CodigoPostalReceptor  = receptor.Attribute("DomicilioFiscalReceptor")?.Value;
            }

            var impuestos = root.Element(ns + "Impuestos");
            if (impuestos != null)
            {
                TryDecimal(impuestos.Attribute("TotalImpuestosTrasladados")?.Value, out var iva);
                cfdi.TotalImpuestosTrasladados = iva;
                TryDecimal(impuestos.Attribute("TotalImpuestosRetenidos")?.Value, out var ret);
                cfdi.TotalImpuestosRetenidos = ret;
            }

            var conceptosEl = root.Element(ns + "Conceptos");
            if (conceptosEl != null)
            {
                foreach (var cEl in conceptosEl.Elements(ns + "Concepto"))
                {
                    var concepto = new ConceptoPdf
                    {
                        ClaveProdServ    = cEl.Attribute("ClaveProdServ")?.Value,
                        NoIdentificacion = cEl.Attribute("NoIdentificacion")?.Value,
                        ClaveUnidad      = cEl.Attribute("ClaveUnidad")?.Value,
                        Unidad           = cEl.Attribute("Unidad")?.Value,
                        Descripcion      = cEl.Attribute("Descripcion")?.Value,
                        ObjetoImp        = cEl.Attribute("ObjetoImp")?.Value,
                    };

                    TryDecimal(cEl.Attribute("Cantidad")?.Value,     out var cant); concepto.Cantidad      = cant ?? 0m;
                    TryDecimal(cEl.Attribute("ValorUnitario")?.Value, out var vu);  concepto.ValorUnitario = vu   ?? 0m;
                    TryDecimal(cEl.Attribute("Importe")?.Value,       out var imp); concepto.Importe       = imp  ?? 0m;
                    TryDecimal(cEl.Attribute("Descuento")?.Value,     out var dsc); concepto.Descuento     = dsc;

                    var cImpuestos = cEl.Element(ns + "Impuestos");
                    var cTraslados = cImpuestos?.Element(ns + "Traslados");
                    if (cTraslados != null)
                    {
                        foreach (var tEl in cTraslados.Elements(ns + "Traslado"))
                        {
                            TryDecimal(tEl.Attribute("Importe")?.Value, out var ti);
                            concepto.Traslados.Add(new TrasladoPdf
                            {
                                Impuesto   = tEl.Attribute("Impuesto")?.Value,
                                TasaOCuota = tEl.Attribute("TasaOCuota")?.Value,
                                Importe    = ti,
                            });
                        }
                    }

                    var cRetenciones = cImpuestos?.Element(ns + "Retenciones");
                    if (cRetenciones != null)
                    {
                        foreach (var rEl in cRetenciones.Elements(ns + "Retencion"))
                        {
                            TryDecimal(rEl.Attribute("Importe")?.Value, out var ri);
                            TryDecimal(rEl.Attribute("Base")?.Value,    out var rb);
                            concepto.Retenciones.Add(new RetencionPdf
                            {
                                Impuesto = rEl.Attribute("Impuesto")?.Value,
                                Base     = rb,
                                Importe  = ri,
                            });
                        }
                    }

                    cfdi.Conceptos.Add(concepto);
                }
            }

            var complemento = root.Element(ns + "Complemento");
            if (complemento != null)
            {
                var tfd = complemento.Element(tfdNs + "TimbreFiscalDigital");
                if (tfd != null)
                {
                    cfdi.UUID                     = tfd.Attribute("UUID")?.Value ?? cfdi.UUID;
                    cfdi.SelloSAT                 = tfd.Attribute("SelloSAT")?.Value;
                    cfdi.NoCertificadoSAT         = tfd.Attribute("NoCertificadoSAT")?.Value;
                    cfdi.RfcProveedorCertificacion = tfd.Attribute("RfcProvCertif")?.Value;

                    if (DateTime.TryParse(tfd.Attribute("FechaTimbrado")?.Value, out var ft))
                        cfdi.FechaTimbrado = ft;

                    var version  = tfd.Attribute("Version")?.Value ?? "";
                    var fechaTfd = tfd.Attribute("FechaTimbrado")?.Value ?? "";
                    cfdi.CadenaOriginal = $"||{version}|{cfdi.UUID}|{fechaTfd}|{cfdi.RfcProveedorCertificacion}|{cfdi.SelloCFDI}|{cfdi.NoCertificadoSAT}||";
                }

                XNamespace pagoNs = "http://www.sat.gob.mx/Pagos20";
                var pagosEl = complemento.Element(pagoNs + "Pagos");
                if (pagosEl != null)
                    cfdi.ComplementoPago = ParseComplementoPago(pagosEl, pagoNs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NubeFiscal.PdfGenerator] WARN: Error parseando XML para UUID={cfdi.UUID}: {ex.Message}");
        }
    }

    private static void TryDecimal(string? s, out decimal? result)
    {
        result = decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static ComplementoPagoPdf ParseComplementoPago(XElement pagosEl, XNamespace ns)
    {
        static decimal? D(string? s) =>
            decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        var comp = new ComplementoPagoPdf
        {
            Version = pagosEl.Attribute("Version")?.Value ?? "2.0"
        };

        var totEl = pagosEl.Element(ns + "Totales");
        if (totEl != null)
        {
            comp.Totales = new TotalesPagoPdf
            {
                TotalRetencionesIVA         = D(totEl.Attribute("TotalRetencionesIVA")?.Value),
                TotalRetencionesISR         = D(totEl.Attribute("TotalRetencionesISR")?.Value),
                TotalRetencionesIEPS        = D(totEl.Attribute("TotalRetencionesIEPS")?.Value),
                TotalTrasladosBaseIVA16     = D(totEl.Attribute("TotalTrasladosBaseIVA16")?.Value),
                TotalTrasladosImpuestoIVA16 = D(totEl.Attribute("TotalTrasladosImpuestoIVA16")?.Value),
                TotalTrasladosBaseIVA8      = D(totEl.Attribute("TotalTrasladosBaseIVA8")?.Value),
                TotalTrasladosImpuestoIVA8  = D(totEl.Attribute("TotalTrasladosImpuestoIVA8")?.Value),
                TotalTrasladosBaseIVA0      = D(totEl.Attribute("TotalTrasladosBaseIVA0")?.Value),
                TotalTrasladosImpuestoIVA0  = D(totEl.Attribute("TotalTrasladosImpuestoIVA0")?.Value),
                TotalTrasladosBaseIVAExento = D(totEl.Attribute("TotalTrasladosBaseIVAExento")?.Value),
                MontoTotalPagos             = D(totEl.Attribute("MontoTotalPagos")?.Value) ?? 0m,
            };
        }

        foreach (var pagoEl in pagosEl.Elements(ns + "Pago"))
        {
            var pago = new PagoPdf
            {
                FormaDePagoP = pagoEl.Attribute("FormaDePagoP")?.Value,
                MonedaP      = pagoEl.Attribute("MonedaP")?.Value,
                NumOperacion = pagoEl.Attribute("NumOperacion")?.Value,
                TipoCambioP  = D(pagoEl.Attribute("TipoCambioP")?.Value),
                Monto        = D(pagoEl.Attribute("Monto")?.Value) ?? 0m,
            };
            if (DateTime.TryParse(pagoEl.Attribute("FechaPago")?.Value, out var fp))
                pago.FechaPago = fp;

            foreach (var drEl in pagoEl.Elements(ns + "DoctoRelacionado"))
            {
                var dr = new DoctoRelacionadoPdf
                {
                    IdDocumento      = drEl.Attribute("IdDocumento")?.Value,
                    Serie            = drEl.Attribute("Serie")?.Value,
                    Folio            = drEl.Attribute("Folio")?.Value,
                    MonedaDR         = drEl.Attribute("MonedaDR")?.Value,
                    ObjetoImpDR      = drEl.Attribute("ObjetoImpDR")?.Value,
                    EquivalenciaDR   = D(drEl.Attribute("EquivalenciaDR")?.Value),
                    ImpSaldoAnt      = D(drEl.Attribute("ImpSaldoAnt")?.Value),
                    ImpPagado        = D(drEl.Attribute("ImpPagado")?.Value),
                    ImpSaldoInsoluto = D(drEl.Attribute("ImpSaldoInsoluto")?.Value),
                };
                if (int.TryParse(drEl.Attribute("NumParcialidad")?.Value, out var np))
                    dr.NumParcialidad = np;

                var impDR = drEl.Element(ns + "ImpuestosDR");
                if (impDR != null)
                {
                    foreach (var r in impDR.Element(ns + "RetencionesDR")?.Elements(ns + "RetencionDR") ?? [])
                        dr.RetencionesDR.Add(new ImpuestoPagoDetalladoPdf
                        {
                            Impuesto   = r.Attribute("ImpuestoDR")?.Value,
                            TipoFactor = r.Attribute("TipoFactorDR")?.Value,
                            Base       = D(r.Attribute("BaseDR")?.Value),
                            TasaOCuota = D(r.Attribute("TasaOCuotaDR")?.Value),
                            Importe    = D(r.Attribute("ImporteDR")?.Value),
                        });
                    foreach (var t in impDR.Element(ns + "TrasladosDR")?.Elements(ns + "TrasladoDR") ?? [])
                        dr.TrasladosDR.Add(new ImpuestoPagoDetalladoPdf
                        {
                            Impuesto   = t.Attribute("ImpuestoDR")?.Value,
                            TipoFactor = t.Attribute("TipoFactorDR")?.Value,
                            Base       = D(t.Attribute("BaseDR")?.Value),
                            TasaOCuota = D(t.Attribute("TasaOCuotaDR")?.Value),
                            Importe    = D(t.Attribute("ImporteDR")?.Value),
                        });
                }
                pago.DoctoRelacionados.Add(dr);
            }

            var impP = pagoEl.Element(ns + "ImpuestosP");
            if (impP != null)
            {
                foreach (var r in impP.Element(ns + "RetencionesP")?.Elements(ns + "RetencionP") ?? [])
                    pago.RetencionesP.Add(new ImpuestoPagoSimplePdf
                    {
                        Impuesto = r.Attribute("ImpuestoP")?.Value,
                        Importe  = D(r.Attribute("ImporteP")?.Value),
                    });
                foreach (var t in impP.Element(ns + "TrasladosP")?.Elements(ns + "TrasladoP") ?? [])
                    pago.TrasladosP.Add(new ImpuestoPagoDetalladoPdf
                    {
                        Impuesto   = t.Attribute("ImpuestoP")?.Value,
                        TipoFactor = t.Attribute("TipoFactorP")?.Value,
                        Base       = D(t.Attribute("BaseP")?.Value),
                        TasaOCuota = D(t.Attribute("TasaOCuotaP")?.Value),
                        Importe    = D(t.Attribute("ImporteP")?.Value),
                    });
            }

            comp.Pagos.Add(pago);
        }

        return comp;
    }
}
