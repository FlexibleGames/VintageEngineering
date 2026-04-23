using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// A node that extracts from whatever it's connected to.
    /// </summary>
    public class PipeExtractionNode : IBlockEntityContainer 
    {        
        protected ICoreAPI _api;
        protected BlockPos _pos;
        protected string faceCode;
        protected PipeInventory inventory;
        protected long listenerID;
        protected EnumPipeDistribution pipeDistribution = EnumPipeDistribution.Nearest;
        protected bool canFilter = false;
        protected bool canChangeDistro = false;
        protected bool _isSleeping = false;

        protected string _subNet = string.Empty;
        protected List<PipeInsertNode> _insertNodes;

        /// <summary>
        /// Set to true to update Insert Node list on the next tick.
        /// </summary>
        public bool _isDirty = false;
        /// <summary>
        /// So Dirty<br/>Delays the trigger of the insert node rebuild to allow more chunks to load in
        /// </summary>
        private float _dirtyTimer = 0.0f;

        public bool _graphDirty = false;
        private float _graphRebuildTimer = 0.0f;

        public List<PipeInsertNode> _unsortedNodes;

        public bool IsSleeping
        {
            get => _isSleeping; 
            set => _isSleeping = value;
        }
        /// <summary>
        /// List of PipeInsertNode objects for this Extraction Node
        /// </summary>
        public List<PipeInsertNode> InsertNodes => _insertNodes;

        /// <summary>
        /// A special tag for this Extraction Node that only pushes to Insert Nodes of the same SubNet
        /// </summary>
        public string SubNet
        {
            get => _subNet;
            set => _subNet = value;
        }
        /// <summary>
        /// Simple Mark Dirty call with optional flag to rebuild the InsertNode list immediately.
        /// </summary>
        /// <param name="rebuildNodeList">True to rebuild insert node list.</param>
        public void MarkNodeDirty(bool rebuildNodeList)
        {
            _isDirty = true;
            if (rebuildNodeList)
            {
                if (_unsortedNodes != null && _unsortedNodes.Count > 0) _unsortedNodes.Clear();
                _unsortedNodes = BEPipeBaseNew.BuildInsertNodeList(_api.World, _pos);
            }
            else _unsortedNodes?.Clear();
        }

        public void MarkGraphDirty(float timeDelay)
        {
            _graphDirty = true;
            _graphRebuildTimer = timeDelay;
            _isDirty = false;
        }

        /// <summary>
        /// A way to mark the node dirty while providing a node list that can replace the current one or just append to it.<br/>
        /// Nodes are reprocessed into a new list with all distance values set properly.<br/>
        /// Does not actually process the IsDirty trigger later as all the data is already provided in this version.
        /// </summary>
        /// <param name="nodelist">Node List</param>
        /// <param name="appendList">Is the list append or replace?</param>
        public void MarkNodeDirty(List<PipeInsertNode> nodelist, bool appendList = false)
        {            
            List<PipeInsertNode> t_nodes = new List<PipeInsertNode>();
            foreach (PipeInsertNode node in nodelist)
            {
                t_nodes.Add(new PipeInsertNode(node.Position.Copy(), node.Facing, node.SubNet, _pos.ManhattanDistance(node.Position)));
            }
            if (appendList)
            {
                PushEnumerator.Dispose();
                _insertNodes ??= new();
                _insertNodes.AddRange(t_nodes);
                if (PipeDistribution == EnumPipeDistribution.Farthest)
                {
                    _insertNodes = _insertNodes.OrderByDescending(x => x.Distance).ToList();
                }
                else _insertNodes = _insertNodes.OrderBy(x => x.Distance).ToList();                
            }
            else 
            {
                PushEnumerator.Dispose();
                _insertNodes ??= new();
                _insertNodes.Clear();
                _insertNodes.AddRange(t_nodes);
                if (PipeDistribution == EnumPipeDistribution.Farthest)
                {
                    _insertNodes = _insertNodes.OrderByDescending(x => x.Distance).ToList();
                }
                else _insertNodes = _insertNodes.OrderBy(x => x.Distance).ToList();
            }
            _api.World.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(_pos)?.MarkDirty(true);
        }

        private ITransportHandler Handler { get { return _api?.World?.BlockAccessor?.GetBlockEntity<BEPipeBaseNew>(_pos)?.GetHandler(); } }

        private bool _doNetworkTick = true;
        
        /// <summary>
        /// The Enumerator set when Node is in RoundRobin mode.
        /// </summary>
        public List<PipeInsertNode>.Enumerator PushEnumerator;

        /// <summary>
        /// Block Position of this extraction node.
        /// </summary>
        public BlockPos BlockPosition { get { return _pos; } }
        /// <summary>
        /// The Face this Extraction node points out of (north, east, south, etc)
        /// </summary>
        public string FaceCode
        { get { return faceCode; } }

        /// <summary>
        /// ItemSlot for the pipe upgrade
        /// </summary>
        public ItemSlot Upgrade
        { get { return inventory[0]; } }
        /// <summary>
        /// Quick access to the stack-size move rate of the upgrade, to prevent checking the attributes of the itemstack every tick.<br/>
        /// If this is -1, do a whole stack, regardless of stack-size.
        /// </summary>
        private int _upgradeRate = 1;
        /// <summary>
        /// Quick access to the stack-size move rate of the upgrade, to prevent checking the attributes of the itemstack every tick.<br/>
        /// If this is -1, do a whole stack, regardless of stack-size.
        /// </summary>
        public int UpgradeRate => _upgradeRate;
        /// <summary>
        /// Itemslot for the filter
        /// </summary>
        public ItemSlot Filter
        { get { return inventory[1]; } }
        /// <summary>
        /// Distribution mode of this node, round-robin, nearest first, etc.<br/>
        /// Set via GUI when upgrade is installed that enables this feature, defaults to Nearest First.
        /// </summary>        
        public EnumPipeDistribution PipeDistribution { get { return pipeDistribution; } }
        /// <summary>
        /// Does the installed upgrade in this node allow filters to be installed?
        /// </summary>
        public bool CanFilter 
        { 
            get 
            { 
                if (inventory != null && !inventory[0].Empty)
                {
                    ItemPipeUpgrade upg = inventory[0].Itemstack.Collectible as ItemPipeUpgrade;
                    if (upg != null) return upg.CanFilter;
                }
                return false;
            } 
        }
        /// <summary>
        /// Does the currently installed upgrade allow the player to change distribution mode?
        /// </summary>
        public bool CanChangeDistro 
        {  
            get 
            {
                if (inventory != null && !inventory[0].Empty)
                {
                    ItemPipeUpgrade upg = inventory[0].Itemstack.Collectible as ItemPipeUpgrade;
                    if (upg != null) return upg.CanChangeDistro;
                }
                return false;
            }
        }
        /// <summary>
        /// The ID of the Tick listener for this Extraction Node<br/>
        /// ID is provided when registering the tick listener by the game and is used to remove the listener.
        /// </summary>
        public long ListenerID { get => listenerID; set { listenerID = value; } }
        public IInventory Inventory => inventory;
        
        public string InventoryClassName => $"PipeInventory-{faceCode}";
        public void CheckInventoryClearedMidTick()
        {
            // new in 1.21.6 
            // going to ignore until I figure out why it exists.
        }

        public PipeExtractionNode()
        {
            inventory = new PipeInventory(null, 0, null);
            inventory.SlotModified += OnSlotModified;
        }

        public virtual void Initialize(ICoreAPI api, BlockPos pos, string facecode)
        {
            _api = api;
            _pos = pos;
            faceCode = facecode;

            inventory.LateInitialize(
                $"{InventoryClassName}/{_pos.X}/{_pos.Y}/{_pos.Z}",
                api
                );
            inventory.FaceIndex = BlockFacing.FromCode(facecode).Index;

            if (api != null)
            {
                if (api.World.BlockAccessor.GetBlock(pos) is BlockPipeBaseNew && api.Side == EnumAppSide.Server)
                { 
                    ApplyUpgrade();
                }
            }
            if (api is ICoreServerAPI)
            {
                VintageEngineeringMod vem = api.ModLoader.GetModSystem<VintageEngineeringMod>(true);
                _doNetworkTick = vem != null ? vem.CommonConfig.DoPipeTick : false;
                if (!_doNetworkTick)
                {
                    if (vem == null)
                    {
                        api.Logger.Debug("VintEng: Error when initializing PipeExtractionNode, could not find VintageEngineeringMod.");
                    }
                    api.Logger.Debug("VintEng: Pipe Ticking has been disabled by config. Set config value DoPipeTick to true to enable pipe distribution.");
                }
            }
        }
        /// <summary>
        /// Called when the chunk that contains this extraction node is in is unloaded.
        /// </summary>
        public virtual void OnBlockUnloaded(IWorldAccessor world)
        {
            PushEnumerator.Dispose();
            _insertNodes?.Clear();
        }

        public virtual void ResetEnumerator()
        {
            PushEnumerator.Dispose();
            PushEnumerator = _insertNodes.GetEnumerator();
        }

        /// <summary>
        /// Sets the Distribution mode of this extraction node.<br/>
        /// String parameter reflects internal GUI Drop down option values.
        /// </summary>
        /// <param name="distro">Given string from GUI Dropdown option set.</param>
        public void SetDistroMode(string distro)
        {

            switch (distro)
            {
                case "nearest": pipeDistribution = EnumPipeDistribution.Nearest; break;
                case "farthest": pipeDistribution = EnumPipeDistribution.Farthest; break;
                case "robin": pipeDistribution = EnumPipeDistribution.RoundRobin; break;
                case "random": pipeDistribution = EnumPipeDistribution.Random; break;
                default: pipeDistribution = EnumPipeDistribution.Nearest; break;
            }
        }

        /// <summary>
        /// Extraction Node Inventory Slot Modified<br/>
        /// SlotID 0 = PipeUpgrade<br/>
        /// SlotID 1 = PipeFilter
        /// </summary>
        /// <param name="slotid">SlotId modified.</param>
        public virtual void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                ApplyUpgrade();
            }
        }

        public virtual void ApplyUpgrade()
        {
            BEPipeBaseNew bep = _api.World.BlockAccessor.GetBlockEntity(_pos) as BEPipeBaseNew;
            if (bep == null) return; // the BE we're apart of is invalid somehow
            if (listenerID != 0)
            {
                // remove the listener if we have it
                bep.RemoveExtractionTickEvent(ListenerID);
                listenerID = 0;
            }

            if (Upgrade.Empty) // it IS possible for someone to remove an upgrade.
            {
                _upgradeRate = 1;
                if (!Filter.Empty)
                {
                    inventory.DropSlots(this._pos.UpCopy(1).ToVec3d(), new int[] { 1 });
                }
                listenerID = bep.AddExtractionTickEvent(1000, UpdateTick);
                SetDistroMode("nearest");
                canChangeDistro = false;
                canFilter = false;
            }
            else
            {
                ItemPipeUpgrade upgradeitem = (ItemPipeUpgrade)Upgrade.Itemstack.Collectible;
                int msdelay = upgradeitem.Delay;
                canChangeDistro = upgradeitem.CanChangeDistro;
                if (!canChangeDistro) SetDistroMode("nearest");
                canFilter = upgradeitem.CanFilter;
                if (!canFilter && !Filter.Empty) inventory.DropSlots(this._pos.UpCopy(1).ToVec3d(), new int[] { 1 });
                _upgradeRate = upgradeitem.Rate;
                listenerID = bep.AddExtractionTickEvent(msdelay, UpdateTick);
            }
            if (bep.PipeExtractionGUIs != null &&
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index] != null &&
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].IsOpened())
            {
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].Update();
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].Recompose();
            }
            bep.MarkDirty(true);            
        }
        /// <summary>
        /// Called when removing the node, drops any upgrade and filter.
        /// </summary>
        public virtual void OnNodeRemoved()
        {
            inventory.SlotModified -= OnSlotModified;

            // what if this is null?
            PushEnumerator.Dispose();
            DropContents(_pos.ToVec3d());
        }

        /// <summary>
        /// Update Tick for this extraction node.<br/>
        /// Override to control update tick behavior.
        /// </summary>
        /// <param name="deltatime">Time (in seconds) since last update.</param>
        public virtual void UpdateTick(float deltatime)
        {
            if (_isSleeping || Handler == null || _api.Side == EnumAppSide.Client || !_doNetworkTick) return;

            if (_graphDirty)
            {
                _graphRebuildTimer -= deltatime;
                if (_graphRebuildTimer <= 0f)
                {
                    _graphDirty = false;
                    _graphRebuildTimer = 0f;
                    BEPipeBaseNew.RebuildPipeGraph(_api.World, _pos);
                }
            }

            if (_isDirty)
            {
                _dirtyTimer += deltatime;
                if (_dirtyTimer >= 8) // 8 seconds
                {
                    CleanWithSoap();
                    _dirtyTimer = 0f;
                }                
                return;
            }

            Stopwatch ws = Stopwatch.StartNew();
            Handler.TransportTick(deltatime, _pos, _api.World, this);
            ws.Stop();
            if (ws.ElapsedMilliseconds > 100)
            {
                _api.World.Logger.Debug($"Transport Handler Tick Took {ws.ElapsedMilliseconds}ms");
            }
        }

        public virtual void CleanWithSoap()
        {
            if (_api.Side == EnumAppSide.Client) return;

            if (_unsortedNodes == null || _unsortedNodes.Count == 0)
            {
                _unsortedNodes = BEPipeBaseNew.BuildInsertNodeList(_api.World, _pos);
            }
            PushEnumerator.Dispose();
            if (_insertNodes != null && _insertNodes.Count > 0) _insertNodes.Clear();
            if (_unsortedNodes.Count > 0)
            {
                if (PipeDistribution == EnumPipeDistribution.Farthest)
                {
                    _insertNodes = _unsortedNodes.OrderByDescending(x => x.Distance).ToList();
                }
                else _insertNodes = _unsortedNodes.OrderBy(x => x.Distance).ToList();
                if (PipeDistribution == EnumPipeDistribution.RoundRobin)
                {
                    PushEnumerator = _insertNodes.GetEnumerator();
                }
            }
            _unsortedNodes.Clear();
            _isDirty = false;
            _api.World.BlockAccessor.GetBlockEntity<BEPipeBaseNew>(_pos)?.MarkDirty(true);
        }
        /// <summary>
        /// Forces this Extraction Node to rebuild it's Insert connections immediately.
        /// </summary>
        /// <returns>Number of Insert Nodes found.</returns>
        public virtual int RebuildConnectionsNow()
        {
            if (_api.Side == EnumAppSide.Client) return 0;
            if (_unsortedNodes != null && _unsortedNodes.Count > 0)
            {
                _unsortedNodes.Clear();
            }
            CleanWithSoap();
            return _insertNodes?.Count ?? 0;
        }

        /// <summary>
        /// Removes all nodes that match the passed in nodes TARGET position.<br/>
        /// Used when the target of an insert node is broken or no longer exists.
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Number removed</returns>
        public virtual int RemoveInsertNode(PipeInsertNode node)
        {
            if (_api.Side == EnumAppSide.Client) return 0;
            PushEnumerator.Dispose();
            _insertNodes ??= new();
            int numremoved = _insertNodes.RemoveAll(x => x.Position == node.Position);
            return numremoved;
        }
        /// <summary>
        /// Removes all nodes that match the passed in PIPE position.<br/>
        /// Used when breaking a pipe on the end of a pipe system.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>Number removed</returns>
        public virtual int RemoveInsertPosition(BlockPos pos)
        {
            if (_api.Side == EnumAppSide.Client) return 0;
            PushEnumerator.Dispose();
            _insertNodes ??= new();
            int numremoved = _insertNodes.RemoveAll(x => x.NodePosition == pos);
            return numremoved;
        }
        /// <summary>
        /// Remove an Exact Insert node based on the given BlockPos of the node and the faceindex of the connection.<br/>
        /// Used when a player disables or changes a single insert node.
        /// </summary>
        /// <param name="pos">Position OF THE PIPE the insert node occupies.</param>
        /// <param name="faceindex">Faceindex of the connection</param>
        /// <returns>Number removed (should be 1)</returns>
        public virtual int RemoveExactInsertNode(BlockPos pos, int faceindex)
        {
            if (_api.Side == EnumAppSide.Client) return 0;
            PushEnumerator.Dispose();
            _insertNodes ??= new();
            int numremoved = _insertNodes.RemoveAll(x => x.NodePosition == pos && x.Facing.Index == faceindex);
            return numremoved;
        }

        /// <summary>
        /// Player right clicked this ExtractionNode, passed in from the block entity.
        /// </summary>
        /// <param name="player">Player who right clicked</param>
        /// <returns>True if event is handled.</returns>
        public virtual bool OnRightClick(IWorldAccessor world, IPlayer player)
        {
            // auto swap held item in player hotbarslot if valid.
            if (player == null || player.InventoryManager.ActiveHotbarSlot == null || player.InventoryManager.ActiveHotbarSlot.Itemstack == null) return false;
            if (player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible is ItemPipeUpgrade)
            {
                if (Upgrade.Empty)
                {
                    player.InventoryManager.ActiveHotbarSlot.TryPutInto(world, Upgrade, 1);
                }
                else
                {
                    player.InventoryManager.ActiveHotbarSlot.TryFlipWith(Upgrade);
                }
            }
            else if (player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible is ItemPipeFilter)
            {
                if (!CanFilter)
                {
                    return false;
                }
                if (Filter.Empty)
                {
                    player.InventoryManager.ActiveHotbarSlot.TryPutInto(world, Filter, 1);
                }
                else
                {
                    player.InventoryManager.ActiveHotbarSlot.TryFlipWith(Filter);
                }
            }
            else { return false; }
            return true;
        }

        /// <summary>
        /// Drop upgrade and filter for this node.
        /// </summary>
        /// <param name="atPos">Position to drop at.</param>
        public virtual void DropContents(Vec3d atPos)
        {
            try
            {
                if (inventory != null) inventory.DropAll(atPos);
            }
            catch (Exception ex)
            {
                _api.Logger.Error(ex);
            }
        }

        /// <summary>
        /// Converts the important object data into a TreeAttribute for saving and syncing.
        /// </summary>
        /// <param name="tree"></param>
        public virtual void ToTreeAttributes(ITreeAttribute tree)
        {
            TreeAttribute inventorytree = new TreeAttribute();
            inventory.ToTreeAttributes(inventorytree);
            tree["inventory"] = inventorytree;
            tree.SetBlockPos("position", _pos);
            tree.SetString("facecode", faceCode);            
            tree.SetString("distro", pipeDistribution.ToString());
            tree.SetString("subnet", _subNet);
        }
        /// <summary>
        /// Converts a TreeAttribute tree to object data for loading and syncing.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="worldForResolving"></param>
        public virtual void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _pos = tree.GetBlockPos("position");
            faceCode = tree.GetString("facecode", "error");
            pipeDistribution = Enum.Parse<EnumPipeDistribution>(tree.GetString("distro", "Nearest"));
            _subNet = tree.GetString("subnet", string.Empty);
        }
    }
}
