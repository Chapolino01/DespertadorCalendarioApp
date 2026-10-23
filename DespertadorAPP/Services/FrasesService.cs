using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DespertadorApp.Services
{
    public class FrasesService
    {
        private static readonly string[] Respaldo =
        {
            "Hoy es un gran día para avanzar un paso más.",
            "Lo que hagas hoy construye lo que serás mañana.",
            "La constancia vale más que la motivación.",
            "Cada línea de código te acerca a tu objetivo."
        };

        private static readonly string[] Excluir =
            { "whatsapp", "pensador", "editores", "contribución", "suscrib", "cookies" };

        private readonly HttpClient _http = new();

        public FrasesService()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-AR,es;q=0.9");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<List<string>> DescargarAsync(string url)
        {
            try
            {
                var html = await _http.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var frases = Extraer(doc, "//p");
                if (frases.Count == 0)
                    frases = Extraer(doc, "//li|//div|//span|//blockquote");

                return frases;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> Extraer(HtmlDocument doc, string xpath)
        {
            var nodos = doc.DocumentNode.SelectNodes(xpath);
            if (nodos == null) return new List<string>();

            return nodos
                .Select(n => Regex.Replace(HtmlEntity.DeEntitize(n.InnerText), @"\s+", " ").Trim())
                .Select(t => Regex.Replace(t, @"^\d+\s*[\.\)\-]\s*", ""))
                .Where(t => t.Length >= 20 && t.Length <= 160)
                .Where(t => char.IsLetter(t[0]) || t[0] == '¿' || t[0] == '¡' || t[0] == '"' || t[0] == '“')
                .Where(t => ".!?…".Contains(t[^1]))
                .Where(t => !Excluir.Any(x => t.Contains(x, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .ToList();
        }

        public string Aleatoria(IEnumerable<string> frases)
        {
            var lista = frases.ToList();
            if (lista.Count == 0) lista = Respaldo.ToList();
            return lista[Random.Shared.Next(lista.Count)];
        }
    }
}