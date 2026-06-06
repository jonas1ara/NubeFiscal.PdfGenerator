using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using NubeFiscal.PdfGenerator.Models;
using System.Globalization;
using System.Text;

namespace NubeFiscal.PdfGenerator.Services;

public static class PdfBuilder
{
    private static readonly CultureInfo MxCulture = CultureInfo.GetCultureInfo("es-MX");
    private const float HeaderLabelWidth = 92f;
    private const float RightHeaderLabelWidth = 138f;

    static PdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Construir(CfdiPdfData cfdi)
    {
        var qrBytes = GenerarQr(cfdi);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(1.1f, Unit.Centimetre);
                page.MarginRight(1.1f, Unit.Centimetre);
                page.MarginBottom(1.1f, Unit.Centimetre);
                page.MarginTop(0.7f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial"));
                page.Header().Element(container => ComposePageHeader(container, cfdi));
                page.Content().PaddingTop(0).Element(container => ComposeContent(container, cfdi, qrBytes));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeContent(IContainer container, CfdiPdfData cfdi, byte[] qrBytes)
    {
        container.Column(col =>
        {
            if (string.Equals(cfdi.EstatusSAT, "Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                col.Item().Element(Box).Background("#F2F2F2").Padding(6).Text(
                    $"COMPROBANTE CANCELADO{(cfdi.FechaCancelacion.HasValue ? $" - Fecha de cancelación: {cfdi.FechaCancelacion:dd/MM/yyyy}" : string.Empty)}")
                    .Bold();
            }

            col.Item().PaddingTop(6).Element(container => ComposeSatHeader(container, cfdi));
            col.Item().PaddingTop(6).PaddingLeft(4).PaddingRight(4).Text("Conceptos").Bold().FontSize(9.5f);
            col.Item().PaddingTop(20).Element(container => ComposeConceptosTable(container, cfdi));

            bool esComplementoPago = string.Equals(cfdi.TipoComprobante, "P", StringComparison.OrdinalIgnoreCase)
                                     && cfdi.ComplementoPago is not null;

            col.Item().PaddingTop(10).PaddingBottom(18).PaddingLeft(4).PaddingRight(4).Row(row =>
            {
                row.RelativeItem().Element(container => ComposeDatosPagoBox(container, cfdi, esComplementoPago));
                row.ConstantItem(236).PaddingLeft(6).Element(container => ComposeTotalesBox(container, cfdi));
            });

            if (esComplementoPago)
            {
                col.Item().PaddingTop(4).Element(container => ComposeComplementoPago(container, cfdi));
                col.Item().PageBreak();
            }

            col.Item().PaddingTop(6).Element(container => ComposeFinalSection(container, cfdi, qrBytes));
        });
    }

    private static void ComposeSatHeader(IContainer container, CfdiPdfData cfdi)
    {
        container.PaddingLeft(4).PaddingRight(4).Row(row =>
        {
            row.RelativeItem(0.92f).Column(col =>
            {
                col.Item().Element(c => HeaderField(c, "Nombre emisor:", ValueOrDash(cfdi.NombreEmisor)));
                if (!string.IsNullOrWhiteSpace(cfdi.Folio))
                    col.Item().Element(c => HeaderField(c, "Folio:", cfdi.Folio));
                col.Item().Element(c => HeaderField(c, "RFC receptor:", ValueOrDash(cfdi.RFCReceptor)));
                col.Item().Element(c => HeaderField(c, "Nombre receptor:", ValueOrDash(cfdi.NombreReceptor)));
                col.Item().Element(c => HeaderField(c, "Código postal del\nreceptor:", ValueOrDash(cfdi.CodigoPostalReceptor)));
                col.Item().Element(c => HeaderField(c, "Régimen fiscal\nreceptor:", DescripcionRegimenFiscal(cfdi.RegimenFiscalReceptor)));
                col.Item().Element(c => HeaderField(c, "Uso CFDI:", DescripcionUsoCfdi(cfdi.UsoCFDI)));
            });
            row.ConstantItem(4);
            row.RelativeItem(1.08f).Column(col =>
            {
                col.Item().Element(c => HeaderField(c, "No. de serie del CSD:", ValueOrDash(cfdi.NoCertificado), RightHeaderLabelWidth));
                if (!string.IsNullOrWhiteSpace(cfdi.Serie))
                    col.Item().Element(c => HeaderField(c, "Serie:", cfdi.Serie, RightHeaderLabelWidth));
                col.Item().Element(c => HeaderField(c, "Código postal, fecha y hora de emisión:", $"{ValueOrDash(cfdi.LugarExpedicion)} {FormatDateTimeCompact(cfdi.FechaEmision)}".Trim(), RightHeaderLabelWidth));
                col.Item().Element(c => HeaderField(c, "Efecto de comprobante:", DescripcionTipoComprobante(cfdi.TipoComprobante), RightHeaderLabelWidth));
                col.Item().Element(c => HeaderField(c, "Régimen fiscal:", DescripcionRegimenFiscal(cfdi.RegimenFiscalEmisor), RightHeaderLabelWidth));
                col.Item().Element(c => HeaderField(c, "Exportación:", MapExportacion(cfdi.Exportacion), RightHeaderLabelWidth));
            });
        });
    }

    private static void ComposePageHeader(IContainer container, CfdiPdfData cfdi)
    {
        container.PaddingLeft(4).PaddingRight(4).PaddingBottom(0).PaddingTop(14).Row(row =>
        {
            row.RelativeItem(0.92f).Row(left =>
            {
                left.ConstantItem(HeaderLabelWidth).Text("RFC emisor: ").Bold().FontSize(8f);
                left.RelativeItem().Text(ValueOrDash(cfdi.RFCEmisor)).FontSize(8f);
            });

            row.ConstantItem(4.3f);

            row.RelativeItem(1.08f).Row(right =>
            {
                right.ConstantItem(RightHeaderLabelWidth).Text("Folio fiscal: ").Bold().FontSize(8f);
                right.RelativeItem().Text(ValueOrDash(cfdi.UUID)).FontSize(6.9f);
            });
        });
    }

    private static void ComposeConceptosTable(IContainer container, CfdiPdfData cfdi)
    {
        container.Decoration(decoration =>
        {
            decoration.Before().Element(ComposeConceptosHeader);

            decoration.Content().Column(col =>
            {
                if (cfdi.Conceptos.Count == 0)
                {
                    col.Item().Border(0.6f).Padding(6)
                        .Text("No fue posible obtener el detalle de conceptos desde el XML.");
                    return;
                }

                foreach (var concepto in cfdi.Conceptos)
                    col.Item().Element(c => ComposeConceptoBlock(c, concepto));
            });
        });
    }

    private static void ComposeConceptosHeader(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.02f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(0.8f);
                columns.RelativeColumn(0.9f);
                columns.RelativeColumn(0.80f);
                columns.RelativeColumn(0.95f);
                columns.RelativeColumn(0.75f);
                columns.RelativeColumn(0.75f);
                columns.RelativeColumn(1.45f);
            });

            table.Cell().Element(HeaderCell).AlignCenter().Text("Clave del producto y/o servicio").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("No. identificación").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Cantidad").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Clave de unidad").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Unidad").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Valor unitario").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Importe").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Descuento").Bold().FontSize(6f);
            table.Cell().Element(HeaderCell).AlignCenter().Text("Objeto impuesto").Bold().FontSize(6f);
        });
    }

    private static void ComposeConceptoBlock(IContainer container, ConceptoPdf concepto)
    {
        var (identLinea1, identLinea2) = SplitIdentifier(concepto.NoIdentificacion);

        container.BorderLeft(0.6f).BorderRight(0).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.02f);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.80f);
                    columns.RelativeColumn(0.95f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(1.45f);
                });

                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(ValueOrDash(concepto.ClaveProdServ)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(identLinea1).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(FormatSix(concepto.Cantidad)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(ValueOrDash(concepto.ClaveUnidad)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(ValueOrDash(concepto.Unidad)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(FormatSix(concepto.ValorUnitario)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(FormatMoney(concepto.Importe)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text((concepto.Descuento ?? 0m) == 0m ? string.Empty : FormatMoney(concepto.Descuento ?? 0m)).FontSize(5.5f);
                table.Cell().Element(DataCell).BorderTop(0).AlignCenter().Text(MapObjetoImp(concepto.ObjetoImp)).FontSize(5.5f);
            });

            col.Item().BorderTop(0.6f).Row(row =>
            {
                row.RelativeItem(0.58f).BorderRight(0.6f).Column(left =>
                {
                    left.Item().BorderBottom(0.6f).Row(r =>
                    {
                        r.ConstantItem(64.5f).Background("#CFCFCF").BorderRight(0.6f).PaddingHorizontal(2).PaddingVertical(10).AlignCenter().Text("Descripción").Bold().FontSize(6f);
                        r.RelativeItem().PaddingHorizontal(6).BorderColor(Colors.Black).PaddingVertical(10).AlignLeft().Text(ValueOrDash(concepto.Descripcion)).FontSize(5.5f);
                    });
                });

                row.RelativeItem(0.5f).Column(right =>
                {
                    right.Item().Row(r =>
                    {
                        r.RelativeItem(0.9f).PaddingVertical(0.6f).AlignCenter().Text("Impuesto").Bold().FontSize(6f);
                        r.RelativeItem(1.0f).PaddingVertical(0.8f).AlignCenter().Text("Tipo").Bold().FontSize(6f);
                        r.RelativeItem(1.3f).PaddingVertical(0.8f).AlignCenter().Text("Base").Bold().FontSize(6f);
                        r.RelativeItem(1.1f).PaddingVertical(0.8f).AlignCenter().Text("Tipo\nFactor").Bold().FontSize(6f);
                        r.RelativeItem(1.2f).PaddingVertical(0.8f).AlignCenter().Text("Tasa o\nCuota").Bold().FontSize(6f);
                        r.RelativeItem(0.9f).Background(Colors.White).PaddingVertical(0.8f).AlignCenter().Text("Importe").Bold().FontSize(6f);
                    });

                    if (concepto.Traslados.Count == 0)
                    {
                        right.Item().Row(r =>
                        {
                            r.RelativeItem().PaddingHorizontal(0).PaddingVertical(0).AlignCenter().Text(" ").FontSize(8.5f);
                            r.RelativeItem().PaddingHorizontal(0).PaddingVertical(0).AlignCenter().Text("Sin impuestos trasladados.").FontSize(5.5f);
                            r.RelativeItem().PaddingHorizontal(0).PaddingVertical(0).AlignCenter().Text(" ").FontSize(8.5f);
                        });
                    }
                    else
                    {
                        foreach (var traslado in concepto.Traslados)
                        {
                            right.Item().Row(r =>
                            {
                                r.RelativeItem(0.9f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text(MapImpuesto(traslado.Impuesto)).FontSize(5.5f);
                                r.RelativeItem(1.0f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text("Traslado").FontSize(5.5f);
                                r.RelativeItem(1.3f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text(FormatSix(CalcularBaseTraslado(concepto, traslado))).FontSize(5.5f);
                                r.RelativeItem(1.1f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text("Tasa").FontSize(5.5f);
                                r.RelativeItem(1.2f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text(FormatRate(traslado.TasaOCuota)).FontSize(5.5f);
                                r.RelativeItem(0.9f).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().Text(FormatMoney(traslado.Importe ?? 0m)).FontSize(5.5f);
                            });
                        }
                    }
                });
            });

            col.Item().BorderTop(0).Table(ped =>
            {
                ped.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(.860f);
                    columns.RelativeColumn(.722f);
                    columns.RelativeColumn(1.0f);
                    columns.RelativeColumn(1.0f);
                });

                ped.Header(h =>
                {
                    h.Cell().Element(InnerHeaderCell).AlignCenter().Text("Número de pedimento").Bold().FontSize(6f);
                    h.Cell().Element(InnerHeaderCell).AlignCenter().Text("Número de cuenta predial").Bold().FontSize(6f);
                });

                ped.Cell().Element(InnerDataCell).AlignCenter().BorderTop(0).Text(GetPedimentoValue(identLinea2)).FontSize(5.5f);
                ped.Cell().Element(InnerDataCell).AlignCenter().BorderTop(0).Text("").FontSize(5.5f);
                ped.Cell().Element(c => c.Background(Colors.White).BorderLeft(0.6f)).Text("");
                ped.Cell().Element(c => c.Background(Colors.White).Border(0)).Text("");
            });
        });
    }

    private static void ComposeDatosPagoBox(IContainer container, CfdiPdfData cfdi, bool esComplementoPago = false)
    {
        container.Column(col =>
        {
            col.Item().Element(c => HeaderField(c, "Moneda:", DescripcionMoneda(cfdi.Moneda), 82f, 3, 8f));
            if (!esComplementoPago)
            {
                col.Item().Element(c => HeaderField(c, "Forma de pago:", DescripcionFormaPago(cfdi.FormaPago), 82f, 3, 8f));
                col.Item().Element(c => HeaderField(c, "Método de pago:", DescripcionMetodoPago(cfdi.MetodoPago), 82f, 0, 8f));
            }
        });
    }

    private static void ComposeTotalesBox(IContainer container, CfdiPdfData cfdi)
    {
        container.Column(col =>
        {
            col.Item().Element(line => TotalDetailRow(line, "Subtotal", string.Empty, string.Empty, cfdi.SubTotal ?? 0m));

            if ((cfdi.Descuento ?? 0m) != 0m)
                col.Item().Element(line => TotalDetailRow(line, "Descuento", string.Empty, string.Empty, cfdi.Descuento ?? 0m));

            var traslados = GetTrasladosAgrupados(cfdi);
            if (traslados.Count == 0 && (cfdi.TotalImpuestosTrasladados ?? 0m) != 0m)
                col.Item().Element(line => TotalDetailRow(line, "Impuestos trasladados", string.Empty, string.Empty, cfdi.TotalImpuestosTrasladados ?? 0m));
            else
            {
                foreach (var traslado in traslados)
                    col.Item().Element(line => TotalDetailRow(line, "Impuestos trasladados", traslado.Impuesto, traslado.Tasa, traslado.Importe));
            }

            col.Item().Element(line => TotalDetailRow(line, "Total", string.Empty, string.Empty, cfdi.Total ?? 0m, true));
        });
    }

    private static void ComposeFinalSection(IContainer container, CfdiPdfData cfdi, byte[] qrBytes)
    {
        container.PaddingLeft(4).PaddingRight(4).Column(col =>
        {
            col.Item().Text("Sello digital del CFDI:").Bold().FontSize(7f);
            col.Item().PaddingTop(2).Text(text =>
            {
                text.Span(PreventSlashBreaks(cfdi.SelloCFDI)).FontSize(7f);
            });
            col.Item().PaddingTop(6).Text("Sello digital del SAT:").Bold().FontSize(7f);
            col.Item().PaddingTop(2).Text(text =>
            {
                text.Span(PreventSlashBreaks(cfdi.SelloSAT)).FontSize(7f);
            });

            col.Item().PaddingTop(6).Row(row =>
            {
                row.ConstantItem(104).PaddingTop(4).PaddingRight(8).Column(qr =>
                {
                    if (qrBytes is { Length: > 0 })
                        qr.Item().Height(96).Image(qrBytes);
                    else
                        qr.Item().Height(96).Placeholder();
                });

                row.RelativeItem().Column(right =>
                {
                    right.Item().Text("Cadena Original del complemento de certificación digital del SAT:").Bold().FontSize(7f);
                    right.Item().PaddingTop(2).Text(text =>
                    {
                        text.Span(PreventSlashBreaks(cfdi.CadenaOriginal)).FontSize(7f);
                    });
                    right.Item().PaddingTop(4).Row(meta =>
                    {
                        meta.RelativeItem().Column(left =>
                        {
                            left.Item().PaddingBottom(2).Row(info =>
                            {
                                info.ConstantItem(122).Text("RFC del proveedor de certificación: ").Bold().FontSize(7f);
                                info.RelativeItem().Text(GetRfcProveedor(cfdi)).FontSize(7f);
                            });

                            left.Item().PaddingTop(2).Row(info =>
                            {
                                info.ConstantItem(122).Text("No. de serie del certificado SAT: ").Bold().FontSize(7f);
                                info.RelativeItem().Text(ValueOrDash(cfdi.NoCertificadoSAT)).FontSize(7f);
                            });
                        });

                        meta.ConstantItem(180).PaddingLeft(12).Column(cert =>
                        {
                            cert.Item().Text(text =>
                            {
                                text.Span("Fecha y hora de certificación: ").Bold().FontSize(7f);
                                text.Span(FormatDateTime(cfdi.FechaTimbrado)).FontSize(7f);
                            });
                        });
                    });
                });
            });
        });
    }

    private static void ComposeComplementoPago(IContainer container, CfdiPdfData cfdi)
    {
        var comp = cfdi.ComplementoPago!;

        container.Column(col =>
        {
            col.Item().PaddingLeft(4).PaddingBottom(4).Text("Recepción de Pagos").Bold().FontSize(9f);

            col.Item().PaddingBottom(6).Table(table =>
            {
                table.ColumnsDefinition(c => c.ConstantColumn(70));
                table.Cell().Background("#CFCFCF").Border(0.5f).Padding(2).AlignCenter()
                     .Text("Versión").Bold().FontSize(6f);
                table.Cell().Border(0.5f).Padding(2).AlignCenter()
                     .Text(comp.Version).FontSize(6f);
            });

            col.Item().PaddingLeft(4).PaddingBottom(2).Text("Totales").Bold().FontSize(8f);
            col.Item().Element(c => ComposeComplementoTotales(c, comp.Totales));

            foreach (var pago in comp.Pagos)
            {
                col.Item().PaddingLeft(4).PaddingTop(6).Text("Pago").Bold().FontSize(8f);
                col.Item().Element(c => ComposePagoRow(c, pago, cfdi));

                foreach (var dr in pago.DoctoRelacionados)
                {
                    col.Item().PaddingLeft(4).PaddingTop(6).Text("Documento Relacionado").Bold().FontSize(8f);
                    col.Item().Element(c => ComposeDoctoRelacionado(c, dr));

                    col.Item().PaddingLeft(4).PaddingTop(4).Text("Impuestos del Documento Relacionado").Bold().FontSize(8f);
                    if (dr.RetencionesDR.Count > 0)
                    {
                        col.Item().PaddingLeft(4).PaddingTop(2).Text("Retención").Bold().FontSize(7.5f);
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(4.8f).Element(c => ComposeImpuestosDetalladosTable(c, dr.RetencionesDR));
                            r.RelativeItem(3.0f);
                        });
                    }
                    if (dr.TrasladosDR.Count > 0)
                    {
                        col.Item().PaddingLeft(4).PaddingTop(2).Text("Traslados").Bold().FontSize(7.5f);
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(4.8f).Element(c => ComposeImpuestosDetalladosTable(c, dr.TrasladosDR));
                            r.RelativeItem(3.0f);
                        });
                    }
                }

                if (pago.RetencionesP.Count > 0 || pago.TrasladosP.Count > 0)
                {
                    col.Item().PaddingLeft(4).PaddingTop(6).Text("Impuestos del Pago").Bold().FontSize(8f);
                    if (pago.RetencionesP.Count > 0)
                    {
                        col.Item().PaddingLeft(4).PaddingTop(2).Text("Retención").Bold().FontSize(7.5f);
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(2.24f).Element(c => ComposeRetencionesSimpleTable(c, pago.RetencionesP));
                            r.RelativeItem(5.56f);
                        });
                    }
                    if (pago.TrasladosP.Count > 0)
                    {
                        col.Item().PaddingLeft(4).PaddingTop(2).Text("Traslados").Bold().FontSize(7.5f);
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(4.8f).Element(c => ComposeImpuestosDetalladosTable(c, pago.TrasladosP));
                            r.RelativeItem(3.0f);
                        });
                    }
                }
            }
        });
    }

    private static void ComposeComplementoTotales(IContainer container, TotalesPagoPdf? t)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { for (int i = 0; i < 7; i++) c.RelativeColumn(); });

            static IContainer TH(IContainer c) =>
                c.Background("#CFCFCF").Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
            static IContainer TD(IContainer c) =>
                c.Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
            static IContainer Empty(IContainer c) => c.Border(0);

            table.Cell().Element(TH).Text("Total impuestos\nretenidos de IVA").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total impuestos\nretenidos de ISR").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total impuestos\nretenidos de IEPS").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nBase IVA 16%").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nImpuesto IVA 16%").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nbase IVA 8%").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nImpuesto IVA 8%").Bold().FontSize(5f);

            table.Cell().Element(TD).Text(FmtTot(t?.TotalRetencionesIVA)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalRetencionesISR)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalRetencionesIEPS)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosBaseIVA16)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosImpuestoIVA16)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosBaseIVA8)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosImpuestoIVA8)).FontSize(5.5f);

            table.Cell().Element(TH).Text("Total Traslados\nbase IVA 0%").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nImpuesto IVA 0%").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Total Traslados\nBase IVA Exento").Bold().FontSize(5f);
            table.Cell().Element(TH).Text("Monto Total\nde Pagos").Bold().FontSize(5f);
            table.Cell().Element(Empty).Text("");
            table.Cell().Element(Empty).Text("");
            table.Cell().Element(Empty).Text("");

            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosBaseIVA0)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosImpuestoIVA0)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.TotalTrasladosBaseIVAExento)).FontSize(5.5f);
            table.Cell().Element(TD).Text(FmtTot(t?.MontoTotalPagos)).FontSize(5.5f);
            table.Cell().Element(Empty).Text("");
            table.Cell().Element(Empty).Text("");
            table.Cell().Element(Empty).Text("");
        });
    }

    private static string FmtTot(decimal? value) =>
        value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty;

    private static void ComposePagoRow(IContainer container, PagoPdf pago, CfdiPdfData cfdi)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.4f);
                c.RelativeColumn(1.0f);
                c.RelativeColumn(1.0f);
                c.RelativeColumn(0.7f);
                c.RelativeColumn(0.8f);
                c.RelativeColumn(1.0f);
            });

            static IContainer TH(IContainer c) =>
                c.Background("#CFCFCF").Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
            static IContainer TD(IContainer c) =>
                c.Border(0.5f).Padding(2).AlignMiddle().AlignCenter();

            table.Cell().Element(TH).Text("Fecha").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Forma de Pago").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Moneda").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Tipo de cambio").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Monto").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Número de operación").Bold().FontSize(6f);

            table.Cell().Element(TD).Text(pago.FechaPago.HasValue ? pago.FechaPago.Value.ToString("yyyy-MM-dd HH:mm:ss") : "—").FontSize(5.5f);
            table.Cell().Element(TD).Text(DescripcionFormaPago(pago.FormaDePagoP)).FontSize(5.5f);
            table.Cell().Element(TD).Text(DescripcionMoneda(pago.MonedaP)).FontSize(5.5f);
            table.Cell().Element(TD).Text(pago.TipoCambioP.HasValue
                ? pago.TipoCambioP.Value.ToString("G29", CultureInfo.InvariantCulture)
                : "1").FontSize(5.5f);
            table.Cell().Element(TD).Text(pago.Monto.ToString("F2", CultureInfo.InvariantCulture)).FontSize(5.5f);
            table.Cell().Element(TD).Text(pago.NumOperacion ?? string.Empty).FontSize(5.5f);
        });
    }

    private static void ComposeDoctoRelacionado(IContainer container, DoctoRelacionadoPdf dr)
    {
        const float c1 = 2.5f, c2 = 0.7f, c3 = 0.7f, c4 = 0.9f, c5 = 0.9f, c6 = 1.0f, c7 = 1.1f;
        float mitad = c1 + c2 + c3;
        float resto  = c4 + c5 + c6 + c7;

        static IContainer TH(IContainer c) =>
            c.Background("#CFCFCF").Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
        static IContainer TD(IContainer c) =>
            c.Border(0.5f).Padding(2).AlignMiddle().AlignCenter();

        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(c1); c.RelativeColumn(c2); c.RelativeColumn(c3);
                    c.RelativeColumn(c4); c.RelativeColumn(c5); c.RelativeColumn(c6);
                    c.RelativeColumn(c7);
                });

                table.Cell().Element(TH).Text("Identificador del documento").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Serie").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Folio").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Moneda").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Equivalencia DR").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Número de parcialidad").Bold().FontSize(5.5f);
                table.Cell().Element(TH).Text("Importe del saldo anterior").Bold().FontSize(5.5f);

                table.Cell().Element(TD).Text(ValueOrDash(dr.IdDocumento)).FontSize(4.8f);
                table.Cell().Element(TD).Text(dr.Serie ?? string.Empty).FontSize(5.5f);
                table.Cell().Element(TD).Text(dr.Folio ?? string.Empty).FontSize(5.5f);
                table.Cell().Element(TD).Text(DescripcionMoneda(dr.MonedaDR)).FontSize(5.5f);
                table.Cell().Element(TD).Text(dr.EquivalenciaDR.HasValue
                    ? dr.EquivalenciaDR.Value.ToString("G29", CultureInfo.InvariantCulture) : "1").FontSize(5.5f);
                table.Cell().Element(TD).Text(dr.NumParcialidad?.ToString() ?? string.Empty).FontSize(5.5f);
                table.Cell().Element(TD).Text(dr.ImpSaldoAnt.HasValue
                    ? dr.ImpSaldoAnt.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty).FontSize(5.5f);
            });

            col.Item().Row(row =>
            {
                row.RelativeItem(mitad).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    table.Cell().Element(TH).Text("Importe pagado").Bold().FontSize(5.5f);
                    table.Cell().Element(TH).Text("Importe de saldo insoluto").Bold().FontSize(5.5f);
                    table.Cell().Element(TH).Text("Objeto de impuesto").Bold().FontSize(5.5f);

                    table.Cell().Element(TD).Text(dr.ImpPagado.HasValue
                        ? dr.ImpPagado.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty).FontSize(5.5f);
                    table.Cell().Element(TD).Text(dr.ImpSaldoInsoluto.HasValue
                        ? dr.ImpSaldoInsoluto.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty).FontSize(5.5f);
                    table.Cell().Element(TD).Text(MapObjetoImp(dr.ObjetoImpDR)).FontSize(5.5f);
                });

                row.RelativeItem(resto);
            });
        });
    }

    private static void ComposeImpuestosDetalladosTable(IContainer container, List<ImpuestoPagoDetalladoPdf> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.3f);
                c.RelativeColumn(0.8f);
                c.RelativeColumn(0.8f);
                c.RelativeColumn(0.8f);
                c.RelativeColumn(0.8f);
            });

            static IContainer TH(IContainer c) =>
                c.Background("#D7D7D7").Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
            static IContainer TD(IContainer c) =>
                c.Border(0.5f).Padding(2).AlignMiddle().AlignCenter();

            table.Cell().Element(TH).Text("Base").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Impuesto").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Tipo Factor").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Tasa o Cuota").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Importe").Bold().FontSize(6f);

            foreach (var item in items)
            {
                table.Cell().Element(TD).Text(item.Base.HasValue ? FormatSix(item.Base.Value) : "—").FontSize(5.5f);
                table.Cell().Element(TD).Text(MapImpuesto(item.Impuesto)).FontSize(5.5f);
                table.Cell().Element(TD).Text(ValueOrDash(item.TipoFactor)).FontSize(5.5f);
                table.Cell().Element(TD).Text(item.TasaOCuota.HasValue ? FormatSix(item.TasaOCuota.Value) : "—").FontSize(5.5f);
                table.Cell().Element(TD).Text(item.Importe.HasValue ? FormatSix(item.Importe.Value) : "—").FontSize(5.5f);
            }
        });
    }

    private static void ComposeRetencionesSimpleTable(IContainer container, List<ImpuestoPagoSimplePdf> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });

            static IContainer TH(IContainer c) =>
                c.Background("#D7D7D7").Border(0.5f).Padding(2).AlignMiddle().AlignCenter();
            static IContainer TD(IContainer c) =>
                c.Border(0.5f).Padding(2).AlignMiddle().AlignCenter();

            table.Cell().Element(TH).Text("Impuesto").Bold().FontSize(6f);
            table.Cell().Element(TH).Text("Importe").Bold().FontSize(6f);

            foreach (var item in items)
            {
                table.Cell().Element(TD).Text(MapImpuesto(item.Impuesto)).FontSize(5.5f);
                table.Cell().Element(TD).Text(item.Importe.HasValue ? FormatSix(item.Importe.Value) : "—").FontSize(5.5f);
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(4).PaddingLeft(4).PaddingRight(4).Row(row =>
        {
            row.ConstantItem(96).Text(string.Empty);
            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));
                text.Span("Este documento es una representación impresa de un CFDI").Bold();
            });
            row.ConstantItem(96).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(6));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
    }

    private static byte[] GenerarQr(CfdiPdfData cfdi)
    {
        try
        {
            var last8 = string.IsNullOrEmpty(cfdi.SelloSAT)
                ? string.Empty
                : cfdi.SelloSAT[^Math.Min(8, cfdi.SelloSAT.Length)..];

            var url = $"https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx" +
                      $"?id={cfdi.UUID}" +
                      $"&re={cfdi.RFCEmisor}" +
                      $"&rr={cfdi.RFCReceptor}" +
                      $"&tt={(cfdi.Total ?? 0m).ToString("F6", CultureInfo.InvariantCulture)}" +
                      $"&fe={last8}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData      = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            using var qrCode      = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(3);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IContainer Box(IContainer container) =>
        container.Border(0.7f).Padding(6);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#CFCFCF").Border(0.6f).Padding(3).AlignMiddle();

    private static IContainer DataCell(IContainer container) =>
        container.Border(0.6f).Padding(3).AlignMiddle();

    private static IContainer InnerHeaderCell(IContainer container) =>
        container.Background("#D7D7D7").Border(0.5f).PaddingVertical(1).PaddingHorizontal(2).AlignMiddle();

    private static IContainer InnerDataCell(IContainer container) =>
        container.Border(0.5f).PaddingVertical(1).PaddingHorizontal(2).AlignMiddle();

    private static void TotalDetailRow(IContainer container, string label, string detail, string rate, decimal amount, bool bold = false)
    {
        container.Row(row =>
        {
            var labelText  = row.ConstantItem(96).Text(label);
            var detailText = row.ConstantItem(24).AlignCenter().Text(detail);
            var rateText   = row.ConstantItem(34).AlignCenter().Text(rate);
            var amountText = row.RelativeItem().AlignRight().Text(FormatMoneyTotal(amount));

            if (bold)
            {
                labelText.Bold().FontSize(8);
                detailText.Bold().FontSize(8);
                rateText.Bold().FontSize(8);
                amountText.Bold().FontSize(8);
            }
            else
            {
                labelText.Bold().FontSize(8);
                detailText.FontSize(8);
                rateText.FontSize(8);
                amountText.FontSize(8);
            }
        });
    }

    private static void HeaderField(IContainer container, string label, string value, float? labelWidth = null, float paddingBottom = 4, float valueFontSize = 8f)
    {
        container.PaddingBottom(paddingBottom).Row(row =>
        {
            row.ConstantItem(labelWidth ?? HeaderLabelWidth).Text(label).Bold().FontSize(8f);
            row.RelativeItem().Text(value).FontSize(valueFontSize);
        });
    }

    private static string FormatDateTime(DateTime? value) =>
        value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm:ss") : "-";

    private static string FormatDateTimeCompact(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string FormatMoney(decimal value) =>
        value.ToString("N2", MxCulture);

    private static string FormatMoneyTotal(decimal value) =>
        value == 0m ? "$ 0" : "$ " + value.ToString("N2", MxCulture);

    private static string FormatSix(decimal value) =>
        value.ToString("N6", MxCulture);

    private static (string line1, string line2) SplitIdentifier(string? value)
    {
        var clean = ValueOrDash(value);
        return (clean, string.Empty);
    }

    private static string GetPedimentoValue(string secondIdentifierLine) =>
        string.IsNullOrWhiteSpace(secondIdentifierLine) ? " " : secondIdentifierLine;

    private static List<(string Impuesto, string Tasa, decimal Importe)> GetTrasladosAgrupados(CfdiPdfData cfdi)
    {
        return cfdi.Conceptos
            .SelectMany(x => x.Traslados)
            .Where(x => x.Importe.HasValue)
            .GroupBy(x => new
            {
                Impuesto = MapImpuesto(x.Impuesto),
                Tasa     = FormatRate(x.TasaOCuota)
            })
            .Select(g => (g.Key.Impuesto, g.Key.Tasa, g.Sum(x => x.Importe ?? 0m)))
            .ToList();
    }

    private static string FormatRate(string? tasa)
    {
        if (string.IsNullOrWhiteSpace(tasa)) return string.Empty;
        if (!decimal.TryParse(tasa, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) return tasa;
        return $"{value * 100m:N0}%";
    }

    private static string PreventSlashBreaks(string? value)
    {
        var cleanValue = ValueOrDash(value);
        return cleanValue.Replace("/", "/⁠");
    }

    private static decimal CalcularBaseTraslado(ConceptoPdf concepto, TrasladoPdf traslado)
    {
        if (!string.IsNullOrWhiteSpace(traslado.TasaOCuota)
            && decimal.TryParse(traslado.TasaOCuota, NumberStyles.Any, CultureInfo.InvariantCulture, out var tasa)
            && tasa != 0
            && traslado.Importe.HasValue)
        {
            return traslado.Importe.Value / tasa;
        }
        return concepto.Importe - (concepto.Descuento ?? 0m);
    }

    private static string MapImpuesto(string? impuesto) => impuesto switch
    {
        "001" => "ISR",
        "002" => "IVA",
        "003" => "IEPS",
        _     => ValueOrDash(impuesto)
    };

    private static string MapObjetoImp(string? objetoImp) => objetoImp switch
    {
        "01" => "No objeto de impuesto.",
        "02" => "Sí objeto de impuesto.",
        "03" => "Sí objeto del impuesto y no obligado al desglose.",
        "04" => "Sí objeto del impuesto y no causa impuesto.",
        _    => ValueOrDash(objetoImp)
    };

    private static string MapExportacion(string? exportacion) => exportacion switch
    {
        null => "No aplica",
        ""   => "No aplica",
        "01" => "No aplica",
        "02" => "Definitiva",
        "03" => "Temporal",
        _    => ValueOrDash(exportacion)
    };

    private static string DescripcionTipoComprobante(string? tipo) => tipo switch
    {
        "I" => "Ingreso",
        "E" => "Egreso",
        "T" => "Traslado",
        "P" => "Pago",
        "N" => "Nómina",
        _   => tipo ?? string.Empty
    };

    private static string DescripcionFormaPago(string? formaPago) => formaPago switch
    {
        "01" => "Efectivo",
        "02" => "Cheque nominativo",
        "03" => "Transferencia electrónica de fondos",
        "04" => "Tarjeta de crédito",
        "05" => "Monedero electrónico",
        "06" => "Dinero electrónico",
        "08" => "Vales de despensa",
        "99" => "Por definir",
        _    => ValueOrDash(formaPago)
    };

    private static string DescripcionMoneda(string? moneda) => moneda switch
    {
        "MXN" => "Peso mexicano",
        "USD" => "Dólar estadounidense",
        "EUR" => "Euro",
        "CAD" => "Dólar canadiense",
        "GBP" => "Libra esterlina",
        "JPY" => "Yen japonés",
        "CNY" => "Yuan chino",
        "ARS" => "Peso argentino",
        "BRL" => "Real brasileño",
        "COP" => "Peso colombiano",
        "PEN" => "Sol peruano",
        "CLP" => "Peso chileno",
        "CHF" => "Franco suizo",
        "AUD" => "Dólar australiano",
        "MXV" => "Unidad de Inversión (UDI) mexicana",
        "XXX" => "Los códigos asignados para las transacciones en que intervenga ninguna moneda",
        _     => ValueOrDash(moneda)
    };

    private static string DescripcionMetodoPago(string? metodoPago) => metodoPago switch
    {
        "PUE" => "Pago en una sola exhibición",
        "PPD" => "Pago en parcialidades o diferido",
        _     => ValueOrDash(metodoPago)
    };

    private static string DescripcionUsoCfdi(string? usoCfdi) => usoCfdi switch
    {
        "G01"  => "Adquisición de mercancías",
        "G02"  => "Devoluciones, descuentos o bonificaciones",
        "G03"  => "Gastos en general",
        "I01"  => "Construcciones",
        "I02"  => "Mobiliario y equipo de oficina por inversiones",
        "I03"  => "Equipo de transporte",
        "I04"  => "Equipo de cómputo y accesorios",
        "I05"  => "Dados, troqueles, moldes, matrices y herramental",
        "I06"  => "Comunicaciones telefónicas",
        "I07"  => "Comunicaciones satelitales",
        "I08"  => "Otra maquinaria y equipo",
        "D01"  => "Honorarios médicos, dentales y gastos hospitalarios",
        "D02"  => "Gastos médicos por incapacidad o discapacidad",
        "D03"  => "Gastos funerales",
        "D04"  => "Donativos",
        "D05"  => "Intereses reales efectivamente pagados por créditos hipotecarios (casa habitación)",
        "D06"  => "Aportaciones voluntarias al SAR",
        "D07"  => "Primas por seguros de gastos médicos",
        "D08"  => "Gastos de transportación escolar obligatoria",
        "D09"  => "Depósitos en cuentas para el ahorro, primas que tengan como base planes de pensiones",
        "D10"  => "Pagos por servicios educativos (colegiaturas)",
        "P01"  => "Por definir",
        "S01"  => "Sin efectos fiscales",
        "CP01" => "Pagos",
        _      => ValueOrDash(usoCfdi)
    };

    private static string DescripcionRegimenFiscal(string? regimenFiscal) => regimenFiscal switch
    {
        "601" => "General de Ley Personas Morales",
        "603" => "Personas Morales con Fines no Lucrativos",
        "605" => "Sueldos y Salarios e Ingresos Asimilados a Salarios",
        "606" => "Arrendamiento",
        "607" => "Régimen de Enajenación o Adquisición de Bienes",
        "608" => "Demás Ingresos",
        "610" => "Residentes en el Extranjero sin Establecimiento Permanente en México",
        "611" => "Ingresos por Dividendos (socios y accionistas)",
        "612" => "Personas Físicas con Actividades Empresariales y Profesionales",
        "614" => "Ingresos por Intereses",
        "616" => "Sin Obligaciones Fiscales",
        "620" => "Sociedades Cooperativas de Producción",
        "621" => "Incorporación Fiscal",
        "622" => "Actividades Agrícolas, Ganaderas, Silvícolas y Pesqueras",
        "623" => "Opcional para Grupos de Sociedades",
        "624" => "Coordinados",
        "625" => "Régimen de las Actividades Empresariales con Plataformas Tecnológicas",
        "626" => "Régimen Simplificado de Confianza (RESICO)",
        "627" => "Sociedad Cooperativa de Responsabilidad Limitada de Capital Variable",
        _     => ValueOrDash(regimenFiscal)
    };

    private static string GetRfcProveedor(CfdiPdfData cfdi)
    {
        if (!string.IsNullOrWhiteSpace(cfdi.RfcProveedorCertificacion))
            return cfdi.RfcProveedorCertificacion;

        var cadenaOriginal = cfdi.CadenaOriginal;
        if (string.IsNullOrEmpty(cadenaOriginal)) return "-";

        var partes = cadenaOriginal.Trim('|').Split('|');
        return partes.Length >= 4 ? partes[4] : "-";
    }
}
