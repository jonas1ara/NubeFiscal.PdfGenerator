namespace NubeFiscal.PdfGenerator.Models;

public class CfdiPdfData
{
    public long Id { get; set; }
    public int IdRazonSocial { get; set; }
    public string RFCRazonSocial { get; set; } = string.Empty;
    public string UUID { get; set; } = string.Empty;
    public string? RFCEmisor { get; set; }
    public string? RFCReceptor { get; set; }
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime? FechaTimbrado { get; set; }
    public decimal? Total { get; set; }
    public string? Moneda { get; set; }
    public string? TipoComprobante { get; set; }
    public string EstatusSAT { get; set; } = string.Empty;
    public DateTime? FechaCancelacion { get; set; }
    public string AnioMes { get; set; } = string.Empty;
    public string TipoDescarga { get; set; } = string.Empty;
    public string? XmlCFDI { get; set; }
    public string? PackageId { get; set; }
    public string? NombrePaqueteZIP { get; set; }
    public DateTime FechaDescarga { get; set; }
    public string? RutaPdf { get; set; }

    // Campos parseados del XML
    public string? NombreEmisor { get; set; }
    public string? RegimenFiscalEmisor { get; set; }
    public string? NombreReceptor { get; set; }
    public string? RegimenFiscalReceptor { get; set; }
    public string? UsoCFDI { get; set; }
    public string? VersionCfdi { get; set; }
    public string? Exportacion { get; set; }
    public string? CodigoPostalEmisor { get; set; }
    public string? CodigoPostalReceptor { get; set; }
    public string? LugarExpedicion { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? Descuento { get; set; }
    public decimal? TotalImpuestosTrasladados { get; set; }
    public decimal? TotalImpuestosRetenidos   { get; set; }
    public string? SelloSAT { get; set; }
    public string? SelloCFDI { get; set; }
    public string? CadenaOriginal { get; set; }
    public string? RfcProveedorCertificacion { get; set; }
    public string? NoCertificado { get; set; }
    public string? NoCertificadoSAT { get; set; }
    public string? FormaPago { get; set; }
    public string? MetodoPago { get; set; }
    public List<ConceptoPdf> Conceptos { get; set; } = new();

    /// <summary>Poblado únicamente cuando TipoComprobante == "P".</summary>
    public ComplementoPagoPdf? ComplementoPago { get; set; }
}

public class ConceptoPdf
{
    public string? ClaveProdServ { get; set; }
    public string? NoIdentificacion { get; set; }
    public decimal Cantidad { get; set; }
    public string? ClaveUnidad { get; set; }
    public string? Unidad { get; set; }
    public string? Descripcion { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal Importe { get; set; }
    public decimal? Descuento { get; set; }
    public string? ObjetoImp { get; set; }
    public List<TrasladoPdf>  Traslados   { get; set; } = new();
    public List<RetencionPdf> Retenciones { get; set; } = new();
}

public class RetencionPdf
{
    public string? Impuesto { get; set; }
    public decimal? Base    { get; set; }
    public decimal? Importe { get; set; }
}

public class TrasladoPdf
{
    public string? Impuesto { get; set; }
    public string? TasaOCuota { get; set; }
    public decimal? Importe { get; set; }
}

// ── Complemento de Pago (TipoDeComprobante = "P") ────────────────────────────

public class ComplementoPagoPdf
{
    public string Version { get; set; } = "2.0";
    public TotalesPagoPdf? Totales { get; set; }
    public List<PagoPdf> Pagos { get; set; } = new();
}

public class TotalesPagoPdf
{
    public decimal? TotalRetencionesIVA { get; set; }
    public decimal? TotalRetencionesISR { get; set; }
    public decimal? TotalRetencionesIEPS { get; set; }
    public decimal? TotalTrasladosBaseIVA16 { get; set; }
    public decimal? TotalTrasladosImpuestoIVA16 { get; set; }
    public decimal? TotalTrasladosBaseIVA8 { get; set; }
    public decimal? TotalTrasladosImpuestoIVA8 { get; set; }
    public decimal? TotalTrasladosBaseIVA0 { get; set; }
    public decimal? TotalTrasladosImpuestoIVA0 { get; set; }
    public decimal? TotalTrasladosBaseIVAExento { get; set; }
    public decimal MontoTotalPagos { get; set; }
}

public class PagoPdf
{
    public DateTime? FechaPago { get; set; }
    public string? FormaDePagoP { get; set; }
    public string? MonedaP { get; set; }
    public decimal? TipoCambioP { get; set; }
    public decimal Monto { get; set; }
    public string? NumOperacion { get; set; }
    public List<DoctoRelacionadoPdf> DoctoRelacionados { get; set; } = new();
    public List<ImpuestoPagoSimplePdf> RetencionesP { get; set; } = new();
    public List<ImpuestoPagoDetalladoPdf> TrasladosP { get; set; } = new();
}

public class DoctoRelacionadoPdf
{
    public string? IdDocumento { get; set; }
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public string? MonedaDR { get; set; }
    public decimal? EquivalenciaDR { get; set; }
    public int? NumParcialidad { get; set; }
    public decimal? ImpSaldoAnt { get; set; }
    public decimal? ImpPagado { get; set; }
    public decimal? ImpSaldoInsoluto { get; set; }
    public string? ObjetoImpDR { get; set; }
    public List<ImpuestoPagoDetalladoPdf> RetencionesDR { get; set; } = new();
    public List<ImpuestoPagoDetalladoPdf> TrasladosDR { get; set; } = new();
}

public class ImpuestoPagoDetalladoPdf
{
    public string? Impuesto { get; set; }
    public string? TipoFactor { get; set; }
    public decimal? Base { get; set; }
    public decimal? TasaOCuota { get; set; }
    public decimal? Importe { get; set; }
}

public class ImpuestoPagoSimplePdf
{
    public string? Impuesto { get; set; }
    public decimal? Importe { get; set; }
}
