using UnityEngine;

namespace StackIt.UI
{
    /// <summary>Marks a graphic coloured by a themed component on an ancestor (e.g. a button's label).</summary>
    public sealed class ThemedPart : MonoBehaviour
    {
        public Themed Owner;

        public static void Mark(Component target, Themed owner) =>
            target.gameObject.AddComponent<ThemedPart>().Owner = owner;
    }
}
