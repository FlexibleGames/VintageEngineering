
using System.Text.Json.Serialization;

namespace VintageEngineering
{
    /// <summary>
    /// Config file for mod control on the server.
    /// </summary>
    public class VintEngCommonConfig
    {
        /// <summary>
        /// Enable/Disable Surface Spout of Oil
        /// </summary>
        public bool OilGyser_GenSpout { get; set; } = true;
        /// <summary>
        /// Enable/Disable Surface Pool of Oil
        /// </summary>
        public bool OilGyser_GenPool { get; set; } = true;        
        /// <summary>
        /// Enable/Disable underground Bubble of Oil
        /// </summary>        
        public bool OilGyser_GenBubble { get; set; } = true;
        /// <summary>
        /// Enable/Disable underground veins of Oil
        /// </summary>
        public bool OilGyser_GenOilDeposit { get; set; } = true;        
        /// <summary>
        /// Enable/Disable Electric Network power distribution.
        /// </summary>
        public bool DoPowerTick { get; set; } = true;
        /// <summary>
        /// Enable/Disable Pipe Distribution tick
        /// </summary>
        public bool DoPipeTick { get; set; } = true;

        public VintEngCommonConfig()
        {
        }

        public VintEngCommonConfig(VintEngCommonConfig oldConfig)
        {
            OilGyser_GenSpout = oldConfig.OilGyser_GenSpout;
            OilGyser_GenPool = oldConfig.OilGyser_GenPool;
            OilGyser_GenBubble = oldConfig.OilGyser_GenBubble;
            OilGyser_GenOilDeposit = oldConfig.OilGyser_GenOilDeposit;
            DoPowerTick = oldConfig.DoPowerTick;
            DoPipeTick = oldConfig.DoPipeTick;
        }
    }
}
