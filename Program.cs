using Alpha.Contable.CertificadoAfip.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<GeneradorCsrAfip>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
      app.UseExceptionHandler("/Certificados/Index");
      app.UseHsts();
}
else
{
      app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
      name: "default",
      pattern: "{controller=Certificados}/{action=Index}/{id?}");

app.Run();
