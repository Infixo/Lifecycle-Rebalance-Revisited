// <copyright file="ResidentAIPatches.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard) and Whitefang Greytail. All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using System;
    using System.Runtime.CompilerServices;
    using AlgernonCommons;
    using ColossalFramework;
    using HarmonyLib;
    using UnityEngine;
    using static DistrictPolicies;
    using static InvestmentAI;
    using static RenderManager;

    /// <summary>
    /// Harmony patches for ResidentAI to implement mod functionality.
    /// </summary>
    [HarmonyPatch(typeof(ResidentAI))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony")]
    public static class ResidentAIPatches
    {
        /// <summary>
        /// Harmony pre-emptive Prefix patch for ResidentAI.CanMakeBabies - implements mod's minor fix so that only adult females (of less than age 180) give birth.
        /// </summary>
        /// <param name="__result">Original method result.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch(nameof(ResidentAI.CanMakeBabies))]
        [HarmonyPrefix]
        public static bool CanMakeBabies(ref bool __result, uint citizenID, ref Citizen data)
        {
            __result =
                !data.Dead &&
                (Citizen.GetGender(citizenID) == Citizen.Gender.Female) &&
                (Citizen.GetAgeGroup(data.Age) == Citizen.AgeGroup.Adult) &&
                ((data.m_flags & Citizen.Flags.MovingIn) == Citizen.Flags.None);

            //Logging.Message("CanMakeBabies",
                //",dead?,", data.Dead,
                //",female?,", (Citizen.GetGender(citizenID) == Citizen.Gender.Female),
                //",adult?,", (Citizen.GetAgeGroup(data.Age) == Citizen.AgeGroup.Adult),
                //",moving?,", ((data.m_flags & Citizen.Flags.MovingIn) == Citizen.Flags.MovingIn),
                //$",result={__result}");

            // Don't execute base method after this.
            return false;
        }

        /// <summary>
        /// Harmony pre-emptive Prefix patch for ResidentAI.UpdateAge - implements mod's ageing and deathcare rate functions.
        /// CRITICAL for mod functionality.
        /// </summary>
        /// <param name="__result">Original method result.</param>
        /// <param name="__instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch("UpdateAge")]
        [HarmonyPrefix]
        public static bool UpdateAge(ref bool __result, ResidentAI __instance, uint citizenID, ref Citizen data)
        {
            // Method result.
            bool removed = false;

            // Allow for lifespan multipler.
            if ((citizenID % DataStore.LifeSpanMultiplier) == Threading.Counter)
            {
                // Local reference.
                CitizenManager citizenManager = Singleton<CitizenManager>.instance;

                // Increment citizen age.
                int newAge = data.Age + 1;

                if (newAge <= ModSettings.YoungStartAge)
                {
                    // Children and teenagers finish school.
                    if (newAge == ModSettings.TeenStartAge || newAge == ModSettings.YoungStartAge)
                    {
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                }
                else if (newAge == ModSettings.AdultStartAge || newAge >= ModSettings.RetirementAge)
                {
                    // Young adults finish university/college, adults retire.
                    FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                }
                // Education management failsafe i.e. evicting those who overstayed for any reason
                else if ((data.m_flags & Citizen.Flags.Student) != Citizen.Flags.None)
                {
                    BuildingInfo info = Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_workBuilding].Info;
                    if (Citizen.GetAgeGroup(data.Age) == Citizen.AgeGroup.Adult)
                    {
                        // Adults are treated differently because they can go to School at any time
                        // The game doesn't keep info about WHEN they started the School
                        // Using just simple  "greater than a specific age" will cause immediate eviction
                        // This is just a simple solution to make sure they will stay at least several age units in School
                        int avgSchoolAge = (ModSettings.AdultStartAge - ModSettings.SchoolStartAge) / 3; // three schools
                        if (newAge % avgSchoolAge == 0)
                        {
                            Logging.Message("UpdateAge: evicting Adult student ", citizenID, " at age ", newAge);
                            FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                        }
                    }
                    else if (newAge > ModSettings.AdultStartAge && info.m_buildingAI.GetEducationLevel3())
                    {
                        Logging.Message("UpdateAge: evicting university student ", citizenID, " at age ", newAge);
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                    // Evict high school students who've overstayed.
                    else if (newAge > ModSettings.YoungStartAge && info.m_buildingAI.GetEducationLevel2())
                    {
                        Logging.Message("UpdateAge: evicting high school student ", citizenID, " at age ", newAge);
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                    // Evict elementary school students who've overstayed.
                    else if (newAge > ModSettings.TeenStartAge && info.m_buildingAI.GetEducationLevel1())
                    {
                        Logging.Message("UpdateAge: evicting elementary school student ", citizenID, " at age ", newAge);
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                }

                // Original citizen?
                if ((data.m_flags & Citizen.Flags.Original) != Citizen.Flags.None)
                {
                    // Yes - if necessary, update oldest original resident flags.
                    if (citizenManager.m_tempOldestOriginalResident < newAge)
                    {
                        citizenManager.m_tempOldestOriginalResident = newAge;
                    }

                    // Update full lifespan counter.
                    if (newAge == 240)
                    {
                        Singleton<StatisticsManager>.instance.Acquire<StatisticInt32>(StatisticType.FullLifespans).Add(1);
                    }
                }

                // Update age.
                data.Age = newAge;

                // Checking for death and sickness chances.
                // Citizens who are currently moving or currently in a vehicle aren't affected.
                if (data.CurrentLocation != Citizen.Location.Moving && data.m_vehicle == 0)
                {
                    // Local reference.
                    SimulationManager simulationManager = Singleton<SimulationManager>.instance;

                    bool died = false;

                    if (ModSettings.Settings.VanillaCalcs)
                    {
                        // Using vanilla lifecycle calculations.
                        int num2 = 240;
                        int num3 = 255;
                        int num4 = Mathf.Max(0, 145 - ((100 - data.m_health) * 3));
                        if (num4 != 0)
                        {
                            num2 += num4 / 3;
                            num3 += num4;
                        }

                        if (newAge >= num2)
                        {
                            bool flag = simulationManager.m_randomizer.Int32(2000u) < 3;
                            died = simulationManager.m_randomizer.Int32(num2 * 100, num3 * 100) / 100 <= newAge || flag;
                        }
                    }
                    else
                    {
                        // Using custom lifecycle calculations.
                        // Game defines years as being age divided by 3.5.  Hence, 35 age increments per decade.
                        // Legacy mod behaviour worked on 25 increments per decade.
                        // If older than the maximum index - lucky them, but keep going using that final index.
                        int index = Math.Min((int)(newAge * ModSettings.Settings.DecadeFactor), 10);

                        // Calculate 90% - 110%; using 100,000 as 100% (for precision).
                        int modifier = 100000 + ((150 * data.m_health) + (50 * data.m_wellbeing) - 10000);

                        // Death chance is simply if a random number between 0 and the modifier calculated above is less than the survival probability calculation for that decade of life.
                        // Also set maximum age of 400 (~114 years) to be consistent with the base game.
                        died = (simulationManager.m_randomizer.Int32(0, modifier) < DataStore.SurvivalProbCalc[index]) || newAge > 400;

                        // Check for sickness chance if they haven't died.
                        if (!died && simulationManager.m_randomizer.Int32(0, modifier) < DataStore.SicknessProbCalc[index])
                        {
                            // Make people sick, if they're unlucky.
                            data.Sick = true;

                            if (LifecycleLogging.UseSicknessLog)
                            {
                                LifecycleLogging.WriteToLog(LifecycleLogging.SicknessLogName, "Citizen became sick with chance factor ", DataStore.SicknessProbCalc[index]);
                            }
                        }
                    }

                    // Handle citizen death.
                    if (died)
                    {
                        // Check if citizen is only remaining parent and there are children.
                        uint unitID = data.GetContainingUnit(citizenID, Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_homeBuilding].m_citizenUnits, CitizenUnit.Flags.Home);
                        CitizenUnit containingUnit = citizenManager.m_units.m_buffer[unitID];

                        // Log if we're doing that.
                        if (LifecycleLogging.UseDeathLog)
                        {
                            LifecycleLogging.WriteToLog(LifecycleLogging.DeathLogName, "Killed citzen ", citizenID, " at age ", data.Age, " (", (int)(data.Age / ModSettings.AgePerYear), " years old) with family ", containingUnit.m_citizen0, ", " + containingUnit.m_citizen1, ", ", containingUnit.m_citizen2, ", ", containingUnit.m_citizen3, ", ", containingUnit.m_citizen4);
                        }

                        // Reverse redirect to access private method Die().
                        DieRev(__instance, citizenID, ref data);

                        // If there are no adults remaining in this CitizenUnit, remove the others, as orphan households end up in simulation purgatory.
                        bool hasParent = containingUnit.m_citizen0 == citizenID | containingUnit.m_citizen1 == citizenID;
                        bool singleParent = hasParent && (containingUnit.m_citizen0 == 0 | containingUnit.m_citizen1 == 0);
                        bool hasChild = containingUnit.m_citizen2 != 0 | containingUnit.m_citizen3 != 0 | containingUnit.m_citizen4 != 0;

                        if (singleParent && hasChild)
                        {
                            for (int i = 0; i <= 2; ++i)
                            {
                                uint currentChild;
                                switch (i)
                                {
                                    case 0:
                                        currentChild = containingUnit.m_citizen2;
                                        break;
                                    case 1:
                                        currentChild = containingUnit.m_citizen3;
                                        break;
                                    default:
                                        currentChild = containingUnit.m_citizen4;
                                        break;
                                }

                                if (currentChild != 0)
                                {
                                    if (LifecycleLogging.UseDeathLog)
                                    {
                                        LifecycleLogging.WriteToLog(LifecycleLogging.DeathLogName, "Removed orphan ", currentChild);
                                        citizenManager.ReleaseCitizen(currentChild);
                                    }
                                }
                            }
                        }

                        // Chance for 'vanishing corpse' (no need for deathcare).
                        if (!KeepCorpse())
                        {
                            citizenManager.ReleaseCitizen(citizenID);
                            removed = true;
                        }
                    }
                }
            }

            // Original method return value.
            __result = removed;

            // Don't execute base method after this.
            return false;
        }

        /// <summary>
        /// Harmony pre-emptive Prefix patch to ResidentAI.UpdateWorkplace to stop children below school age going to school, and to align young adult and adult behaviour with custom childhood factors.
        /// </summary>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch("UpdateWorkplace")]
        [HarmonyPrefix]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool UpdateWorkplace(uint citizenID, ref Citizen data)
        {
            // Don't do anything if the citizen is employed or is homeless.
            if (data.m_workBuilding != 0 || data.m_homeBuilding == 0)
            {
                // Don't execute original method (which would just abort anyway).
                return false;
            }

            // Local references.
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            Vector3 position = buildingManager.m_buildings.m_buffer[data.m_homeBuilding].m_position;
            DistrictManager districtManager = Singleton<DistrictManager>.instance;
            byte district = districtManager.GetDistrict(position);
            DistrictPolicies.Services servicePolicies = districtManager.m_districts.m_buffer[district].m_servicePolicies;
            bool isEducationBoostActive = ( (servicePolicies & DistrictPolicies.Services.EducationBoost) != 0 );
            bool isSchoolsOutActive     = ( (servicePolicies & DistrictPolicies.Services.SchoolsOut)     != 0 );
            int age = data.Age;
            var randomizer = Singleton<SimulationManager>.instance.m_randomizer;

            // Default transfer reason - will be replaced with any valid workplace offers.
            TransferManager.TransferReason educationReason = TransferManager.TransferReason.None;
            TransferManager.TransferReason workReason      = TransferManager.TransferReason.None;
            bool isSearchingForSchool = false;

            Logging.Message($"UpdateWorkplace: citizenID={citizenID}, age={age}, group={Citizen.GetAgeGroup(age)}, eduLevel={data.EducationLevel}, unempl={data.Unemployed}");

            // Treatment depends on citizen age.
            switch (Citizen.GetAgeGroup(age))
            {
                case Citizen.AgeGroup.Child:
                    // Is this a young child?
                    if (age < ModSettings.SchoolStartAge)
                    {
                        // Young children should never be educated.
                        // Sometimes the UpdateWellbeing method (called immediately before UpdateWorkplace in SimulationStep) will give these kids education, so we just clear it here.
                        // Easier than messing with UpdateWellbeing.
                        data.Education1 = false;

                        // Young children should also not go shopping (this is checked in following UpdateLocation call in SimulationStep).
                        // This prevents children from going shopping normally (vanilla code), but an additional patch is needed for the Real Time mod - see RealTime.cs.
                        data.m_flags &= ~Citizen.Flags.NeedGoods;

                        // Don't execute original method (thus avoiding assigning to a school).
                        return false;
                    }

                    // If of school age, and not already educated, go to elementary school.
                    if (!data.Education1)
                    {
                        educationReason = TransferManager.TransferReason.Student1;
                        Logging.Message($"...Child, reason {educationReason}");
                    }

                    break;

                case Citizen.AgeGroup.Teen:
                    // Try to go to HighSchool when TeenStartAge
                    isSearchingForSchool = (age - ModSettings.TeenStartAge < 3);
                    if (data.Education1 && !data.Education2 && isSearchingForSchool)
                    {
                        // Teens try 3 times go to high school, if they've finished elementary school.
                        int educationProb = ModSettings.Settings.EduProbTeen;
                        if (isEducationBoostActive)
                            educationProb = educationProb * ModSettings.Settings.FactorEducationBoost / 100;
                        if (isSchoolsOutActive)
                            educationProb = educationProb * ModSettings.Settings.FactorSchoolsOut / 100;
                        int chance = randomizer.Int32(100);
                        if (chance < educationProb)
                            educationReason = TransferManager.TransferReason.Student2;
                        Logging.Message("...Teen, eduProb ", educationProb, " cycle ", age - ModSettings.TeenStartAge, " chance ", chance, " reason  ", educationReason);
                    }

                    // When reaches WorkStartAge, tried 3x and still not in school => go to work
                    if (educationReason == TransferManager.TransferReason.None && age >= ModSettings.WorkStartAge && !isSearchingForSchool)
                    {
                        workReason = TransferManager.TransferReason.Worker1;
                    }

                    break;

                case Citizen.AgeGroup.Young:
                    // Try to go to University when YoungStartAge
                    isSearchingForSchool = (age - ModSettings.YoungStartAge < 3);
                    if (data.Education1 && data.Education2 && !data.Education3 && isSearchingForSchool)
                    {
                        // Young Adults try 3 times go to university, if they've secondary education
                        int educationProb = ModSettings.Settings.EduProbYoung;
                        if (isEducationBoostActive)
                            educationProb = educationProb * ModSettings.Settings.FactorEducationBoost / 100;
                        if (isSchoolsOutActive)
                            educationProb = educationProb * ModSettings.Settings.FactorSchoolsOut / 100;
                        int chance = randomizer.Int32(100);
                        if (chance < educationProb)
                            educationReason = TransferManager.TransferReason.Student3;
                        Logging.Message("...Young, eduProb ", educationProb, " cycle ", age - ModSettings.YoungStartAge, " chance ", chance, " reason  ", educationReason);
                    }

                    // When reaches WorkStartAge, tried 3x and still not in school => go to work
                    if (educationReason == TransferManager.TransferReason.None && age >= ModSettings.WorkStartAge && !isSearchingForSchool)
                    {
                        workReason = TransferManager.TransferReason.Worker2;
                    }

                    break;

                case Citizen.AgeGroup.Adult:
                    // Adults should primarly go for work that correlates with their education
                    TransferManager.TransferReason adultReason = TransferManager.TransferReason.None; // helper to avoid 2 switches
                    switch (data.EducationLevel)
                    {
                        case Citizen.Education.Uneducated:
                            workReason = TransferManager.TransferReason.Worker0;
                            adultReason = TransferManager.TransferReason.Student1;
                            break;
                        case Citizen.Education.OneSchool:
                            workReason = TransferManager.TransferReason.Worker1;
                            adultReason = TransferManager.TransferReason.Student2;
                            break;
                        case Citizen.Education.TwoSchools:
                            workReason = TransferManager.TransferReason.Worker2;
                            adultReason = TransferManager.TransferReason.Student3;
                            break;
                        case Citizen.Education.ThreeSchools:
                            workReason = TransferManager.TransferReason.Worker3;
                            break;
                    }

                    // However, when Unemployed for more than a treshold, they will try to raise an education level with some residual chance
                    if (data.Unemployed > ModSettings.Settings.UnemployedAge && data.EducationLevel != Citizen.Education.ThreeSchools)
                    {
                        int educationProb = ModSettings.Settings.EduProbAdult; // per missing edu level
                        if (!data.Education1)
                            educationProb *= 4;
                        else if (!data.Education2)
                            educationProb *= 2;
                        // policies don't affect Adults because their motivation is internal (Unemployment) not external (governance)
                        int chance = randomizer.Int32(100);
                        if (chance < educationProb)
                            educationReason = adultReason;
                        Logging.Message("...Adult, eduProb ", educationProb, " unemployed ", data.Unemployed, " chance ", chance, " reason ", educationReason);
                    }

                    break;
            }

            // Look for work
            if (workReason != TransferManager.TransferReason.None &&
                //data.Unemployed != 0 &&
                age >= ModSettings.WorkStartAge && // failsafe so children won't go to work (it happens!)
                educationReason == TransferManager.TransferReason.None) // going 
            {
                TransferManager.TransferOffer jobSeeking = default;
                jobSeeking.Priority = Singleton<SimulationManager>.instance.m_randomizer.Int32(8u);
                jobSeeking.Citizen = citizenID;
                jobSeeking.Position = position;
                jobSeeking.Amount = 1;
                jobSeeking.Active = true;
                Singleton<TransferManager>.instance.AddOutgoingOffer(workReason, jobSeeking);
                Logging.Message($"...looking for JOB, reason={workReason}");
            }

            // Look for school
            if (educationReason != TransferManager.TransferReason.None)
            {
                // Look for education (this can be parallel with looking for work, above).
                TransferManager.TransferOffer educationSeeking = default;
                educationSeeking.Priority = Singleton<SimulationManager>.instance.m_randomizer.Int32(8u);
                educationSeeking.Citizen = citizenID;
                educationSeeking.Position = position;
                educationSeeking.Amount = 1;
                educationSeeking.Active = true;
                Singleton<TransferManager>.instance.AddOutgoingOffer(educationReason, educationSeeking);
                Logging.Message($"...looking for EDU, reason={educationReason}");
            }

            // If we got here, we need to continue on to the original method (this is not a young child).
            return false;
        }

        /// <summary>
        /// Reverse patch for ResidentAI.FinishSchoolOrWork to access private method of original instance.
        /// </summary>
        /// <param name="instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ResidentAI), "FinishSchoolOrWork")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FinishSchoolOrWorkRev(object instance, uint citizenID, ref Citizen data)
        {
            string message = "FinishSchoolOrWork reverse Harmony patch wasn't applied";
            Logging.Error(message, instance.ToString(), citizenID.ToString(), data.ToString());
            throw new NotImplementedException(message);
        }

        /// <summary>
        /// Reverse patch for ResidentAI.Die to access private method of original instance.
        /// </summary>
        /// <param name="instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ResidentAI), "Die")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DieRev(object instance, uint citizenID, ref Citizen data)
        {
            string message = "Die reverse Harmony patch wasn't applied";
            Logging.Error(message, instance, citizenID, data);
            throw new NotImplementedException(message);
        }

        /// <summary>
        /// Calculates whether or not a corpse should remain (to be picked up deathcare services), or 'vanish into thin air'.
        /// </summary>
        /// <returns>True if the corpse should remain, False if the corpse should vanish.</returns>
        public static bool KeepCorpse()
        {
            return Singleton<SimulationManager>.instance.m_randomizer.Int32(0, 99) > DataStore.AutoDeadRemovalChance;
        }
    }
}
