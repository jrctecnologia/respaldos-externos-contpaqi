# Respaldos BAK CONTPAQi

Herramienta portable para Windows que copia o mueve los respaldos `.bak` de un año, junto con sus `.zip` de `Index\respaldos`, a otra carpeta o unidad externa, conservando la estructura de carpetas.

> **En desarrollo.** La ventana ya valida y simula; copiar y mover todavía no están habilitados.

- Un solo `RespaldosBak.exe`, sin instalación (requiere .NET Framework 4.8, incluido en Windows 10 1903+, Windows 11 y Server 2022+).
- Solo toma archivos de las subcarpetas de `Empresas`; nunca toca los de la raíz.
- Al mover, borra el original únicamente después de verificar la copia.
- Nunca sobrescribe: lo que ya existe en el destino se omite.
- Compresión ZIP opcional por archivo y modo de simulación.

Esta herramienta no está afiliada a CONTPAQi.

## Desarrollo

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
