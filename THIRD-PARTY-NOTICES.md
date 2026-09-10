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

No se distribuye en este repositorio ni en el instalador: se descarga una
sola vez la primera vez que se usa Quitar fondo, directo desde el release
oficial de rembg, y queda cacheado en el perfil del usuario.

## Fuentes

| Fuente | Licencia | Origen |
|---|---|---|
| Mona Sans | SIL Open Font License 1.1 | https://github.com/github/mona-sans |
| Commit Mono | SIL Open Font License 1.1 | https://commitmono.com |

Los archivos de licencia originales de cada fuente viven junto a los `.otf` en `src/Fragua.App/Assets/Fonts/`.
