﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;

// ReSharper disable UnusedMember.Local

namespace ShareSuite
{
    [BepInDependency("com.frogtown.shared", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.funkfrog_sipondo.sharesuite", "ShareSuite", "1.6.0")]
    public class ShareSuite : BaseUnityPlugin
    {
        public static ConfigWrapper<bool> WrapModIsEnabled;
        public static ConfigWrapper<bool> WrapMoneyIsShared;
        public static ConfigWrapper<int> WrapMoneyScalar;
        public static ConfigWrapper<bool> WrapWhiteItemsShared;
        public static ConfigWrapper<bool> WrapGreenItemsShared;
        public static ConfigWrapper<bool> WrapRedItemsShared;
        public static ConfigWrapper<bool> WrapLunarItemsShared;
        public static ConfigWrapper<bool> WrapBossItemsShared;
        public static ConfigWrapper<bool> WrapQueensGlandsShared;
        public static ConfigWrapper<bool> WrapPrinterCauldronFixEnabled;
        public static ConfigWrapper<bool> WrapOverridePlayerScalingEnabled;
        public static ConfigWrapper<int> WrapInteractablesCredit;
        public static ConfigWrapper<bool> WrapOverrideBossLootScalingEnabled;
        public static ConfigWrapper<int> WrapBossLootCredit;
        public static ConfigWrapper<bool> WrapDeadPlayersGetItems;
        public static ConfigWrapper<string> WrapItemBlacklist;

        public static HashSet<int> GetItemBlackList()
        {
            var blacklist = new HashSet<int>();
            var rawPieces = WrapItemBlacklist.Value.Split(',');
            foreach(var piece in rawPieces)
            {
                if(int.TryParse(piece, out int itemNum)){
                    blacklist.Add(itemNum);
                }
            }
            return blacklist;
        }

        public ShareSuite()
        {
            InitWrap();
            On.RoR2.Console.Awake += (orig, self) =>
            {
                CommandHelper.RegisterCommands(self);
                FrogtownInterface.Init(Config);
                orig(self);
            };
            // Register all the hooks
            Hooks.OnGrantItem();
            Hooks.OnShopPurchase();
            Hooks.OnPurchaseDrop();
            Hooks.DisableInteractablesScaling();
            Hooks.ModifyGoldReward();
            Hooks.SplitTpMoney();
            Hooks.FixBoss();
        }

        public class CommandHelper
        {
            public static void RegisterCommands(RoR2.Console self)
            {
                var types = typeof(CommandHelper).Assembly.GetTypes();
                var catalog = self.GetFieldValue<IDictionary>("concommandCatalog");

                foreach (var methodInfo in types.SelectMany(x =>
                    x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
                {
                    var customAttributes = methodInfo.GetCustomAttributes(false);
                    foreach (var attribute in customAttributes.OfType<ConCommandAttribute>())
                    {
                        var conCommand = Reflection.GetNestedType<RoR2.Console>("ConCommand").Instantiate();

                        conCommand.SetFieldValue("flags", attribute.flags);
                        conCommand.SetFieldValue("helpText", attribute.helpText);
                        conCommand.SetFieldValue("action", (RoR2.Console.ConCommandDelegate)
                            Delegate.CreateDelegate(typeof(RoR2.Console.ConCommandDelegate), methodInfo));

                        catalog[attribute.commandName.ToLower()] = conCommand;
                    }
                }
            }
        }
        
        public void InitWrap()
        {
            WrapModIsEnabled = Config.Wrap(
                "Settings",
                "ModEnabled",
                "Toggles mod.",
                true);

            // Add config options for all settings
            WrapMoneyIsShared = Config.Wrap(
                "Settings",
                "MoneyShared",
                "Toggles money sharing.",
                false);

            WrapMoneyScalar = Config.Wrap(
                "Settings",
                "MoneyScalar",
                "Modifies player count used in calculations of gold earned when money sharing is on.",
                1);

            WrapWhiteItemsShared = Config.Wrap(
                "Settings",
                "WhiteItemsShared",
                "Toggles item sharing for common items.",
                true);

            WrapGreenItemsShared = Config.Wrap(
                "Settings",
                "GreenItemsShared",
                "Toggles item sharing for rare items.",
                true);

            WrapRedItemsShared = Config.Wrap(
                "Settings",
                "RedItemsShared",
                "Toggles item sharing for legendary items.",
                true);

            WrapLunarItemsShared = Config.Wrap(
                "Settings",
                "LunarItemsShared",
                "Toggles item sharing for Lunar items.",
                false);

            WrapBossItemsShared = Config.Wrap(
                "Settings",
                "BossItemsShared",
                "Toggles item sharing for boss items.",
                true);

            WrapQueensGlandsShared = Config.Wrap(
                "Balance",
                "QueensGlandsShared",
                "Toggles item sharing for specifically the Queen's Gland (reduces possible lag).",
                false);

            WrapPrinterCauldronFixEnabled = Config.Wrap(
                "Balance",
                "PrinterCauldronFix",
                "Toggles 3D printer and Cauldron item dupe fix by giving the item directly instead of" +
                " dropping it on the ground.",
                true);

            WrapOverridePlayerScalingEnabled = Config.Wrap(
                "Balance",
                "DisablePlayerScaling",
                "Toggles override of the scalar of interactables (chests, shrines, etc) that spawn in the world to your configured credit.",
                true);

            WrapInteractablesCredit = Config.Wrap(
                "Balance",
                "InteractablesCredit",
                "If player scaling via this mod is enabled, the amount of players the game should think are playing in terms of chest spawns.",
                1);

            WrapOverrideBossLootScalingEnabled = Config.Wrap(
                "Balance",
                "DisableBossLootScaling",
                "Toggles override of the scalar of boss loot drops to your configured balance.",
                true);

            WrapBossLootCredit = Config.Wrap(
                "Settings",
                "BossLootCredit",
                "Specifies the amount of boss items dropped when boss drop override is true.",
                1);

            WrapDeadPlayersGetItems = Config.Wrap(
                "Balance",
                "DeadPlayersGetItems",
                "Toggles item sharing for dead players.",
                false);

            WrapItemBlacklist = Config.Wrap(
                "Settings",
                "ItemBlacklist",
                "Items (by index) that you do not want to share, comma seperated. Please find the item indices at: https://github.com/risk-of-thunder/R2Wiki/wiki/Item-Equipment-names",
                "");
        }

        private static bool TryParseIntoConfig<T>(string rawValue, ConfigWrapper<T> wrapper)
        {
            switch (wrapper)
            {
                case ConfigWrapper<bool> boolWrapper when bool.TryParse(rawValue, out bool result):
                    boolWrapper.Value = result;
                    return true;
                case ConfigWrapper<int> intWrapper when int.TryParse(rawValue, out int result):
                    intWrapper.Value = result;
                    return true;
                default:
                    return false;
            }
        }

        // ModIsEnabled
        [ConCommand(commandName = "ss_Enabled", flags = ConVarFlags.None, helpText = "Toggles mod.")]
        private static void CcModIsEnabled(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapModIsEnabled))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Mod status set to {WrapModIsEnabled}.");
        }

        // MoneyIsShared
        [ConCommand(commandName = "ss_MoneyIsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether money is shared or not.")]
        private static void CcMoneyIsShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapMoneyIsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Money sharing set to {WrapMoneyIsShared}.");
        }

        // MoneyScalar
        [ConCommand(commandName = "ss_MoneyScalar", flags = ConVarFlags.None,
            helpText = "Modifies percent of gold earned when money sharing is on.")]
        private static void CcMoneyScalar(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapMoneyScalar))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Money multiplier set to {WrapMoneyScalar}.");
        }

