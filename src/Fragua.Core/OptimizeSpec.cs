namespace Fragua.Core;

/// <summary>
/// Ajuste de "Optimizar" del plan (calidad y metadatos). No es una
/// transformacion en memoria como Redimensionar: calidad y tira de
/// metadatos solo tienen efecto en el momento en que se escribe el archivo
/// final, asi que viaja como parametro del escritor en vez de ser un paso
/// de <see cref="IImageOperation"/> propio que reescribiria un archivo
/// intermedio sin sentido real.
/// </summary>
public sealed record OptimizeSpec(int Quality = 82, bool StripMetadata = true);
