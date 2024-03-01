using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVPets
{
    public static class PetSpawnManager
    {
        public static GameObject CurrentPet = null;

        public static void SetupListeners()
        {
            PlayerManager.PlayerTeleportFinished += OnTeleportEnd;

            UnloadWatcher.UnloadRequested += OnUnload;
        }

        public static void RefreshSpawnedPet()
        {
            if (CurrentPet)
            {
                Vector3 petPos = CurrentPet.transform.position;
                Quaternion petRot = CurrentPet.transform.rotation;

                SpawnPet();

                CurrentPet.transform.position = petPos;
                CurrentPet.transform.rotation = petRot;
            }
        }

        private static void OnUnload()
        {
            DestroyPet();
            _cats = null;
        }

        public static void SpawnPet()
        {
            if (CurrentPet) DestroyPet();

            var petType = PetsMain.Settings.PetType;
            var prefab = GetPetPrefab(petType);
            if (!prefab)
            {
                PetsMain.Error($"Failed to load prefab for type {petType}");
                return;
            }

            CurrentPet = Object.Instantiate(prefab, WorldMover.OriginShiftParent);
            if (!CurrentPet)
            {
                PetsMain.Warning($"Failed to instantiate pet of type {petType}");
                return;
            }

            CurrentPet.transform.position = PlayerManager.PlayerTransform.TransformPoint(Vector3.forward);
            var controller = CurrentPet.AddComponent<PetController>();
            controller.ControlMode = PetController.Mode.GroundFollow;

            PetsMain.Log($"Spawned pet of type {petType}");
        }

        private static List<GameObject> _cats = null;

        private static GameObject GetPetPrefab(PetType type)
        {
            // can only fetch these at runtime
            _cats ??= Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(g => g.name.StartsWith("Cat") && g.name.EndsWith("rigged"))
                    .ToList();

            string prefabName = type switch
            {
                PetType.BlackCat => "CatSimpleBlack_rigged",
                PetType.WhiteSpottedCat => "CatSimpleWhiteSpotted_rigged",
                PetType.GrayTabbyCat => "CatSimpleGray_rigged",
                PetType.OrangeTabbyCat => "CatSimpleYellow_rigged",
                _ => throw new System.NotImplementedException(),
            };

            return _cats.FirstOrDefault(cat => cat.name == prefabName);
        }

        private static void DestroyPet()
        {
            if (CurrentPet)
            {
                Object.Destroy(CurrentPet);
                PetsMain.Log("Destroyed pet model");
            }
            else
            {
                PetsMain.Log("Unloading, no pet to destroy");
            }
        }

        private static void OnTeleportEnd()
        {
            Vector3 offset = PlayerManager.PlayerTransform.position - CurrentPet.transform.position;
            if (offset.sqrMagnitude > PetController.MAX_PLAYER_SQR_DISTANCE * 16)
            {
                SpawnPet();
            }
        }
    }

    [HarmonyPatch(typeof(StartingItemsController), nameof(StartingItemsController.AddStartingItems))]
    internal static class SessionStarted_Patch
    {
        public static void Postfix()
        {
            PetSpawnManager.SpawnPet();
        }
    }
}
