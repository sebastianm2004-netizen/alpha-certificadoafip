using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Alpha.Contable.CertificadoAfip.Services;

public class GeneradorCsrAfip
{
  public (bool EsValido, string? Error) ValidarCuit(string cuit)
  {
    var digitos = new string((cuit ?? "").Where(char.IsDigit).ToArray());
    if (digitos.Length != 11)
    {
      return (false, "El C.U.I.T. debe tener 11 digitos.");
    }

    int[] multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
    var suma = 0;
    for (var i = 0; i < 10; i++)
    {
      suma += (digitos[i] - '0') * multiplicadores[i];
    }

    var resto = 11 - (suma % 11);
    var verificador = resto == 11 ? 0 : resto;

    if (verificador == 10 || verificador != digitos[10] - '0')
    {
      return (false, "El C.U.I.T. ingresado no es valido (digito verificador incorrecto).");
    }

    return (true, null);
  }

  public string NormalizarCuit(string cuit)
  {
    return new string((cuit ?? "").Where(char.IsDigit).ToArray());
  }

  public byte[] ConvertirP12APfx(byte[] bytesP12)
  {
    var coleccion = new X509Certificate2Collection();
    coleccion.Import(bytesP12, "", X509KeyStorageFlags.Exportable);
    return coleccion.Export(X509ContentType.Pfx, "") ?? Array.Empty<byte>();
  }

  public byte[] GenerarPaquete(string cuit, string razonSocial, string? alias)
  {
    var cuitLimpio = NormalizarCuit(cuit);
    var aliasCertificado = string.IsNullOrWhiteSpace(alias) ? razonSocial : alias!.Trim();

    var subjectDn = ArmarSubjectDn(cuitLimpio, razonSocial.Trim(), aliasCertificado);

    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
      new X500DistinguishedName(subjectDn),
      rsa,
      HashAlgorithmName.SHA256,
      RSASignaturePadding.Pkcs1);

    var csrDer = request.CreateSigningRequest();
    var csrPem = ConvertirAPem("CERTIFICATE REQUEST", csrDer);
    var clavePem = rsa.ExportPkcs8PrivateKeyPem();

    var nombreBase = $"afip_{cuitLimpio}";

    using var memoria = new MemoryStream();
    using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
    {
      EscribirEntrada(zip, $"{nombreBase}.csr", csrPem);
      EscribirEntrada(zip, $"{nombreBase}.key", clavePem);
      EscribirEntrada(zip, "instructivo.txt", ArmarInstructivo(cuitLimpio, razonSocial, aliasCertificado, nombreBase));
    }

    return memoria.ToArray();
  }

  public byte[] ConvertirAPfx(byte[] bytesCertificado, byte[] bytesClave, string contrasena)
  {
    using var rsa = RSA.Create();
    rsa.ImportFromPem(DecodificarTexto(bytesClave));

    using var certificadoSolo = CargarCertificado(bytesCertificado);
    using var certificadoConClave = certificadoSolo.CopyWithPrivateKey(rsa);

    return certificadoConClave.Export(X509ContentType.Pfx, contrasena);
  }

  private static X509Certificate2 CargarCertificado(byte[] bytesCertificado)
  {
    var texto = DecodificarTexto(bytesCertificado);
    if (texto.Contains("BEGIN CERTIFICATE"))
    {
      return X509Certificate2.CreateFromPem(texto);
    }

    return new X509Certificate2(bytesCertificado);
  }

  private static string DecodificarTexto(byte[] bytes)
  {
    try
    {
      return Encoding.UTF8.GetString(bytes);
    }
    catch
    {
      return "";
    }
  }

  private static string ArmarSubjectDn(string cuit, string razonSocial, string alias)
  {
    return string.Join(", ", new[]
                       {
                         $"C=AR",
                         $"O={EscaparValorDn(razonSocial)}",
                         $"CN={EscaparValorDn(alias)}",
                         $"SERIALNUMBER=CUIT {cuit}"
                         });
  }

  private static string EscaparValorDn(string valor)
  {
    var necesitaComillas = valor.IndexOfAny(new[] { ',', '+', '"', '\\', '<', '>', ';', '=' }) >= 0
      || valor.StartsWith(' ') || valor.EndsWith(' ');

    if (!necesitaComillas)
    {
      return valor;
    }

    return "\"" + valor.Replace("\"", "\\\"") + "\"";
  }

  private static string ConvertirAPem(string etiqueta, byte[] datosDer)
  {
    var base64 = Convert.ToBase64String(datosDer);
    var sb = new StringBuilder();
    sb.Append("-----BEGIN ").Append(etiqueta).Append("-----\n");
    for (var i = 0; i < base64.Length; i += 64)
    {
      sb.Append(base64, i, Math.Min(64, base64.Length - i)).Append('\n');
    }
    sb.Append("-----END ").Append(etiqueta).Append("-----\n");
    return sb.ToString();
  }

  private static void EscribirEntrada(ZipArchive zip, string nombre, string contenido)
  {
    var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
    using var writer = new StreamWriter(entrada.Open(), Encoding.UTF8);
    writer.Write(contenido);
  }

  private static string ArmarInstructivo(string cuit, string razonSocial, string alias, string nombreBase)
  {
    return $"""
      Certificado Digital para Facturacion Electronica (AFIP) - {razonSocial}
    C.U.I.T.: {cuit}
    Alias del certificado: {alias}
    Generado: {DateTime.Now:dd/MM/yyyy HH:mm}

    Este paquete contiene:
    - {nombreBase}.csr  -> El pedido de certificado (Certificate Signing Request).
      Esto es lo que se sube a AFIP.
      - {nombreBase}.key  -> La clave privada. GUARDALA A BUEN RESGUARDO y no la compartas.
      Sin ella no vas a poder usar el certificado que emita AFIP.

      Pasos siguientes en el sitio de AFIP (esto AFIP lo hace de forma manual,
                                            no se puede automatizar via API publica):

    1. Entrar a https://www.afip.gob.ar/ws/ con Clave Fiscal (nivel de
    seguridad 3) del contribuyente (o de quien tenga la relacion de
                                    apoderado/administrador de relaciones).
      2. Ir a "Administracion de Certificados Digitales".
      3. Elegir "Nuevo Certificado" y pegar el contenido completo del
      archivo {nombreBase}.csr (incluyendo las lineas
                                -----BEGIN CERTIFICATE REQUEST----- y -----END CERTIFICATE REQUEST-----).
      4. AFIP va a emitir el certificado (.crt). Descargalo.
      5. Ir al "Administrador de Relaciones de Clave Fiscal" y asociar ese
      certificado al servicio "Web Service de Facturacion Electronica"
      (WSFE / WSFEv1), autorizando la relacion entre el CUIT y el
      certificado para ese servicio.
      6. Con el .crt que entrego AFIP + la {nombreBase}.key generada aca,
    tu sistema ya puede autenticarse contra el WSAA y facturar por
      WSFE.
      """;
    }
}
