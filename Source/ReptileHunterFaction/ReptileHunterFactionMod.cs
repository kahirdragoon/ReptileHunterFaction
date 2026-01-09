using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
internal class ReptileHunterFactionMod : Mod
{
    public ReptileHunterFactionMod(ModContentPack content) : base(content)
    {
        var harmony = new Harmony("kahirdragoon.ReptileHunterFaction");
        harmony.PatchAll();
    }
}
