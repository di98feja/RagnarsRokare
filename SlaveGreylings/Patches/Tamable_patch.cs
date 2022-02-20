using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        class MyTextReceiver : TextReceiver
        {
            private ZNetView m_nview;
            private readonly Character m_character;

            public MyTextReceiver(Character character)
            {
                this.m_nview = character.GetComponent<ZNetView>();
                this.m_character = character;
            }

            public string GetText()
            {
                return m_nview.GetZDO().GetString(Constants.Z_GivenName);
            }

            public void SetText(string text)
            {
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(Constants.Z_GivenName, text);
                BroadcastUpdateCharacterName(ref m_nview, text);
            }
        }


        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        static class Tameable_GetHoverName_Patch
        {
            static bool Prefix(Tameable __instance, ref string __result, ZNetView ___m_nview, Character ___m_character)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name)) return true;
                if (!___m_character.IsTamed()) return true;
                if (!___m_nview.IsValid())
                {
                    __result = string.Empty;
                    return true;
                }
                string aiStatus = ___m_nview.GetZDO().GetString(Constants.Z_AiStatus) ?? Traverse.Create(__instance).Method("GetStatusString").GetValue() as string;
                string str = Localization.instance.Localize(___m_character.GetHoverName());
                str += Localization.instance.Localize($" ({aiStatus})");
                __result = str + Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $hud_pet" + "\n[<color=yellow>Hold E</color>] to change name");

                return false;
            }
        }
        [HarmonyPatch(typeof(Tameable), "Interact")]
        static class Tameable_Interact_Patch
        {
            static bool Prefix(Tameable __instance, ref bool __result, Humanoid user, bool hold, ZNetView ___m_nview, ref Character ___m_character,
                ref float ___m_lastPetTime)
            {
                if (!MobConfigManager.IsControllableMob(__instance.name)) return true;

                if (!___m_nview.IsValid())
                {
                    __result = false;
                    return true;
                }
                string hoverName = ___m_character.GetHoverName();
                if (___m_character.IsTamed())
                {
                    if (hold)
                    {
                        TextInput.instance.RequestText(new MyTextReceiver(___m_character), "Name", 15);
                        __result = false;
                        return false;
                    }

                    if (Time.time - ___m_lastPetTime > 1f)
                    {
                        ___m_lastPetTime = Time.time;
                        __instance.m_petEffect.Create(___m_character.GetCenterPoint(), Quaternion.identity);
                        if (__instance.m_commandable)
                        {
                            typeof(Tameable).GetMethod("Command", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { user });
                        }
                        else
                        {
                            user.Message(MessageHud.MessageType.Center, hoverName + " $hud_tamelove");
                        }
                        __result = true;
                        return false;
                    }
                    __result = false;
                    return false;
                }
                __result = false;
                return false;
            }
        }
    }
}