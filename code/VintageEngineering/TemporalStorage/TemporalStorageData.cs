using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.TemporalStorage
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TemporalStorageData
    {
        /// <summary>
        /// Temporal Chest Storage Inventories, indexed by frequency
        /// </summary>
        [ProtoIgnore]
        public Dictionary<int, InventoryGeneric> _chestStorages;
        /// <summary>
        /// Temporal Tank Storage Inventories, indexed by frequency
        /// </summary>
        [ProtoIgnore]
        public Dictionary<int, InventoryGeneric> _tankStorages;

        // These are what actually get Serialized as InventoryGeneric doesn't work
        public Dictionary<int, byte[]> _chestStorageData;
        public Dictionary<int, byte[]> _tankStorageData;

        [ProtoBeforeSerialization]
        private void OnBeforeSerialization()
        {
            _chestStorageData.Clear();
            _tankStorageData.Clear();

            foreach (KeyValuePair<int, InventoryGeneric> pair in _chestStorages)
            {
                TreeAttribute tree = new TreeAttribute();
                pair.Value.ToTreeAttributes(tree);
                _chestStorageData[pair.Key] = tree.ToBytes();
            }

            foreach (KeyValuePair<int, InventoryGeneric> pair in _tankStorages)
            {
                TreeAttribute tree = new TreeAttribute();
                pair.Value.ToTreeAttributes(tree);
                _tankStorageData[pair.Key] = tree.ToBytes();
            }
        }

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

            _chestStorageData = new Dictionary<int, byte[]>();
            _tankStorageData = new Dictionary<int, byte[]>();

            _chestFreqUsers = new Dictionary<int, string>();
            _tankFreqUsers  = new Dictionary<int, string>();

            _chestPositions = new Dictionary<int, List<BlockPos>>();
            _tankPositions  = new Dictionary<int, List<BlockPos>>();
        }

        public void Clean()
        {
            _chestStorages.Clear();
            _tankStorages.Clear();

            _chestStorageData.Clear();
            _tankStorageData.Clear();

            _chestFreqUsers.Clear();
            _tankFreqUsers.Clear();

            _chestPositions.Clear();
            _tankPositions.Clear();
        }
    }
}
