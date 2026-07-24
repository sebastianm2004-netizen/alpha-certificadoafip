using System.ComponentModel.DataAnnotations;

namespace Alpha.Contable.CertificadoAfip.Models;

public class GenerarCertificadoModel
{
  [Required(ErrorMessage = "Ingresa el C.U.I.T.")]
  [Display(Name = "C.U.I.T.")]
  public string Cuit { get; set; } = "";

  [Required(ErrorMessage = "Ingresa la razon social / nombre de la empresa.")]
  [Display(Name = "Razon Social / Nombre de Empresa")]
  public string RazonSocial { get; set; } = "";

  [Display(Name = "Alias del certificado (opcional)")]
  public string? Alias { get; set; }
}
