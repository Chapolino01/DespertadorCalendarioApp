using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace DespertadorApp.Services;

public class VozService
{
    public Task DecirAsync(string texto)
    {
        return Task.Run(() =>
        {
            using var s = new SpeechSynthesizer();
            s.SetOutputToDefaultAudioDevice();
            s.Speak(texto);
        });
    }
}