# Avisos de terceros

Fragua es software libre construido sobre las siguientes librerías y fuentes, todas gratuitas y sin restricciones de uso comercial.

## Librerías

| Componente | Licencia | Proyecto |
|---|---|---|
| Avalonia UI | MIT | https://avaloniaui.net |
| Magick.NET | Apache 2.0 | https://github.com/dlemstra/Magick.NET |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| Microsoft.ML.OnnxRuntime | MIT | https://onnxruntime.ai |
| xUnit | Apache 2.0 | https://xunit.net |

## Codigo vendorizado

`src/Fragua.Imaging/ImageTrace/` porta el algoritmo de vectorizado de
[MiYanni/ImageTracer.NET](https://github.com/MiYanni/ImageTracer.NET) (The
Unlicense, dominio publico). Se reescribieron `ColorReference.cs` y
`Palettes/PaletteGenerator.cs` para sacar la dependencia de
`System.Drawing`/WPF del original y que funcione igual en Windows, Linux y
macOS; el resto del algoritmo (cuantizacion, capas, pathscan,
interpolacion, segmentacion, generacion de SVG) es el original sin tocar.

## Modelos de IA

| Modelo | Licencia | Origen |
|---|---|---|
| silueta (U2Net comprimido) | Apache 2.0 | https://github.com/danielgatis/rembg |
| Real-ESRGAN-General-x4v3 | BSD-3-Clause | https://github.com/xinntao/Real-ESRGAN |

Ninguno se distribuye en este repositorio ni en el instalador: se
descargan una sola vez la primera vez que se usa Quitar fondo o Mejorar
calidad, directo desde su fuente oficial, y quedan cacheados en el
perfil del usuario. El export ONNX de Real-ESRGAN se toma de la
publicacion oficial de Qualcomm AI Hub (mismo modelo, misma licencia
BSD-3-Clause del proyecto original de Xintao Wang); se eligio la
variante liviana (General x4v3) sobre la completa (x4plus, 23 bloques)
porque esta ultima mide ~35 segundos por mosaico de 128px en CPU,
impracticable para una foto real.

## Fuentes

| Fuente | Licencia | Origen |
|---|---|---|
| Mona Sans | SIL Open Font License 1.1 | https://github.com/github/mona-sans |
| Commit Mono | SIL Open Font License 1.1 | https://commitmono.com |

Los archivos de licencia originales de cada fuente viven junto a los `.otf` en `src/Fragua.App/Assets/Fonts/`.
