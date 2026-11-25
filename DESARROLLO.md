# Desarrollo

Se necesita el SDK de .NET 10. Compila también en macOS y Linux.

```
dotnet build RespaldosBak -c Release
dotnet test RespaldosBak.Tests
```

La prueba de archivos de más de 4 GB solo corre con la variable de entorno `RESPALDOS_PRUEBA_4GB=1`.
En Windows las pruebas corren en net48 (el entorno real) y en net10.0; en otros sistemas, solo en net10.0.

## Publicar una versión

```
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions ejecuta las pruebas, compila el `.exe` con ese número de versión y lo publica en *Releases* con su SHA-256.

[Volver al README](README.md)
