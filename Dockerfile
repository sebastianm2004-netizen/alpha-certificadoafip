# Build multietapa para Alpha.Contable.CertificadoAfip (ASP.NET Core 8, sin
# base de datos ni dependencias externas: solo genera CSR/clave privada para
# el tramite de Certificado Digital de AFIP).

# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Alpha.Contable.CertificadoAfip.csproj .
RUN dotnet restore "Alpha.Contable.CertificadoAfip.csproj"

COPY . .
RUN dotnet publish "Alpha.Contable.CertificadoAfip.csproj" -c Release -o /app/publish --no-restore

# ---------- Etapa 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet Alpha.Contable.CertificadoAfip.dll --urls http://0.0.0.0:${PORT}"]
