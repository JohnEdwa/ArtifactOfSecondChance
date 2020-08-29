using BepInEx;
using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using MoreArtifacts;
using R2API.Utils;
using System.Collections.Generic;
using System.Reflection;
using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Rewired;


namespace SecondChanceArtifact {

    /// <summary>
    /// Your artifact mod.
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(MoreArtifacts.MoreArtifacts.ModGUID, MoreArtifacts.MoreArtifacts.ModVersion)]
    [R2APISubmoduleDependency("LanguageAPI")]
    public class SecondChanceArtifactMod : BaseUnityPlugin {
        public const string ModGUID = "com.johnedwa.SecondChanceArtifact";
        public const string ModName = "Artifact of Second Chance";
        public const string ModVersion = "1.0.0";

        internal static new BepInEx.Logging.ManualLogSource Logger { get; private set; }

        public static SecondChanceArtifact SecondChanceArtifact;

        public void Awake() {
            Logger = base.Logger;

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

        public static void Init() {
            // initialize stuff here, like fields, properties, or things that should run only one time           
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
                    c.GotoNext ( 
                        MoveType.Before, 
                        x => x.MatchLdarg(0), // 1388	0D5A	ldarg.0
                        x => x.MatchLdcI4(0x21), // 1389	0D5B	ldc.i4.s	0x21
                        x => x.MatchCallvirt<CharacterBody>("HasBuff") // 1390	0D5D	call	instance bool RoR2.CharacterBody::HasBuff(valuetype RoR2.BuffIndex)
                    );

                    if (c.Index != 0) {
                        c.Index++;

                        c.EmitDelegate<Func<bool>> (() => {
                            return RunArtifactManager.instance.IsArtifactEnabled(myArtifact.artifactIndex);
                        });
                        c.Emit(OpCodes.Brfalse, dioend);

                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_cursePenalty"));
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => {
                            float multiplier = 0;
                            if (cb.master.inventory) {
                                int consumed = cb.master.inventory.GetItemCount(ItemIndex.ExtraLifeConsumed);
                                if (consumed > 0) {
                                    multiplier = (0.15f * consumed);
                                }
                            }
                            return multiplier;
                        });
                        c.Emit(OpCodes.Add);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_cursePenalty", BindingFlags.Instance | BindingFlags.NonPublic ));  // 1495	0E82	call	instance void RoR2.CharacterBody::set_cursePenalty(float32)


                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_regen")); // 1489	0E73	call	instance float32 RoR2.CharacterBody::get_cursePenalty()
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => {
                            float multiplier = 1;
                            if (cb.master.inventory) {
                                int consumed = cb.master.inventory.GetItemCount(ItemIndex.ExtraLifeConsumed);
                                if (consumed > 0) {
                                    multiplier *= Mathf.Pow(0.85f, consumed);
                                }
                            }
                            return multiplier;
                        });
                        c.Emit(OpCodes.Mul);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_regen", BindingFlags.Instance | BindingFlags.NonPublic ));  // 1495	0E82	call	instance void RoR2.CharacterBody::set_cursePenalty(float32)

                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_moveSpeed")); // 1489	0E73	call	instance float32 RoR2.CharacterBody::get_cursePenalty()
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<CharacterBody, float>> ((cb) => {
                            float multiplier = 1;
                            if (cb.master.inventory) {
                                int consumed = cb.master.inventory.GetItemCount(ItemIndex.ExtraLifeConsumed);
                                if (consumed > 0) {
                                    multiplier *= Mathf.Pow(0.85f, consumed);
                                }
                            }
                            return multiplier;
                        });
                        c.Emit(OpCodes.Mul);
                        c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("set_moveSpeed", BindingFlags.Instance | BindingFlags.NonPublic ));  // 1495	0E82	call	instance void RoR2.CharacterBody::set_cursePenalty(float32)
                    
                        c.MarkLabel(dioend);
                    }

                } catch (Exception ex) { Debug.LogError(ex); }
            };	

            ModifyTexts(true);

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
            GiveDio();
        }
        
        private static CharacterMaster GetCurrentPlayer() {
        if (currentPlayer == null) {
                currentPlayer = PlayerCharacterMasterController.instances[0].master;
            }
            return currentPlayer;
        }

        private static void GiveDio()
		{   CharacterMaster currentPlayer = GetCurrentPlayer();
			currentPlayer.inventory.GiveItem(ItemCatalog.FindItemIndex("ExtraLife"),1);
		}

        private static void ModifyTexts(bool replace) {
            if (replace) {
                oldText[0] = ReplaceString("ITEM_EXTRALIFECONSUMED_NAME", "Dio's Affliction");
                oldText[1] = ReplaceString("ITEM_EXTRALIFECONSUMED_PICKUP", "The curse of resurrection makes you feel frail. Reduces Max HP, Regen and Movement Speed.");
                oldText[2] = ReplaceString("ITEM_EXTRALIFECONSUMED_DESC", "The curse of resurrection makes you feel frail. Reduces Max HP and Regen.");
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

    public static class Utils
	{
		public static T GetInstanceField<T>(this object instance, string fieldName)
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr);
			return (T)((object)field.GetValue(instance));
		}

		public static void SetInstanceField<T>(this object instance, string fieldName, T value)
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr);
			field.SetValue(instance, value);
		}
	}
}
