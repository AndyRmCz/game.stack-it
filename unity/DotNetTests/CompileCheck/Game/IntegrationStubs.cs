// Compile-check only: stand-ins for Scripts/Integrations/UrpPostFx.cs and UnityLocalizer.cs, whose
// packages (URP, Localization) are not compiled by this project. Keep signatures in sync with those files.
namespace StackIt.Integrations
{
    public sealed class UrpPostFx : UnityEngine.MonoBehaviour, StackIt.Services.IPostFx
    {
        public void ChromaticPulse(float strength) { }
        public void SetBloom(float intensity) { }
        public void Configure(UnityEngine.Camera camera) { }
    }

    public sealed class UnityLocalizer : StackIt.Services.ILocalizer
    {
        public string Language => "en";
        public event System.Action Changed;
        public string Get(string key) => key;
        public void SetLanguage(string code) { Changed?.Invoke(); }
        public System.Threading.Tasks.Task InitializeAsync(string preferred) => System.Threading.Tasks.Task.CompletedTask;
    }
}
