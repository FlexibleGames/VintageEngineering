using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace VintageEngineering.Electrical.Systems
{
    public class ElectricalNetworkManager
    {
        public Dictionary<long, IElectricNetwork> networks = new Dictionary<long, IElectricNetwork>();
        private long nextNetworkID;

        private ICoreServerAPI sapi;
        private ElectricalNetworkMod mod;

        public ElectricalNetworkManager(ICoreServerAPI _api, ElectricalNetworkMod _mod)
        {
            sapi = _api;
            this.mod = _mod;
        }

        public ElectricNetwork CreateNetwork(IElectricNode node)
        {
            ElectricNetwork network = new ElectricNetwork(mod, nextNetworkID);
            network.AddNode(node);
            networks[nextNetworkID] = network;

            return network;
            
        }
    }
}
