using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StackIt.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StackIt.Tests
{
    public class ThemedComponentTests
    {
        [Test]
        public void EveryThemedComponentToleratesBeingAppliedBeforeItIsWiredUp()
        {
            // In Play mode AddComponent runs OnEnable (and so Apply) immediately, before the builder
            // has set the component's target: Apply must cope with an unconfigured component.
            Type[] kinds = typeof(Themed).Assembly.GetTypes()
                .Where(t => typeof(Themed).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();
            Assert.IsNotEmpty(kinds);
            foreach (Type kind in kinds)
            {
                var go = new GameObject(kind.Name, typeof(RectTransform));
                try
                {
                    if (kind == typeof(TextEffect)) go.AddComponent<TMPro.TextMeshProUGUI>();
                    var themed = (Themed)go.AddComponent(kind);
                    Assert.DoesNotThrow(() => themed.Apply(Look.P), kind.Name);
                    // Wired up, but with every token left "": in Play mode Unity's serializer turns
                    // unset (null) public strings into empty ones.
                    foreach (FieldInfo field in kind.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (field.FieldType == typeof(string)) field.SetValue(themed, "");
                        else if (typeof(Component).IsAssignableFrom(field.FieldType)) field.SetValue(themed, Part(go, field.FieldType));
                    }
                    Assert.DoesNotThrow(() => themed.Apply(Look.P), kind.Name + " with empty tokens");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>A graphic of the field's type on its own child (one graphic per object).</summary>
        private static Component Part(GameObject parent, Type type)
        {
            if (type == typeof(UnityEngine.UI.Graphic)) type = typeof(TMPro.TextMeshProUGUI);
            if (type == typeof(TMPro.TMP_InputField)) return null;
            var child = new GameObject(type.Name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent(type);
        }
    }
}