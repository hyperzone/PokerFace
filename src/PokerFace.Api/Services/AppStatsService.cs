using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PokerFace.Api.Models;

namespace PokerFace.Api.Services;

public class AppStatsService
{
    private readonly string _filePath;
    private AppStats _stats = new();
    private readonly object _lock = new object();

    public AppStatsService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "App_Data", "stats.json");
        LoadStats();
    }

    private void LoadStats()
    {
        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _stats = JsonSerializer.Deserialize<AppStats>(json) ?? new AppStats();
                }
                catch
                {
                    _stats = new AppStats();
                }
            }
            else
            {
                _stats = new AppStats();
                SaveStats();
            }
        }
    }

    private void SaveStats()
    {
        try
        {
            var json = JsonSerializer.Serialize(_stats, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppStatsService] Errore durante il salvataggio delle statistiche: {ex.Message}");
        }
    }

    public void IncrementTotalTables()
    {
        lock (_lock)
        {
            _stats.TotalTablesCreated++;
            SaveStats();
        }
    }

    public void IncrementTotalPlayers()
    {
        lock (_lock)
        {
            _stats.TotalPlayersConnected++;
            SaveStats();
        }
    }

    public void UpdateMaxSimultaneousUsers(int currentUsersCount)
    {
        lock (_lock)
        {
            if (currentUsersCount > _stats.MaxSimultaneousUsersAtTable)
            {
                _stats.MaxSimultaneousUsersAtTable = currentUsersCount;
                SaveStats();
            }
        }
    }

    public AppStats GetStats()
    {
        lock (_lock)
        {
            return new AppStats
            {
                TotalTablesCreated = _stats.TotalTablesCreated,
                TotalPlayersConnected = _stats.TotalPlayersConnected,
                MaxSimultaneousUsersAtTable = _stats.MaxSimultaneousUsersAtTable
            };
        }
    }
}
