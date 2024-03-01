using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace DVPets
{
    public class PetSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Cat Type")]
        public PetType PetType = PetType.GrayTabbyCat;

        [Draw("Max Degree Tilt")]
        public float MaxRotateSpeed = 10;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            PetSpawnManager.RefreshSpawnedPet();
        }
    }

    public enum PetType
    {
        BlackCat,
        WhiteSpottedCat,
        GrayTabbyCat,
        OrangeTabbyCat,
    }
}
