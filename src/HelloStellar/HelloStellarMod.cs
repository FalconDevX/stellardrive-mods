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
            _debugKey = category.CreateEntry("DebugKey", Key.F8, description: "Klawisz wypisujący informacje do konsoli");

            // Patche z atrybutem [HarmonyPatch] w tym assembly MelonLoader nakłada automatycznie.
            LoggerInstance.Msg("Załadowano. Naciśnij " + _debugKey.Value + " w grze.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scena: {sceneName} ({buildIndex})");
        }

        public override void OnUpdate()
        {
            // Gra używa nowego Input Systemu, więc UnityEngine.Input nie zadziała.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_debugKey.Value].wasPressedThisFrame)
            {
                var cam = UnityEngine.Camera.main;
                LoggerInstance.Msg(cam != null ? $"Kamera: {cam.transform.position}" : "Brak kamery");
            }
        }
    }
}
