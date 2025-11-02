
namespace VintageEngineering.Electrical
{
    public class ElectricalNetworkConfig
    {
        public ulong NetworkPPS_LV { get; set; } = 500;
        public ulong NetworkPPS_MV { get; set; } = 4000;
        public ulong NetworkPPS_HV { get; set; } = 200000;
        public ulong NetworkPPS_EV { get; set; } = ulong.MaxValue;
        public ElectricalNetworkConfig()
        {
        }

        public ElectricalNetworkConfig(ElectricalNetworkConfig oldConfig)
        {
            NetworkPPS_LV = oldConfig.NetworkPPS_LV;
            NetworkPPS_MV = oldConfig.NetworkPPS_MV;
            NetworkPPS_HV = oldConfig.NetworkPPS_HV;
            NetworkPPS_EV = oldConfig.NetworkPPS_EV;
        }
    }
}
