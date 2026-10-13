using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.TemporalStorage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TemporalStorageData
    {
        /// <summary>
        /// Temporal Chest Storage Inventories, indexed by frequency
        /// </summary>
        public Dictionary<int, InventoryGeneric> _chestStorages;
        /// <summary>
        /// Temporal Tank Storage Inventories, indexed by frequency
        /// </summary>
        public Dictionary<int, InventoryGeneric> _tankStorages;
        /// <summary>
        /// Temporal Chest Frequency User, indexed by frequency<br/>
        /// String Value is playerUID who first set that frequency
        /// </summary>
        public Dictionary<int, string> _chestFreqUsers;
        /// <summary>
        /// Temporal Tank Frequency User, indexed by frequency<br/>
        /// String Value is playerUID who first set that frequency
        /// </summary>
        public Dictionary<int, string> _tankFreqUsers;
        /// <summary>
        /// Temporal Chest Block Positions List indexed by frequency<br/>
        /// List is BlockPos
        /// </summary>
        public Dictionary<int, List<BlockPos>> _chestPositions;
        /// <summary>
        /// Temporal Tank Block Positions List indexed by frequency<br/>
        /// List is BlockPos
        /// </summary>
        public Dictionary<int, List<BlockPos>> _tankPositions;

        public TemporalStorageData()
        {
            _chestStorages = new Dictionary<int, InventoryGeneric>();
            _tankStorages  = new Dictionary<int, InventoryGeneric>();

            _chestFreqUsers = new Dictionary<int, string>();
            _tankFreqUsers  = new Dictionary<int, string>();

            _chestPositions = new Dictionary<int, List<BlockPos>>();
            _tankPositions  = new Dictionary<int, List<BlockPos>>();
        }

        public void Clean()
        {
            _chestStorages.Clear();
            _tankStorages.Clear();

            _chestFreqUsers.Clear();
            _tankFreqUsers.Clear();

            _chestPositions.Clear();
            _tankPositions.Clear();
        }
    }
}
