using UnityEngine;

namespace StackIt.UI
{
    /// <summary>
    /// Marks a graphic whose colours are not the look's: invisible hit areas, avatars, skin
    /// previews, the theme-switch snapshot. The no-hardcoded-colours test accepts these.
    /// </summary>
    public sealed class IgnoreTheming : MonoBehaviour
    {
        public string Reason;

        public static void Mark(Component target, string reason) =>
            target.gameObject.AddComponent<IgnoreTheming>().Reason = reason;
    }
}
