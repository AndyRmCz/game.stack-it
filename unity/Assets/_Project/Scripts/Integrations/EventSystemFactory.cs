using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace StackIt.Integrations
{
    /// <summary>uGUI event system driven by the Input System package (the project's only input backend).</summary>
    public static class EventSystemFactory
    {
        public static void Create()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
