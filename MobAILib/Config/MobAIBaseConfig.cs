using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public class MobAIBaseConfig
    {
        /// <summary>
        /// General awareness, used to calculate search ranges and ability to detect enemies
        /// </summary>
        public int Awareness { get; set; } = 5;

        /// <summary>
        /// Agressivness determines how to behave when fighting and when to give up and flee
        /// </summary>
        public int Agressiveness { get; set; } = 5;

        /// <summary>
        /// Mobility is used to determine how often and how far the mob moves
        /// </summary>
        public int Mobility { get; set; } = 5;

        /// <summary>
        /// General intelligence, how much the mob can remember
        /// </summary>
        public int Intelligence { get; set; } = 5;

        public Dictionary<string,string> AIStateCustomStrings { get; set; }
    }
}
