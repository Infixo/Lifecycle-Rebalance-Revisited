﻿using System.Reflection;
using UnityEngine;
using HarmonyLib;
using CitiesHarmony.API;
using System;

namespace LifecycleRebalance
{
    /// <summary>
    /// Class to manage the mod's Harmony patches.
    /// </summary>
    public static class Patcher
    {
        // Unique harmony identifier.
        private const string harmonyID = "com.github.algernon-A.csl.lifecyclerebalancerevisited";

        // Flag.
        public static bool patched = false;


        // Target methods for patches that are dynamically applied (instead of through Harmony annotations).
        // These are methods who can be either patched or unpatched depending on option settings.
        public static MethodInfo OriginalGetAgeGroup => typeof(Citizen).GetMethod("GetAgeGroup");
        public static MethodInfo OriginalGetCarProbability => typeof(ResidentAI).GetMethod("GetCarProbability", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new System.Type[] { typeof(ushort), typeof(CitizenInstance).MakeByRefType(), typeof(Citizen.AgeGroup) },
            new ParameterModifier[0]);
        public static MethodInfo OriginalGetBikeProbability => typeof(ResidentAI).GetMethod("GetBikeProbability", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new System.Type[] { typeof(ushort), typeof(CitizenInstance).MakeByRefType(), typeof(Citizen.AgeGroup) },
            new ParameterModifier[0]);
        public static MethodInfo OriginalGetTaxiProbability => typeof(ResidentAI).GetMethod("GetTaxiProbability", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new System.Type[] { typeof(ushort), typeof(CitizenInstance).MakeByRefType(), typeof(Citizen.AgeGroup) },
            new ParameterModifier[0]);

        // Patch methods for patches that are dynamically applied.

        public static MethodInfo GetAgeGroupPrefix => typeof(GetAgeGroupPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        public static MethodInfo GetCarProbabilityPrefix => typeof(GetCarProbabilityPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        public static MethodInfo GetBikeProbabilityPrefix => typeof(GetBikeProbabilityPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        public static MethodInfo GetTaxiProbabilityPrefix => typeof(GetTaxiProbabilityPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);


        /// <summary>
        /// Apply all Harmony patches.
        /// </summary>
        public static void PatchAll()
        {
            // Don't do anything if already patched.
            if (!patched)
            {
                Debug.Log("Lifecycle Rebalance Revisited v" + LifecycleRebalance.Version + ": deploying Harmony patches.");

                // Apply all annotated patches and update flag.
                Harmony harmonyInstance = new Harmony(harmonyID);
                harmonyInstance.PatchAll();
                patched = true;
            }
        }


        /// <summary>
        /// Remove all Harmony patches applied by this mod.
        /// </summary>
        public static void UnpatchAll()
        {
            // Only unapply if patches appplied.
            if (patched)
            {
                Debug.Log("Lifecycle Rebalance Revisited: reverting Harmony patches.");

                // Unapply patches, but only with our HarmonyID.
                Harmony harmonyInstance = new Harmony(harmonyID);
                harmonyInstance.UnpatchAll(harmonyID);
                patched = false;
            }
        }


        /// <summary>
        /// Manually applies Harmony patches.
        /// </summary>
        public static void ApplyPrefixPatch(MethodInfo originalMethod, MethodInfo patchMethod)
        {
            Harmony harmonyInstance = new Harmony(harmonyID);

            // Check if the patch is already installed before proceeding.
            if (!IsPatchInstalled(originalMethod))
            {
                if (originalMethod == null)
                {
                    string message = "Lifecycle Rebalance Revisited: null original method passed for patching.";
                    Debug.Log(message);
                    throw new UnassignedReferenceException(message);
                }

                if (patchMethod == null)
                {
                    string message = "Lifecycle Rebalance Revisited: null patch method passed for patching.";
                    Debug.Log(message);
                    throw new UnassignedReferenceException(message);
                }

                Debug.Log("Lifecycle Rebalance Revisited: patching '" + originalMethod.Name + "'.");
                harmonyInstance.Patch(originalMethod, prefix: new HarmonyMethod(patchMethod));
            }
            else
            {
                Debug.Log("Lifecycle Rebalance Revisited: '" + originalMethod.Name + "' patch already applied, doing nothing.");
            }
        }


        /// <summary>
        /// Manually removes specified Harmony patches.
        /// </summary>
        public static void RevertPatch(MethodInfo originalMethod, MethodInfo patchMethod)
        {
            Harmony harmonyInstance = new Harmony(harmonyID);

            // Check if the patch is installed before proceeding.
            if (IsPatchInstalled(originalMethod))
            {
                Debug.Log("Lifecycle Rebalance Revisited: removing patch from '" + originalMethod.Name + "'.");
                harmonyInstance.Unpatch(originalMethod, patchMethod);
            }
            else
            {
                Debug.Log("Lifecycle Rebalance Revisited: '" + originalMethod.Name + "' patch not applied, doing nothing.");
            }
        }


        /// <summary>
        /// Checks to see if Harmony Citizen.GetAgeGroup() pre-emptive Prefix patch is currently applied.
        /// </summary>
        public static bool IsPatchInstalled(MethodInfo originalMethod)
        {
            var patches = Harmony.GetPatchInfo(originalMethod);
            if (patches != null)
            {
                foreach (var patch in patches.Prefixes)
                {
                    if (patch.owner == harmonyID)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}