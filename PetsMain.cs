using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace DVPets
{
    public static class PetsMain
    {
        public static ModEntry Instance;

        public static PetSettings Settings { get; private set; }

        public static bool Load(ModEntry modEntry)
        {
            Instance = modEntry;
            Settings = ModSettings.Load<PetSettings>(modEntry);

            modEntry.OnGUI = DrawGUI;
            modEntry.OnSaveGUI = SaveGUI;

            PetSpawnManager.SetupListeners();

            var harmony = new Harmony("cc.foxden.dvpets");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        static void DrawGUI(ModEntry entry)
        {
            Settings.Draw(entry);
        }

        static void SaveGUI(ModEntry entry)
        {
            Settings.Save(entry);
        }

        public static void Log(string message)
        {
            Instance.Logger.Log(message);
        }

        public static void Error(string message)
        {
            Instance.Logger.Error(message);
        }

        public static void Warning(string message)
        {
            Instance.Logger.Warning(message);
        }
    }
}
