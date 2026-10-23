using DespertadorApp.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace DespertadorApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Alarma> Alarmas => Set<Alarma>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<PlaylistUrl> Playlists => Set<PlaylistUrl>();
    public DbSet<FuenteFrases> Fuentes => Set<FuenteFrases>();
    public DbSet<Frase> Frases => Set<Frase>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DespertadorApp");
        Directory.CreateDirectory(carpeta);
        options.UseSqlite($"Data Source={Path.Combine(carpeta, "despertador.db")}");
    }
}