        // WhiteItemsShared
        [ConCommand(commandName = "ss_WhiteItemsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether white items are shared or not.")]
        private static void CcWhiteShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapWhiteItemsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"White item sharing set to {WrapWhiteItemsShared}.");
        }

        // GreenItemsShared
        [ConCommand(commandName = "ss_GreenItemsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether green items are shared or not.")]
        private static void CcGreenShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapGreenItemsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Green item sharing set to {WrapGreenItemsShared}.");
        }

        // RedItemsShared
        [ConCommand(commandName = "ss_RedItemsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether red items are shared or not.")]
        private static void CcRedShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapRedItemsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Red item sharing set to {WrapRedItemsShared}.");
        }

        // LunarItemsShared
        [ConCommand(commandName = "ss_LunarItemsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether lunar items are shared or not.")]
        private static void CcLunarShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapLunarItemsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Lunar item sharing set to {WrapLunarItemsShared}.");
        }

        // BossItemsShared
        [ConCommand(commandName = "ss_BossItemsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether boss items are shared or not.")]
        private static void CcBossShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapBossItemsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Boss item sharing set to {WrapBossItemsShared}.");
        }

        // QueensGlandsShared
        [ConCommand(commandName = "ss_QueensGlandsShared", flags = ConVarFlags.None,
            helpText = "Modifies whether Queens Glands are shared or not.")]
        private static void CcQueenShared(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapQueensGlandsShared))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Queens Gland sharing set to {WrapQueensGlandsShared}.");
        }

        // PrinterCauldronFix
        [ConCommand(commandName = "ss_PrinterCauldronFix", flags = ConVarFlags.None,
            helpText = "Modifies whether printers and cauldrons should not duplicate items.")]
        private static void CcPrinterCauldronFix(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapPrinterCauldronFixEnabled))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Printer and cauldron fix set to {WrapPrinterCauldronFixEnabled}.");
        }

        // DisablePlayerScaling
        [ConCommand(commandName = "ss_OverridePlayerScaling", flags = ConVarFlags.None,
            helpText = "Modifies whether interactable count should scale based on player count.")]
        private static void CcDisablePlayerScaling(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapOverridePlayerScalingEnabled))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Player scaling disable set to {WrapOverridePlayerScalingEnabled}.");
        }

        // InteractablesCredit
        [ConCommand(commandName = "ss_InteractablesCredit", flags = ConVarFlags.None,
            helpText = "Modifies amount of interactables when player scaling is overridden.")]
        private static void CcInteractablesCredit(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapInteractablesCredit))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Interactables credit set to {WrapInteractablesCredit}.");
        }

        // DisableBossLootScaling
        [ConCommand(commandName = "ss_OverrideBossLootScaling", flags = ConVarFlags.None,
            helpText = "Modifies whether boss loot should scale based on player count.")]
        private static void CcBossLoot(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapOverrideBossLootScalingEnabled))
                Debug.Log("Invalid arguments.");
            else
            {
                Debug.Log($"Boss loot scaling disable set to {WrapOverrideBossLootScalingEnabled}.");
                Hooks.FixBoss();
            }
        }

        // BossLootCredit
        [ConCommand(commandName = "ss_BossLootCredit", flags = ConVarFlags.None,
            helpText = "Modifies amount of boss item drops.")]
        private static void CCBossLootCredit(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapBossLootCredit))
                Debug.Log("Invalid arguments.");
            else
            {
                Debug.Log($"Boss loot credit set to {WrapBossLootCredit}.");
                Hooks.FixBoss();
            }       
        }

        // DeadPlayersGetItems
        [ConCommand(commandName = "ss_DeadPlayersGetItems", flags = ConVarFlags.None,
            helpText = "Modifies whether boss loot should scale based on player count.")]
        private static void CcDeadPlayersGetItems(ConCommandArgs args)
        {
            if (args.Count != 1 || !TryParseIntoConfig(args[0], WrapDeadPlayersGetItems))
                Debug.Log("Invalid arguments.");
            else
                Debug.Log($"Boss loot scaling disable set to {WrapDeadPlayersGetItems}.");
        }
    }
}