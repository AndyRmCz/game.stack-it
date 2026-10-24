using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using StackIt.Client;
using StackIt.Client.Music;

// From unity/:  dotnet run -c Release --project DotNetTests/MusicPreview [style id] [--stats]
// Writes out/<style>-<scene>.wav for every style (or the one named), and prints loudness and
// voice-pool stats. Scenes: Home, the start of a run, 30 blocks, a thin block high up, and a
// whole run that climbs from 0 to 60 blocks with the block narrowing near the end.

string only = args.FirstOrDefault(a => !a.StartsWith("--"));
bool statsOnly = args.Contains("--stats");
string outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out");
Directory.CreateDirectory(outDir);

var scenes = new (string Name, int Bars, Func<int, MusicState> At)[]
{
    ("1-home", 8, _ => new MusicState(MusicMood.Home, 0, 1f)),
    ("2-start", 8, _ => new MusicState(MusicMood.Run, 0, 1f)),
    ("3-30-blocks", 8, _ => new MusicState(MusicMood.Run, 30, 0.6f)),
    ("4-thin-block", 8, _ => new MusicState(MusicMood.Run, 45, 0.2f)),
    ("5-whole-run", 32, bar => new MusicState(MusicMood.Run, bar * 2, bar < 24 ? 1f - bar * 0.02f : 0.25f)),
};

Console.WriteLine($"{"style",-17} {"scene",-13} {"rms dB",7} {"phone",6} {"peak",6} {"notes",6} {"cuts",5}");
foreach (string id in MusicStyles.Ids.Where(i => only == null || i == only))
{
    var clock = Stopwatch.StartNew();
    var renderer = new MusicRenderer(MusicStyles.Create(id));
    Console.WriteLine($"{id}: instruments rendered in {clock.ElapsedMilliseconds} ms");
    foreach (var (name, bars, at) in scenes)
    {
        float[] mix = renderer.Render(bars, at, out RenderStats s);
        Console.WriteLine($"{id,-17} {name,-13} {20 * Math.Log10(s.Rms),7:0.0} {20 * Math.Log10(s.PhoneRms),6:0.0} {s.Peak,6:0.00} {s.Notes,6} {s.LoudSteals,5}");
        if (!statsOnly) WriteWav(Path.Combine(outDir, $"{id}-{name}.wav"), mix);
    }
}
if (!statsOnly) Console.WriteLine("WAV files in " + Path.GetFullPath(outDir));

static void WriteWav(string path, float[] stereo)
{
    using var w = new BinaryWriter(File.Create(path));
    int bytes = stereo.Length * 2;
    w.Write(Encoding.ASCII.GetBytes("RIFF"));
    w.Write(36 + bytes);
    w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
    w.Write(16);
    w.Write((short)1); // PCM
    w.Write((short)2); // stereo
    w.Write(Dsp.SampleRate);
    w.Write(Dsp.SampleRate * 4);
    w.Write((short)4);
    w.Write((short)16);
    w.Write(Encoding.ASCII.GetBytes("data"));
    w.Write(bytes);
    foreach (float v in stereo) w.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767f));
}
