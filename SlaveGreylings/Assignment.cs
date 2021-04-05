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
        public int BelongsTo { get; set; }
        public MonoBehaviour AssignmentObject { get; set; }
        public AssignmentType TypeOfAssignment { get; set; }

        public static IEnumerable<AssignmentType> AssignmentTypes { get; } = new List<AssignmentType>
        {  
            new AssignmentType { Name = "Smelter", PieceName = "smelter", ComponentType = typeof(Smelter) },
            new AssignmentType { Name = "Kiln", PieceName = "charcoal_kiln", ComponentType = typeof(Smelter)},
            new AssignmentType { Name = "Fireplace", PieceName = "fire_pit", ComponentType = typeof(Fireplace)},
            new AssignmentType { Name = "StandingWoodTorch", PieceName = "piece_groundtorch_wood", ComponentType = typeof(Fireplace)}
        };
    }

}
