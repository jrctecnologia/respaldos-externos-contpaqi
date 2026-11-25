# Respaldos BAK CONTPAQi

Herramienta portable para Windows que copia o mueve los respaldos `.bak` de un año, junto con sus `.zip` de `Index\respaldos`, a otra carpeta o a una unidad externa, conservando la estructura de carpetas.

**[Descargar la última versión](https://github.com/jrctecnologia/respaldos-externos-contpaqi/releases/latest)**: es un solo archivo, `RespaldosBak.exe`, y no se instala.

## Deslinde de responsabilidad

Esta herramienta copia, mueve y borra archivos. **Úsala bajo tu propia responsabilidad.**

Se ofrece "tal cual", sin garantía de ningún tipo. JRC Tecnología y los autores no se hacen responsables por pérdida de información ni por otros daños derivados de su uso. Consulta también la [licencia MIT](LICENSE).

Antes de mover tus respaldos:

- **Simula y revisa el resultado con cuidado.** Simular no copia, mueve ni borra nada.
- **Ten otro respaldo** de la información importante.

Esta herramienta no está afiliada a CONTPAQi.

## Requisitos

- Windows con .NET Framework 4.8. Ya viene en Windows 10 (1903 o posterior), Windows 11 y Server 2022. En Server 2016 y 2019 hay que instalarlo si no está.
- Permiso para leer la carpeta `Empresas` y para escribir en el destino. Para mover, también permiso para borrar en el origen.

## Cómo se usa

1. Copia `RespaldosBak.exe` a una carpeta o a una USB y ábrelo. Como no está firmado, Windows puede mostrar una advertencia.
2. Llena los datos:

   | Campo | Qué poner |
   |---|---|
   | **Origen (Empresas)** | La carpeta `Empresas` de CONTPAQi. Si existe `C:\Compac\Empresas`, aparece sola. |
   | **Carpeta Index** | Se llena sola al elegir el origen. Si no existe, solo se procesan los `.bak`. |
   | **Destino** | Dónde quedarán los respaldos. No puede estar dentro de Origen ni de Index. |
   | **Año** | Año de la fecha de modificación de los `.bak`. |
   | **Operación** | **Copiar** o **Mover** (ver abajo). |
   | **Comprimir .bak a ZIP** | Opcional. Crea un `.bak.zip` por cada `.bak`. |

3. Deja marcado **Simular** y presiona **Iniciar**. Revisa la lista, los avisos y el resumen.
4. Si el resultado es correcto, desmarca **Simular** y presiona **Iniciar** otra vez. **Mover** pide confirmación.
5. Al terminar, revisa el resumen. **Abrir log** muestra el detalle y **Abrir destino** abre la carpeta.

**Cancelar** detiene el proceso, incluso a la mitad de un archivo. Lo que ya se terminó de copiar o mover se queda así.

### ¿Copiar o Mover?

Elige una, según lo que necesites. No son pasos seguidos.

- **Copiar** deja los originales en su lugar.
- **Mover** quita los originales. Cada uno se borra después de verificar el tamaño de su copia.

> **Si vas a mover, usa un destino donde no hayas copiado antes los mismos archivos.** Lo que ya existe en el destino se omite y su original no se borra. Además, un `.zip` de Index puede quedarse en el origen sin su `.bak`, y la herramienta ya no lo tomará.

### Qué archivos toma

- Los `.bak` que están en las subcarpetas de `Empresas`. Los de la raíz nunca se tocan.
- El `.zip` de `Index\respaldos` que se llama igual que cada `.bak`. Los `.zip` no se vuelven a comprimir.

Nunca sobrescribe: lo que ya existe en el destino se omite. El destino queda así:

```
Destino\
├── Empresas\
│   └── adEMPRESA\Respaldo.bak      (o Respaldo.bak.zip)
└── Index\
    └── respaldos\Respaldo.zip
```

### Qué significa cada línea del log

El log se guarda junto al `.exe`, o en la carpeta temporal de Windows (`%TEMP%`) si ahí no se puede escribir.

| Tipo | Significado |
|---|---|
| `COPIADO`, `MOVIDO`, `COMPRIMIDO` | Se procesó bien. `COMPRIMIDO` es una copia comprimida. |
| `COPIARÍA`, `MOVERÍA` | Simulación: es lo que haría. |
| `OMITIDO` | Ya existía en el destino. |
| `AVISO` | Algo por revisar, por ejemplo un `.bak` sin `.zip` en Index. |
| `ERROR` | No se pudo procesar, o se copió pero no se pudo borrar el original. Si dice que el acceso fue denegado, prueba abrir el `.exe` como administrador. |

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
