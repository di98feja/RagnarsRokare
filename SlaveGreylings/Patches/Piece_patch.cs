using HarmonyLib;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Piece), "Awake")]
        static class Piece_Awake_Patch
        {
            static void Postfix(Piece __instance, ref ZNetView ___m_nview)
            {
                GetOrCreateUniqueId(___m_nview);
            }

            private static string GetOrCreateUniqueId(ZNetView ___m_nview)
            {
                var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_UniqueId);
                if (string.IsNullOrEmpty(uniqueId))
                {
                    uniqueId = System.Guid.NewGuid().ToString();
                    ___m_nview.GetZDO().Set(Constants.Z_UniqueId, uniqueId);
                }
                return uniqueId;
            }
        }
    }
}