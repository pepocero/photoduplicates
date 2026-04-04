# Aplicaciones Windows autocontenidas (.NET + WinUI 3)

Documento de referencia para publicar apps de escritorio **sin exigir** .NET Desktop Runtime ni Windows App Runtime instalados en el PC destino. Copiar **toda** la carpeta de salida; el tamaño es mayor que en modo dependiente del framework (FDD).

---

## 1. Qué implica “autocontenido” en este contexto

| Capa | Propiedad | Efecto |
|------|-----------|--------|
| Runtime .NET | `SelfContained=true` | Incluye el runtime de .NET en la carpeta publicada. |
| Windows App SDK (WinUI, WinRT desacoplado, etc.) | `WindowsAppSDKSelfContained=true` | Incluye los binarios del WASDK junto al exe (necesario para WinUI 3 desempaquetado). |

Sin `WindowsAppSDKSelfContained`, un WinUI publicado con `SelfContained=true` **sigue** necesitando el [Windows App Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/) instalado en el sistema para cargar `Microsoft.UI.Xaml` y dependencias.

**TFM típico:** `net9.0-windows10.0.xxxxx.0` (o la versión que use el proyecto). El `RuntimeIdentifier` de publicación suele ser `win-x64`.

---

## 2. Propiedades recomendadas en el `.csproj` (WinUI 3 desempaquetado)

Evitan fallos conocidos al publicar y al arrancar (p. ej. `0xc000027b` en `Microsoft.UI.Xaml.dll`, errores de PRI).

- `WindowsPackageType` = `None` — app desempaquetada (carpeta + exe), no MSIX obligatorio.
- `EnableMsixTooling` = `true` — la cadena de MSBuild genera recursos **.pri** correctamente; **no** fuerza empaquetado MSIX si `WindowsPackageType` es `None`.
- `PublishTrimmed` = `false` — el recorte (trimming) rompe reflexión y metadatos que usa XAML/WinUI.
- `PublishSingleFile` = `false` — WinUI + single file suele fallar en desempaquetado; además complica PRI y despliegue.
- `PublishAot` = `false` para WinUI clásico (no aplica el mismo flujo que consolas nativas AOT).
- Opcional si aparece **0xc000027b** en builds **Release** publicados:  
  `Optimize` = `false` cuando `Configuration` es `Release` (workaround documentado en el ecosistema WinUI; revisar si en versiones futuras del SDK ya no hace falta).

`WindowsAppSDKSelfContained` puede vivir en el `.csproj` o solo en el perfil `.pubxml`; debe ser **coherente** con lo que quieras en cada modo de publicación (FDD vs autocontenido).

---

## 3. Perfil de publicación (`.pubxml`)

Ejemplo mínimo para carpeta autocontenida x64:

```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<PublishTrimmed>false</PublishTrimmed>
<PublishSingleFile>false</PublishSingleFile>
<PublishReadyToRun>false</PublishReadyToRun>
```

- `PublishReadyToRun` en `false` simplifica diagnóstico; activar solo si medís beneficio real.
- `DeleteExistingFiles` = `true` evita mezclar restos de una publicación FDD anterior.

**CLI:**

```bash
dotnet publish Proyecto.csproj -c Release -p:PublishProfile=NombrePerfil
```

---

## 4. Errores frecuentes y mitigaciones

| Síntoma | Causa probable | Acción |
|--------|----------------|--------|
| Error al publicar: *PublishSingleFile requires EnableMsixTooling* (o similar PRI) | Falta tooling MSIX para recursos | `EnableMsixTooling=true` |
| Crash `0xc000027b` en `Microsoft.UI.Xaml.dll` | Optimización Release / trimming / incompatibilidades | `PublishTrimmed=false`, `PublishSingleFile=false`, probar `Optimize=false` en Release |
| En el PC destino sigue pidiendo Windows App Runtime | Solo `SelfContained=true` sin WASDK embebido | `WindowsAppSDKSelfContained=true` |
| App no arranca y en Visor de eventos aparece ruta bajo `WindowsApps\Microsoft.WindowsAppRuntime...` | Confusión entre runtime del sistema y carpeta local | Verificar publicación y que se copió **toda** la carpeta |

---

## 5. SmartScreen, “Editor desconocido” y archivos “dañinos”

Al ejecutar un `.exe` **sin firma** (o firmado con un certificado no reconocido / no de confianza), Windows Defender SmartScreen muestra la pantalla azul con **Editor desconocido** y puede advertir que el archivo parece dañino. **No es un bug de la app autocontenida**: es la política de reputación y firma de Microsoft.

**Forma estándar de reducir o eliminar esa fricción:**

1. **Firma Authenticode** del ejecutable (y idealmente del instalador o de todos los binarios relevantes) con un **certificado de firma de código** emitido por una **CA pública de confianza** (p. ej. DigiCert, Sectigo, GlobalSign). Usar **sellado de tiempo** (timestamp) al firmar para que la firma siga validando tras expirar el certificado.
2. **EV Code Signing** (Extended Validation) suele generar reputación con SmartScreen **más rápido** que los estándar; tiene coste y proceso de identidad más estricto.
3. **Distribución por Microsoft Store** (MSIX): la firma de Store evita ese flujo de “descarga arbitraria” para muchos usuarios.
4. **Entornos corporativos:** directivas de grupo / Intune para confiar en el certificado interno o en la ruta de despliegue.

**Qué no soluciona el problema por sí solo:** cambiar `SelfContained`, el nombre del exe, o “marcar como desbloqueado” en propiedades del ZIP en **otro** PC — puede ayudar en casos de `Zone.Identifier`, pero la advertencia de **Editor desconocido** en descargas web casi siempre exige **firma de código** o **reputación** acumulada por un ejecutable firmado.

Herramientas habituales: `signtool` (Windows SDK), integración en CI. Mantener el certificado y la cadena fuera del repositorio (secretos).

---

## 6. FDD vs autocontenido (decisión rápida)

| Modo | Ventaja | Inconveniente |
|------|---------|----------------|
| FDD (`SelfContained=false`, `WindowsAppSDKSelfContained=false`) | Carpeta pequeña | Instalar .NET Desktop Runtime + Windows App Runtime en cada PC |
| Autocontenido | Sin instalación de runtimes | Carpeta grande; sigue aplicando SmartScreen si no hay firma |

Conviene mantener en Git una **etiqueta o rama** con el último estado FDD estable antes de cambiar a autocontenido, para poder revertir si aparece un fallo específico del SDK.

---

## 7. Referencias

- [Implementación desempaquetada con Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps)
- [Publicar .NET con aplicación autocontenida](https://learn.microsoft.com/dotnet/core/deploying/)
- [Descargas Windows App Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
