using System;

namespace DespertadorApp.Models;

public class Evento
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public DateTime Fecha { get; set; }
    public int DiasAntes { get; set; } = 3;
    public DateTime? UltimoAviso { get; set; }

    public override string ToString()
    {
        return $"{Fecha:dd/MM/yyyy}  |  {Titulo}  |  avisa {DiasAntes} días antes";
    }
}