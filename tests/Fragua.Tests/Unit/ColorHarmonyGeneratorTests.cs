using Fragua.Core;

namespace Fragua.Tests.Unit;

/// <summary>
/// Matematica de color pura, sin Magick.NET ni archivos: se verifica con
/// colores primarios donde el resultado esperado es exacto y conocido, no
/// una aproximacion.
/// </summary>
public sealed class ColorHarmonyGeneratorTests
{
    [Fact]
    public void Complementary_de_rojo_puro_da_cyan_puro()
    {
        var result = ColorHarmonyGenerator.Generate("#FF0000", ColorHarmony.Complementary);

        Assert.Equal(["#FF0000", "#00FFFF"], result);
    }

    [Fact]
    public void Triadic_de_rojo_puro_da_los_tres_primarios()
    {
        var result = ColorHarmonyGenerator.Generate("#FF0000", ColorHarmony.Triadic);

        Assert.Equal(["#FF0000", "#00FF00", "#0000FF"], result);
    }

    [Fact]
    public void Analogous_devuelve_tres_colores_con_el_base_en_el_medio()
    {
        var result = ColorHarmonyGenerator.Generate("#FF0000", ColorHarmony.Analogous);

        Assert.Equal(3, result.Count);
        Assert.Equal("#FF0000", result[1]);
    }

    [Fact]
    public void Monochromatic_devuelve_cinco_tonos_de_oscuro_a_claro()
    {
        var result = ColorHarmonyGenerator.Generate("#3366CC", ColorHarmony.Monochromatic);

        Assert.Equal(5, result.Count);

        // Cada paso deberia ser mas claro que el anterior: comparamos la
        // suma de canales RGB como aproximacion de luminosidad.
        int Brightness(string hex) => Convert.ToInt32(hex[1..3], 16) + Convert.ToInt32(hex[3..5], 16) + Convert.ToInt32(hex[5..7], 16);
        for (var i = 1; i < result.Count; i++)
        {
            Assert.True(Brightness(result[i]) > Brightness(result[i - 1]),
                $"{result[i]} deberia ser mas claro que {result[i - 1]}");
        }
    }

    [Fact]
    public void Un_gris_puro_se_mantiene_gris_en_cualquier_armonia()
    {
        var result = ColorHarmonyGenerator.Generate("#808080", ColorHarmony.Complementary);

        Assert.All(result, hex => Assert.Equal("#808080", hex));
    }
}
