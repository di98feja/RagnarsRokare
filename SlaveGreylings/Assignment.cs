using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlaveGreylings
{
    public class AssignmentType
    {
        public string Name { get; set; }
        public string PieceName { get; set; }
        public Type ComponentType { get; set; }
        public float InteractDist { get; set; }
        public bool Activated { get; set; }
    }
    //public enum AssignmentTypes
    //{
    //    [Description("Smelter")]
    //    smelter,
    //    [Description("Kiln")]
    //    charcoal_kiln,
    //    blastfurnace,
    //    windmill,
    //    piece_spinningwheel,
    //    fire_pit,
    //    bonfire,
    //    hearth,
    //    piece_groundtorch_wood,
    //    piece_groundtorch,
    //    piece_groundtorch_green,
    //    piece_walltorch,
    //    piece_brazierceiling01
    //}

    public class Assignment : MonoBehaviour
    {
        private Vector3 m_position;

        public int BelongsTo { get; set; }
        public GameObject AssignmentObject { get; set; }
        public AssignmentType TypeOfAssignment { get; set; }
        public float AssignmentTime { get; set; }
        public Vector3 Position 
        {
            get
            {
                if (TypeOfAssignment.ComponentType == typeof(Smelter))
                {
                    return AssignmentObject.GetComponent<Smelter>().m_outputPoint.position;
                }
                else
                {
                    return AssignmentObject.transform.position;
                }
            }
            set
            {
                m_position = value;
            }
        } 

        public ItemDrop.ItemData NeedFuel
        {
            get 
            {
                if (TypeOfAssignment.ComponentType == typeof(Smelter))
                {
                    var smelter = AssignmentObject.GetComponent<Smelter>();
                    if (smelter.m_maxFuel != 0 && smelter.m_maxFuel - Mathf.CeilToInt(smelter.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f)) > 0)
                    {
                        return smelter.m_fuelItem.m_itemData;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (TypeOfAssignment.ComponentType == typeof(Fireplace))
                {
                    var fireplace = AssignmentObject.GetComponent<Fireplace>();
                    if (fireplace.m_maxFuel - Mathf.CeilToInt(fireplace.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f)) > 0)
                    {
                        return fireplace.m_fuelItem.m_itemData;
                    }
                    else
                    {
                        return null;
                    }
                }
                return null;
            }
        }

        public IEnumerable<ItemDrop.ItemData> NeedOre
        {
            get
            {
                if (TypeOfAssignment.ComponentType == typeof(Smelter))
                {
                    var smelter = AssignmentObject.GetComponent<Smelter>();
                    bool needsOre = smelter.m_maxOre - Traverse.Create(smelter).Method("GetQueueSize").GetValue<int>() > 0;
                    if (needsOre)
                    {
                        foreach (Smelter.ItemConversion itemConversion in smelter.m_conversion)
                        {
                            yield return itemConversion.m_from.m_itemData;
                        }
                    }
                }
            }
        }

        public bool IsClose(Vector3 point)
        {
            return Vector3.Distance(point, Position) < TypeOfAssignment.InteractDist;
        }

        public Assignment(int instanceId, Piece piece)
        {
            BelongsTo = instanceId;
            TypeOfAssignment = GetAssignmentType(piece);
            AssignmentObject = piece.gameObject;
            AssignmentTime = 0;
        }

        private AssignmentType GetAssignmentType(Piece piece)
        {
            return AssignmentTypes.FirstOrDefault(a => a.PieceName == GetPrefabName(piece.name));
        }

        private static string GetPrefabName(string name)
        {
            char[] anyOf = new char[] { '(', ' ' };
            int num = name.IndexOfAny(anyOf);
            string result;
            if (num >= 0)
                result = name.Substring(0, num);
            else
                result = name;
            return result;
        }

        public static IEnumerable<AssignmentType> AssignmentTypes { get; } = new List<AssignmentType>
        {
            new AssignmentType { Name = "Smelter", PieceName = "smelter", ComponentType = typeof(Smelter), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeSmelterInAssignments.Value},
            new AssignmentType { Name = "Kiln", PieceName = "charcoal_kiln", ComponentType = typeof(Smelter), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeKilnInAssignments.Value},
            new AssignmentType { Name = "Fireplace", PieceName = "fire_pit", ComponentType = typeof(Fireplace), InteractDist = 4.0f, Activated = GreylingsConfig.IncludeFireplaceInAssignments.Value},
            new AssignmentType { Name = "StandingWoodTorch", PieceName = "piece_groundtorch_wood", ComponentType = typeof(Fireplace), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeStandingWoodTorchInAssignments.Value},
            new AssignmentType { Name = "StandingIronTorch", PieceName = "piece_groundtorch", ComponentType = typeof(Fireplace), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeStandingIronTorchInAssignments.Value},
            new AssignmentType { Name = "StandingGreenTorch", PieceName = "piece_groundtorch_green", ComponentType = typeof(Fireplace), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeStandingGreenTorchInAssignments.Value},
            new AssignmentType { Name = "WallTorch", PieceName = "piece_walltorch", ComponentType = typeof(Fireplace), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeWallTorchInAssignments.Value},
            new AssignmentType { Name = "Brazier", PieceName = "piece_brazierceiling01", ComponentType = typeof(Fireplace), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeBrazierInAssignments.Value},
            new AssignmentType { Name = "Blastfurnace", PieceName = "blastfurnace", ComponentType = typeof(Smelter), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeBlastfurnaceInAssignments.Value},
            new AssignmentType { Name = "Windmill", PieceName = "windmill", ComponentType = typeof(Smelter), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeWindmillInAssignments.Value},
            new AssignmentType { Name = "Spinningwheel", PieceName = "piece_spinningwheel", ComponentType = typeof(Smelter), InteractDist = 2.5f, Activated = GreylingsConfig.IncludeSpinningwheelInAssignments.Value},
        };
    }

}
