# Duplicados de fotos (PhotoDuplicates)

Aplicación de **escritorio para Windows** que ayuda a **localizar fotos duplicadas** dentro de una carpeta (y subcarpetas) y a **gestionar qué copias enviar a la papelera de reciclaje**, conservando al menos un archivo por grupo de duplicados.

**Repositorio:** [github.com/pepocero/photoduplicates](https://github.com/pepocero/photoduplicates)

## ¿Para qué sirve?

- Elegir una **carpeta** con imágenes (por ejemplo `Pictures` o un disco de respaldo).
- **Escanear** buscando duplicados con dos modos:
  - **Misma imagen (huella visual):** detecta la misma foto aunque tenga distinto nombre o metadatos (perceptual hash).
  - **Mismo archivo (hash exacto):** solo coincidencias byte a byte.
- Ver los **grupos de duplicados** con miniaturas y rutas.
- **Marcar** qué archivos de cada grupo quieres eliminar (por defecto se propone conservar uno y marcar el resto).
- **Enviar los marcados a la papelera** (no borrado permanente), con barra de progreso y confirmación al terminar.
- **Vista previa** al pulsar una miniatura para revisar la foto a tamaño grande.

Útil para **liberar espacio**, ordenar colecciones de fotos y evitar borrar sin querer al usar la papelera en lugar del borrado directo.

## Requisitos

- **Windows 10/11** (64 bits).
- La publicación por defecto (`FolderWin64`) es **autocontenida**: la carpeta publicada incluye el runtime .NET y el Windows App SDK; en el PC destino **no** hace falta instalar runtimes por separado (copiar **toda** la carpeta de salida).
- Si en el futuro se vuelve al modo **dependiente del framework** (más pequeño, requiere runtimes en el sistema): [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0) (x64) y [Windows App Runtime 1.6](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) (x64).

## Tecnología

- **WinUI 3** (.NET 9), Windows App SDK.
- Detección de duplicados con **ImageSharp** (modo visual) y hash SHA-256 (modo exacto).

## Compilar y publicar

Abre `PhotoDuplicates/PhotoDuplicates.csproj` en Visual Studio (carga de trabajo **Desarrollo para el escritorio con .NET** y herramientas WinUI).

Publicación típica (carpeta autocontenida lista para copiar):

```bash
dotnet publish PhotoDuplicates/PhotoDuplicates.csproj -c Release -p:PublishProfile=FolderWin64
```

La salida queda bajo `PhotoDuplicates/bin/Publish/win-x64/`. Copia **toda la carpeta**, no solo el `.exe`. Punto de restauración antes de autocontenida (modo FDD): etiqueta git `pre-self-contained-fdd`.

La carpeta `bin/` no se sube a Git (`.gitignore`); el ZIP compilado no debe versionarse en el repo. Para **copia de seguridad en GitHub**: **Actions** → **Publicar backup (win-x64)** → **Run workflow**. Tras el run con ✓, al **final de la página de esa ejecución**, sección **Artifacts** → descarga **PhotoDuplicates-win-x64-self-contained** (no hay URL de “app alojada”; es un ZIP para descargar y ejecutar en local). Con un **tag** `v1.0.0`, el ZIP también suele estar en **Releases**.

## Créditos

Aplicación creada por **CarliniTools** — [info@carlinitools.com](mailto:info@carlinitools.com)
