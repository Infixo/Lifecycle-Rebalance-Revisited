﻿// <copyright file="Mod.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using AlgernonCommons;
    using AlgernonCommons.Notifications;
    using AlgernonCommons.Patching;
    using AlgernonCommons.Translation;
    using ColossalFramework.Plugins;
    using ICities;

    /// <summary>
    /// The base mod class for instantiation by the game.
    /// </summary>
    public sealed class Mod : PatcherMod<OptionsPanel, Patcher>, IUserMod
    {
        /// <summary>
        /// Gets the mod's base display name (name only).
        /// </summary>
        public override string BaseName => "Lifecycle Rebalance Revisited (Infixo)";

        /// <summary>
        /// Gets the mod's unique Harmony identfier.
        /// </summary>
        public override string HarmonyID => "com.github.algernon-A.csl.lifecyclerebalancerevisited";

        /// <summary>
        /// Gets the mod's description for display in the content manager.
        /// </summary>
        public string Description => Translations.Translate("LBR_DESC");

        /// <summary>
        /// Gets the mod's what's new message array.
        /// </summary>
        public override WhatsNewMessage[] WhatsNewMessages => new WhatsNewMessageListing().Messages;

        /// <summary>
        /// Called by the game when the mod is enabled.
        /// </summary>
        public override void OnEnabled()
        {
            // Perform conflict detection.
            ConflictDetection conflictDetection = new ConflictDetection();
            if (conflictDetection.CheckModConflicts())
            {
                Logging.Error("aborting activation due to conflicting mods");

                // Load mod settings to ensure that correct language is selected for notification display.
                LoadSettings();

                // Disable mod.
                if (AssemblyUtils.ThisPlugin is PluginManager.PluginInfo plugin)
                {
                    Logging.KeyMessage("disabling mod");
                    plugin.isEnabled = false;
                }

                // Don't do anything further.
                return;
            }

            base.OnEnabled();

            // Load configuation file.
            DataStore.ReadFromXML();
        }

        /// <summary>
        /// Saves settings file.
        /// </summary>
        public override void SaveSettings() => ModSettings.Save();

        /// <summary>
        /// Loads settings file.
        /// </summary>
        public override void LoadSettings() => ModSettings.Load();
    }
}
