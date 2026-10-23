using System;
using System.Linq;

namespace DespertadorApp.Models;

public class Alarma
{
    public int Id { get; set; }
    public TimeSpan Hora { get; set; }
    public bool Activa { get; set; } = true;
    public string DiasSemana { get; set; } = "1,2,3,4,5"; // 0=domingo ... 6=sábado

    public override string ToString()
    {
        var nombres = new[] { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
        var dias = DiasSemana
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => nombres[int.Parse(d)]);
        return $"{Hora:hh\\:mm}  |  {string.Join(" ", dias)}  |  {(Activa ? "Activa" : "Inactiva")}";
    }
}