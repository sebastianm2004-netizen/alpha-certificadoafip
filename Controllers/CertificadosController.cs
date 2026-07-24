using Alpha.Contable.CertificadoAfip.Models;
using Alpha.Contable.CertificadoAfip.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alpha.Contable.CertificadoAfip.Controllers;

public class CertificadosController : Controller
{
private readonly GeneradorCsrAfip _generador;

public CertificadosController(GeneradorCsrAfip generador)
{
_generador = generador;
}

public IActionResult Index()
{
return View(new GenerarCertificadoModel());
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Generar(GenerarCertificadoModel modelo)
{
var (cuitValido, errorCuit) = _generador.ValidarCuit(modelo.Cuit ?? "");
if (!cuitValido)
{
ModelState.AddModelError(nameof(modelo.Cuit), errorCuit!);
}

if (!ModelState.IsValid)
{
return View("Index", modelo);
}

var zip = _generador.GenerarPaquete(modelo.Cuit!, modelo.RazonSocial!, modelo.Alias);
var cuitLimpio = _generador.NormalizarCuit(modelo.Cuit!);

return File(zip, "application/zip", $"afip_{cuitLimpio}_csr.zip");
}

public IActionResult Convertir()
{
return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequestSizeLimit(2 * 1024 * 1024)]
public async Task<IActionResult> ConvertirAPfx(IFormFile? archivoCrt, IFormFile? archivoKey)
{
if (archivoCrt is null || archivoCrt.Length == 0)
{
ModelState.AddModelError("archivoCrt", "Subi el archivo .crt que te entrego AFIP.");
}

if (archivoKey is null || archivoKey.Length == 0)
{
ModelState.AddModelError("archivoKey", "Subi el archivo .key (clave privada) generado junto al CSR.");
}

if (!ModelState.IsValid)
{
return View("Convertir");
}

byte[] bytesCrt;
byte[] bytesKey;
using (var memoriaCrt = new MemoryStream())
{
await archivoCrt!.CopyToAsync(memoriaCrt);
bytesCrt = memoriaCrt.ToArray();
}
using (var memoriaKey = new MemoryStream())
{
await archivoKey!.CopyToAsync(memoriaKey);
bytesKey = memoriaKey.ToArray();
}

byte[] pfx;
try
{
pfx = _generador.ConvertirAPfx(bytesCrt, bytesKey, "");
}
catch (Exception ex)
{
ModelState.AddModelError(string.Empty,
"No se pudo combinar el .crt y la .key. Verifica que sean el certificado y la clave " +
"correspondientes al mismo CSR (el certificado que emitio AFIP para ESA clave privada). " +
$"Detalle: {ex.Message}");
return View("Convertir");
}

var nombreArchivo = Path.GetFileNameWithoutExtension(archivoCrt!.FileName);
return File(pfx, "application/x-pkcs12", $"{nombreArchivo}.pfx");
}

public IActionResult ConvertirP12()
{
return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequestSizeLimit(2 * 1024 * 1024)]
public async Task<IActionResult> ConvertirP12APfx(IFormFile? archivoP12)
{
if (archivoP12 is null || archivoP12.Length == 0)
{
ModelState.AddModelError("archivoP12", "Subi el archivo .p12.");
}

if (!ModelState.IsValid)
{
return View("ConvertirP12");
}

byte[] bytesP12;
using (var memoria = new MemoryStream())
{
await archivoP12!.CopyToAsync(memoria);
bytesP12 = memoria.ToArray();
}

byte[] pfx;
try
{
pfx = _generador.ConvertirP12APfx(bytesP12);
}
catch (Exception ex)
{
ModelState.AddModelError(string.Empty,
"No se pudo leer el .p12. Verifica que sea un archivo PKCS#12 valido y que realmente no " +
$"tenga contrasena. Detalle: {ex.Message}");
return View("ConvertirP12");
}

var nombreArchivo = Path.GetFileNameWithoutExtension(archivoP12!.FileName);
return File(pfx, "application/x-pkcs12", $"{nombreArchivo}.pfx");
}
}
