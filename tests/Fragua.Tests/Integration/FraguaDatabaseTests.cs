using Fragua.App.Data;
using Fragua.App.Models;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba que el historial y los presets sobreviven de verdad: dos
/// instancias separadas de FraguaDatabase apuntando al mismo archivo, como
/// pasaria entre dos arranques reales de la app, no una sola instancia
/// reusada que no probaria nada sobre persistencia.
/// </summary>
public sealed class FraguaDatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fragua-db-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public void El_historial_sobrevive_a_una_instancia_nueva_de_la_base()
    {
        var first = new FraguaDatabase(_dbPath);
        first.InsertHistoryEntry(new HistoryEntry("foto.png", 800_000, 120_000, "WebP", DateTimeOffset.Now));
        first.InsertHistoryEntry(new HistoryEntry("logo.png", 50_000, 12_000, "Png", DateTimeOffset.Now));

        var second = new FraguaDatabase(_dbPath);
        var history = second.LoadHistory();

        Assert.Equal(2, history.Count);
        // Mas reciente primero.
        Assert.Equal("logo.png", history[0].FileName);
        Assert.Equal("foto.png", history[1].FileName);
    }

    [Fact]
    public void ClearHistory_borra_de_verdad_en_disco_no_solo_en_memoria()
    {
        var db = new FraguaDatabase(_dbPath);
        db.InsertHistoryEntry(new HistoryEntry("a.png", 100, 50, "Png", DateTimeOffset.Now));

        db.ClearHistory();

        var reopened = new FraguaDatabase(_dbPath);
        Assert.Empty(reopened.LoadHistory());
    }

    [Fact]
    public void Un_preset_guardado_se_lee_identico_desde_otra_instancia()
    {
        var preset = new ConversionPreset(
            Name: "Para marketplace",
            ResizeEnabled: true,
            ResizeMode: "Exact",
            WidthText: "1000",
            HeightText: "1000",
            TargetFormat: "Jpeg",
            OptimizeEnabled: true,
            OptimizeQuality: 75,
            RemoveBackgroundEnabled: true,
            VectorizeEnabled: false,
            VectorizeColors: 16,
            UpscaleEnabled: false);

        var first = new FraguaDatabase(_dbPath);
        first.SavePreset(preset);

        var second = new FraguaDatabase(_dbPath);
        var loaded = Assert.Single(second.LoadPresets());

        Assert.Equal(preset, loaded);
    }

    [Fact]
    public void Guardar_un_preset_con_el_mismo_nombre_lo_actualiza_no_lo_duplica()
    {
        var db = new FraguaDatabase(_dbPath);
        db.SavePreset(new ConversionPreset("Mi preset", true, "Fit", "500", "500", "Png", false, 82, false, false, 16, false));
        db.SavePreset(new ConversionPreset("Mi preset", false, "Exact", "200", "200", "WebP", true, 60, true, false, 16, true));

        var presets = db.LoadPresets();
        var only = Assert.Single(presets);

        Assert.Equal("WebP", only.TargetFormat);
        Assert.True(only.UpscaleEnabled);
    }

    [Fact]
    public void DeletePreset_lo_saca_de_verdad()
    {
        var db = new FraguaDatabase(_dbPath);
        db.SavePreset(new ConversionPreset("Temporal", false, "Fit", "", "", "Png", false, 82, false, false, 16, false));

        db.DeletePreset("Temporal");

        Assert.Empty(db.LoadPresets());
    }
}
