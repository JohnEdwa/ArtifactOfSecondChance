using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using R2API.Utils;
using System;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;

using MoreArtifacts;

namespace SecondChanceArtifact {

    /// <summary>
    /// Your artifact mod.
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(MoreArtifacts.MoreArtifacts.ModGUID, MoreArtifacts.MoreArtifacts.ModVersion)]
    [BepInDependency(R2API.R2API.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [R2APISubmoduleDependency("LanguageAPI")]
    public class SecondChanceArtifactMod : BaseUnityPlugin {
        public const string ModGUID = "com.johnedwa.SecondChanceArtifact";
        public const string ModName = "Artifact of Second Chance";
        public const string ModVersion = "1.0.0";

        internal static new BepInEx.Logging.ManualLogSource Logger { get; private set; }

        public static SecondChanceArtifact SecondChanceArtifact;
        public static ConfigEntry<double> CurseMultiplierConfig { get; set; }
        public static ConfigEntry<double> RegenMultiplierConfig { get; set; }
        public static ConfigEntry<double> MovementMultiplierConfig { get; set; }

        public void Awake() {
            Logger = base.Logger;

            CurseMultiplierConfig = Config.Bind<double>("", "Curse Multiplier", 0.15, new ConfigDescription("Curse (Max HP Loss) in percentage. Linear.", new AcceptableValueRange<double>(0.0, 1.0)));
            RegenMultiplierConfig = Config.Bind<double>("", "Regen Multiplier", 0.40, new ConfigDescription("Regen Reduction in percentage. Exponential.", new AcceptableValueRange<double>(0.0, 1.0)));
            MovementMultiplierConfig = Config.Bind<double>("", "Movement Speed Multiplier", 0.25, new ConfigDescription("Movement Speed Reduction in percentage. Exponential.", new AcceptableValueRange<double>(0.0, 1.0)));

            // initialize artifacts and other things here
            SecondChanceArtifact = new SecondChanceArtifact();
        }
    }

    /// <summary>
    /// The actual artifact definition.
    /// </summary>
    public class SecondChanceArtifact : NewArtifact<SecondChanceArtifact> {

        public override string Name => "Artifact of Second Chance";
        public override string Description => "Start with Dio's Best Friend, but the Curse of Resurrection weighs a heavy burden.";
        public override Sprite IconSelectedSprite => CreateSprite(Properties.Resources.example_selected, Color.magenta);
        public override Sprite IconDeselectedSprite => CreateSprite(Properties.Resources.example_deselected, Color.gray);

        protected override void InitManager() {
            SecondChanceArtifactManager.Init();
        }
    }
    

    /// <summary>
    /// Overarching Manager for this artifact. Handles hooking and unhooking actions.
    /// </summary>
    public static class SecondChanceArtifactManager {
        private static ArtifactDef myArtifact {
            get { return SecondChanceArtifact.Instance.ArtifactDef; }
        }

        private static readonly Dictionary<string, string> DefaultLanguage = new Dictionary<string, string>();
        private static string[] oldText = new string[3];
        private static CharacterMaster currentPlayer;
        private static int consumedCount;

        public static void Init() {
            // initialize stuff here, like fields, properties, or things that should run only one time  

            Debug.LogWarning("Artifact of Second Chance Loaded, configured multiplier for 1/2/3 stacks:");
            Debug.Log("Curse Amount   : " + (SecondChanceArtifactMod.CurseMultiplierConfig.Value * 1)*100 + "%, "  + (SecondChanceArtifactMod.CurseMultiplierConfig.Value * 2)*100 + "%, "  + (SecondChanceArtifactMod.CurseMultiplierConfig.Value * 3)*100 + "%.");
            Debug.Log("Regen Speed    : " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.RegenMultiplierConfig.Value), 1)))*100 + "%, " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.RegenMultiplierConfig.Value), 2)))*100 + "%, " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.RegenMultiplierConfig.Value), 3)))*100 + "%.");
            Debug.Log("Movement Speed : " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.MovementMultiplierConfig.Value), 1)))*100 + "%, " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.MovementMultiplierConfig.Value), 2)))*100 + "%, " + (1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.MovementMultiplierConfig.Value), 3)))*100 + "%.");

            RunArtifactManager.onArtifactEnabledGlobal += OnArtifactEnabled;
            RunArtifactManager.onArtifactDisabledGlobal += OnArtifactDisabled;
        }

        private static void OnArtifactEnabled(RunArtifactManager man, ArtifactDef artifactDef) {
            if(!NetworkServer.active || artifactDef != myArtifact) {
                return;
            }

            IL.RoR2.CharacterBody.RecalculateStats += (il) => {
                ILCursor c = new ILCursor(il);
                ILLabel dioend = il.DefineLabel();

                try {
                    c.Index = 0;
                    // find the item counts
                    c.GotoNext ( 
                        MoveType.Before, 
                        x => x.MatchLdarg(0),
                        x => x.MatchCallvirt<CharacterBody>("get_inventory"),
                        x => x.MatchLdcI4(0x21),
                        x => x.MatchCallvirt<Inventory>("GetItemCount"),
                        x => x.MatchStloc(0)
                    );

                    // Add an item check
                    if (c.Index != 0) {
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Action<CharacterBody>> ((cb) => {
                            consumedCount = cb.master.inventory.GetItemCount(ItemIndex.ExtraLifeConsumed);
                        });
                    }

                    c.Index = 0;

                    // find the weakness buff spot in the code
                    c.GotoNext ( 
                        MoveType.Before, 
                        x => x.MatchLdarg(0),
                        x => x.MatchLdcI4(0x21),
                        x => x.MatchCallvirt<CharacterBody>("HasBuff")
                    );


                    if (c.Index != 0) {
                        c.Index++;
                        // If the artifact isn't enabled, jump over the code
                        c.EmitDelegate<Func<bool>> (() => { return RunArtifactManager.instance.IsArtifactEnabled(myArtifact.artifactIndex); });
                        c.Emit(OpCodes.Brfalse, dioend);

                        // this.cursePenalty
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_cursePenalty"));
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => {  return ((float)SecondChanceArtifactMod.CurseMultiplierConfig.Value * consumedCount); });
                        c.Emit(OpCodes.Add);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_cursePenalty", BindingFlags.Instance | BindingFlags.NonPublic )); 

                        // this.regen
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_regen")); // 1489	0E73	call	instance float32 RoR2.CharacterBody::get_cursePenalty()
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => { return 1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.RegenMultiplierConfig.Value), consumedCount)); });
                        c.Emit(OpCodes.Mul);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_regen", BindingFlags.Instance | BindingFlags.NonPublic ));

                        // this.moveSpeed
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_moveSpeed"));
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => { return 1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.MovementMultiplierConfig.Value), consumedCount)); });
                        c.Emit(OpCodes.Mul);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_moveSpeed", BindingFlags.Instance | BindingFlags.NonPublic ));
                        
                        c.MarkLabel(dioend);
                    }

                } catch (Exception ex) { Debug.LogError(ex); }
            };	

            ModifyTexts(true);

            // chat message when pickup the consumed dio
            On.RoR2.CharacterMaster.RespawnExtraLife += (orig, self) => { 
                orig(self); 

                if (RunArtifactManager.instance.IsArtifactEnabled(myArtifact.artifactIndex)) {
                    consumedCount = PlayerCharacterMasterController.instances[0].master.inventory.GetItemCount(ItemIndex.ExtraLifeConsumed);
                    RoR2.Chat.AddMessage(
                    "The <color=purple>Curse of Resurrection</color> weighs a heavy burden on the soul of <color=red>" + PlayerCharacterMasterController.instances[0].GetDisplayName() + 
                    "</color> (Curse: " + ((SecondChanceArtifactMod.CurseMultiplierConfig.Value * consumedCount)*100) + "%, " +
                    "Regen -" +
                    (100-(1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.RegenMultiplierConfig.Value), consumedCount))*100)) + "%, " +
                    "Move Speed -" +
                    (100-(1 * (Mathf.Pow((1-(float)SecondChanceArtifactMod.MovementMultiplierConfig.Value), consumedCount))*100)) + "%)");
                };
            };

            // hook things
            Run.onRunStartGlobal += Something;
        }

        private static void OnArtifactDisabled(RunArtifactManager man, ArtifactDef artifactDef) {
            if(artifactDef != myArtifact) {
                return;
            }
            ModifyTexts(false);

            // unhook things
            Run.onRunStartGlobal -= Something;
        }

        private static void Something(Run run) {
            consumedCount = 0;

            if (currentPlayer == null) {
                currentPlayer = PlayerCharacterMasterController.instances[0].master;
                currentPlayer.inventory.GiveItem(ItemCatalog.FindItemIndex("ExtraLife"),1);
            }
        }

        private static void ModifyTexts(bool replace) {
            if (replace) {
                oldText[0] = ReplaceString("ITEM_EXTRALIFECONSUMED_NAME", "Dio's Affliction");
                oldText[1] = ReplaceString("ITEM_EXTRALIFECONSUMED_PICKUP", "The curse of resurrection makes you feel frail. Curses Max HP, reduces Regen and Movement Speed.");
                oldText[2] = ReplaceString("ITEM_EXTRALIFECONSUMED_DESC", "The curse of resurrection makes you feel frail. Curses Max HP, reduces Regen and Movement Speed.");
                RoR2.Language.CCLanguageReload(new ConCommandArgs());
            } else {
                ReplaceString("ITEM_EXTRALIFECONSUMED_NAME", oldText[0]);
                ReplaceString("ITEM_EXTRALIFECONSUMED_PICKUP", oldText[1]);
                ReplaceString("ITEM_EXTRALIFECONSUMED_DESC", oldText[2]);
                RoR2.Language.CCLanguageReload(new ConCommandArgs());
            }
        }

        private static string ReplaceString(string token, string newText)
		{
			DefaultLanguage[token] = Language.GetString(token);
			R2API.LanguageAPI.Add(token, newText);
            return DefaultLanguage[token];
		}
    }

    public static class Utils{
		public static T GetInstanceField<T>(this object instance, string fieldName){
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr); return (T)((object)field.GetValue(instance));}
		public static void SetInstanceField<T>(this object instance, string fieldName, T value){
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr);field.SetValue(instance, value);}
	}
}
