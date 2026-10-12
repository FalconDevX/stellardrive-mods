using HelloStellar;
using MelonLoader;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(HelloStellarMod), "Hello Stellar", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace HelloStellar
{
    public class HelloStellarMod : MelonMod
    {
        private MelonPreferences_Entry<Key> _debugKey;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("HelloStellar");
            _debugKey = category.CreateEntry("DebugKey", Key.F8, description: "Key that prints information to the console");

            LoggerInstance.Msg("Loaded. Press " + _debugKey.Value + " in game.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene: {sceneName} ({buildIndex})");
        }

        public override void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_debugKey.Value].wasPressedThisFrame)
            {
                var cam = UnityEngine.Camera.main;
                LoggerInstance.Msg(cam != null ? $"Camera: {cam.transform.position}" : "No camera");
            }
        }
    }
}
