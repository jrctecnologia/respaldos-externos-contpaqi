# Respaldos BAK CONTPAQi

Herramienta portable para Windows que copia o mueve los respaldos `.bak` de un año, junto con sus `.zip` de `Index\respaldos`, a otra carpeta o unidad externa, conservando la estructura de carpetas.

Descárgala en [Releases](https://github.com/jrctecnologia/respaldos-externos-contpaqi/releases). Antes de copiar o mover, usa *Simular* y revisa el resultado.

- Un solo `RespaldosBak.exe`, sin instalación (requiere .NET Framework 4.8, incluido en Windows 10 1903+, Windows 11 y Server 2022+).
- Solo toma archivos de las subcarpetas de `Empresas`; nunca toca los de la raíz.
- Al mover, borra el original únicamente después de verificar la copia.
- Nunca sobrescribe: lo que ya existe en el destino se omite.
- Compresión ZIP opcional por archivo y modo de simulación.

## Antes de usarla: deslinde de responsabilidad

Esta herramienta copia, mueve y borra archivos de respaldo. Se ofrece "tal cual", sin garantía de ningún tipo, y **quien la usa lo hace bajo su propia responsabilidad**. JRC Tecnología y los autores no se hacen responsables por pérdida de información, daños o interrupciones derivados de su uso. Consulta también la [licencia MIT](LICENSE).

Se recomienda:

1. Usar primero **Simular** y revisar con cuidado el resultado y el log, incluidos los avisos y los archivos omitidos.
2. Elegir después **una** de las dos operaciones, según lo que se necesite:
   - **Copiar**: deja los originales en su lugar.
   - **Mover**: solo si se quieren quitar los originales. Se borran después de verificar el tamaño de la copia.
3. No usar **Mover** hacia un destino donde ya se hizo **Copiar**: lo que ya existe se omite, así que esos originales no se borran, y un `.zip` de Index puede quedarse en el origen sin su `.bak`. Para mover, usar una carpeta nueva.
4. Conservar siempre otro respaldo de la información importante.

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
