using Fragua.App.Models;
using Microsoft.Data.Sqlite;

namespace Fragua.App.Data;

/// <summary>
/// Historial y presets, persistidos de verdad (no se pierden al cerrar la
/// app). SQLite embebido en un archivo del perfil del usuario, mismo
/// criterio que uso ForgeMD para su historial.
/// </summary>
public sealed class FraguaDatabase
{
    private readonly string _connectionString;

    public FraguaDatabase() : this(DefaultDbPath())
    {
    }

    /// <summary>Constructor con ruta explicita: lo que usan los tests para no tocar la base real del usuario.</summary>
    public FraguaDatabase(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        // Pooling=False: sin esto, SqliteConnection mantiene el archivo abierto en un
        // pool incluso despues de Dispose(), lo que rompe el borrado del directorio en
        // los tests de Lotes (y dejaria el archivo bloqueado para cualquier otro proceso).
        _connectionString = $"Data Source={dbPath};Pooling=False";

        EnsureSchema();
    }

    private static string DefaultDbPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fragua", "fragua.db");

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS History (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                SizeBeforeBytes INTEGER NOT NULL,
                SizeAfterBytes INTEGER NOT NULL,
                FormatAfter TEXT NOT NULL,
                WhenUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Preset (
                Name TEXT PRIMARY KEY,
                ResizeEnabled INTEGER NOT NULL,
                ResizeMode TEXT NOT NULL,
                WidthText TEXT NOT NULL,
                HeightText TEXT NOT NULL,
                TargetFormat TEXT NOT NULL,
                OptimizeEnabled INTEGER NOT NULL,
                OptimizeQuality INTEGER NOT NULL,
                RemoveBackgroundEnabled INTEGER NOT NULL,
                VectorizeEnabled INTEGER NOT NULL,
                VectorizeColors INTEGER NOT NULL,
                UpscaleEnabled INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    // --- Historial ---

    public void InsertHistoryEntry(HistoryEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO History (FileName, SizeBeforeBytes, SizeAfterBytes, FormatAfter, WhenUtc)
            VALUES ($fileName, $sizeBefore, $sizeAfter, $format, $when);
            """;
        command.Parameters.AddWithValue("$fileName", entry.FileName);
        command.Parameters.AddWithValue("$sizeBefore", entry.SizeBeforeBytes);
        command.Parameters.AddWithValue("$sizeAfter", entry.SizeAfterBytes);
        command.Parameters.AddWithValue("$format", entry.FormatAfter);
        command.Parameters.AddWithValue("$when", entry.When.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>Las mas recientes primero, con un tope: el historial no es un log infinito.</summary>
    public List<HistoryEntry> LoadHistory(int limit = 500)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT FileName, SizeBeforeBytes, SizeAfterBytes, FormatAfter, WhenUtc
            FROM History
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<HistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new HistoryEntry(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)).ToLocalTime()));
        }
        return results;
    }

    public void ClearHistory()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM History;";
        command.ExecuteNonQuery();
    }

    // --- Presets ---

    public void SavePreset(ConversionPreset preset)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Preset (
                Name, ResizeEnabled, ResizeMode, WidthText, HeightText, TargetFormat,
                OptimizeEnabled, OptimizeQuality, RemoveBackgroundEnabled,
                VectorizeEnabled, VectorizeColors, UpscaleEnabled)
            VALUES (
                $name, $resizeEnabled, $resizeMode, $width, $height, $format,
                $optimizeEnabled, $optimizeQuality, $removeBgEnabled,
                $vectorizeEnabled, $vectorizeColors, $upscaleEnabled)
            ON CONFLICT(Name) DO UPDATE SET
                ResizeEnabled = excluded.ResizeEnabled,
                ResizeMode = excluded.ResizeMode,
                WidthText = excluded.WidthText,
                HeightText = excluded.HeightText,
                TargetFormat = excluded.TargetFormat,
                OptimizeEnabled = excluded.OptimizeEnabled,
                OptimizeQuality = excluded.OptimizeQuality,
                RemoveBackgroundEnabled = excluded.RemoveBackgroundEnabled,
                VectorizeEnabled = excluded.VectorizeEnabled,
                VectorizeColors = excluded.VectorizeColors,
                UpscaleEnabled = excluded.UpscaleEnabled;
            """;
        command.Parameters.AddWithValue("$name", preset.Name);
        command.Parameters.AddWithValue("$resizeEnabled", preset.ResizeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$resizeMode", preset.ResizeMode);
        command.Parameters.AddWithValue("$width", preset.WidthText);
        command.Parameters.AddWithValue("$height", preset.HeightText);
        command.Parameters.AddWithValue("$format", preset.TargetFormat);
        command.Parameters.AddWithValue("$optimizeEnabled", preset.OptimizeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$optimizeQuality", preset.OptimizeQuality);
        command.Parameters.AddWithValue("$removeBgEnabled", preset.RemoveBackgroundEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$vectorizeEnabled", preset.VectorizeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$vectorizeColors", preset.VectorizeColors);
        command.Parameters.AddWithValue("$upscaleEnabled", preset.UpscaleEnabled ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public List<ConversionPreset> LoadPresets()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Name, ResizeEnabled, ResizeMode, WidthText, HeightText, TargetFormat,
                   OptimizeEnabled, OptimizeQuality, RemoveBackgroundEnabled,
                   VectorizeEnabled, VectorizeColors, UpscaleEnabled
            FROM Preset
            ORDER BY Name COLLATE NOCASE;
            """;

        var results = new List<ConversionPreset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ConversionPreset(
                reader.GetString(0),
                reader.GetInt32(1) != 0,
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6) != 0,
                reader.GetInt32(7),
                reader.GetInt32(8) != 0,
                reader.GetInt32(9) != 0,
                reader.GetInt32(10),
                reader.GetInt32(11) != 0));
        }
        return results;
    }

    public void DeletePreset(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Preset WHERE Name = $name;";
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }
}
