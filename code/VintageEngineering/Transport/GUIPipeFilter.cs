using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class GUIPipeFilter : GuiDialog
    {        
        public override string ToggleKeyCombinationCode => null;

        public override double DrawOrder => 0.2;

        public virtual string DialogTitle => Lang.Get("vinteng:gui-filtersettings");

        protected string _currentSearchText;

        protected bool _canSearchBlocks = true;
        protected bool _canSearchItems = true;
        protected bool _canSearchWildCards = false;

        protected List<IFlatListItem> _filterItems = new List<IFlatListItem>();
        protected List<IFlatListItem> _searchItems = new List<IFlatListItem>();        



        /// <summary>
        /// The Actual filter item we right clicked to edit. Passed in when dialog is created.
        /// </summary>
        protected ItemStack _filterItem;
        
        private double _dialogHeight = 516.0;

        public GUIPipeFilter(ICoreClientAPI capi, ItemStack filterItem) : base(capi)
        {            
            _filterItem = filterItem;
            
        }

        public override void OnKeyPress(KeyEvent args)
        {
            
            if (args.KeyCode == ((int)GlKeys.Delete))
            {
                // Delete was pressed!!
            }
            base.OnKeyPress(args);
        }

        public void SetupDialog()
        {
            // Title Bar Height
            int tbh = 31; 
            double padding = 4;

            bool isBlackList = _filterItem.Attributes.GetBool("isblacklist", false);

            // Parent (main) Dialog
            ElementBounds dialogBounds = ElementBounds.Fixed(439, (int)(_dialogHeight + tbh));
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(padding);
            dialog.BothSizing = ElementSizing.FitToChildren;

            // BlackList option
            ElementBounds blklistInset = ElementBounds.Fixed(7, 7 + tbh, 76, 76);
            ElementBounds blklistText = ElementBounds.Fixed(11, 11 + tbh, 68, 22);
            ElementBounds blklistToggle = ElementBounds.Fixed(30, 40 + tbh, 30, 30);

            // Filter Options
            ElementBounds optionInset = ElementBounds.Fixed(7, 88 + tbh, 76, 192);
            ElementBounds optionBlockText = ElementBounds.Fixed(11, 92 + tbh, 68, 22);
            ElementBounds optionBlockToggle = ElementBounds.Fixed(29, 117 + tbh, 30, 30);
            ElementBounds optionItemText = ElementBounds.Fixed(11, 154 + tbh, 68, 22);
            ElementBounds optionItemToggle = ElementBounds.Fixed(29, 179 + tbh, 30, 30);
            ElementBounds optionWildcardText = ElementBounds.Fixed(11, 219 + tbh, 68, 22);
            ElementBounds optionWildcardToggle = ElementBounds.Fixed(29, 244 + tbh, 30, 30);

            // Set/Saved Filter items            
            ElementBounds filterItemsList = ElementBounds.Fixed(92, 11 + tbh, 337, 265);
            ElementBounds filterItemsClip = filterItemsList.ForkBoundingParent(0, 0, 0, 0);
            ElementBounds filterItemsInset = filterItemsList.FlatCopy().FixedGrow(padding).WithFixedOffset(-padding, -padding); //ElementBounds.Fixed(88, 7 + tbh, 345, 273);
            ElementBounds filterItemsScroll = filterItemsInset.CopyOffsetedSibling(filterItemsList.fixedWidth + 10, 0, 0, 0).WithFixedWidth(20);

            // search bar and button
            ElementBounds searchTextInset = ElementBounds.Fixed(7, 285 + tbh, 353, 34);
            ElementBounds searchTextText = ElementBounds.Fixed(11, 289 + tbh, 64, 26);
            ElementBounds searchTextInput = ElementBounds.Fixed(88, 289 + tbh, 268, 26);
            ElementBounds searchTextButton = ElementBounds.Fixed(365, 285 + tbh, 68, 34);
            ElementBounds searchTextButtonText = searchTextButton.FlatCopy().FixedShrink(padding); //ElementBounds.Fixed(369, 289 + tbh, 60, 26);

            // Save/Cancel Buttons
            ElementBounds cancelButton = ElementBounds.Fixed(7, 436 + tbh, 76, 34);
            ElementBounds cancelText = cancelButton.FlatCopy().FixedShrink(padding);
            ElementBounds saveButton = ElementBounds.Fixed(7, 475 + tbh, 76, 34);
            ElementBounds saveText = saveButton.FlatCopy().FixedShrink(padding);

            // Search Filter Results
            ElementBounds resultsItemsList = ElementBounds.Fixed(92, 328 + tbh, 337, 177);
            ElementBounds resultsItemsClip = resultsItemsList.ForkBoundingParent(0, 0, 0, 0);
            ElementBounds resultsItemsInset = resultsItemsList.FlatCopy().FixedGrow(padding).WithFixedOffset(-padding, -padding); //ElementBounds.Fixed(88, 7 + tbh, 345, 273);
            ElementBounds resultsItemsScroll = resultsItemsInset.CopyOffsetedSibling(resultsItemsList.fixedWidth + 10, 0, 0, 0).WithFixedWidth(20);

            dialog.WithChildren(new ElementBounds[]
            {
                blklistInset,blklistText,blklistToggle,
                optionInset,optionBlockText,optionBlockToggle,optionItemText,optionItemToggle,optionWildcardText,optionWildcardToggle,
                filterItemsList,filterItemsInset,filterItemsScroll,
                searchTextInset,searchTextText,searchTextInput,searchTextButton,searchTextButtonText,
                cancelButton,cancelText,saveButton,saveText,
                resultsItemsList,resultsItemsInset,resultsItemsScroll
            });

            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            CairoFont whitecenter = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            CairoFont whiteright = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);
            CairoFont whiteleft = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);

            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont yellowleft = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);

            this.SingleComposer = capi.Gui.CreateCompo("vepipefiltergui", window)
                .AddShadedDialogBG(dialogBounds, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-filtersettings"), new Action(OnTitleBarClose), null, null)
                .BeginChildElements()

                // Blacklist toggle
                .AddInset(blklistInset, ((int)padding), 0f)
                .AddStaticText(Lang.Get("vinteng:gui-blacklist"), whitecenter, blklistText, "blklisttext")
                .AddSwitch(new Action<bool>(OnBlackListSwitch), blklistToggle, "blklisttoggle", 30, 0)

                // Search Option Panel
                .AddInset(optionInset, ((int)padding), 0f)
                .AddStaticText(Lang.Get("vinteng:gui-blocks"), whitecenter, optionBlockText, "optionblocktext")
                .AddSwitch(new Action<bool>(OnSearchBlockSwitch), optionBlockToggle, "optionblocktoggle", 30, 0)
                .AddStaticText(Lang.Get("vinteng:gui-items"), whitecenter, optionItemText, "optionitemtext")
                .AddSwitch(new Action<bool>(OnSearchItemSwitch), optionItemToggle, "optionitemtoggle", 30, 0)
                .AddStaticText(Lang.Get("vinteng:gui-wildcards"), whitecenter, optionWildcardText, "optionwildcardtext")
                .AddSwitch(new Action<bool>(OnSearchWildcardSwitch), optionWildcardToggle, "optionwildcardtoggle", 30, 0)

                // Saved/Loaded Filter settingss
                .AddInset(filterItemsInset, ((int)padding), 0f)
                .BeginClip(filterItemsClip)
                .AddFlatList(filterItemsList, new Action<int>(OnLeftClickFilterEntry), _filterItems, "filteritemslist")
                .EndClip()
                .AddVerticalScrollbar(new Action<float>(OnFilterItemsScroll), filterItemsScroll, "filteritemsscroll")

                // Search Bar and Button
                .AddInset(searchTextInset, ((int)padding), 0f)
                .AddStaticText(Lang.Get("vinteng:gui-search"), whiteright, searchTextText, "searchtexttext")
                .AddTextInput(searchTextInput, new Action<string>(OnSearchTextChange), whiteleft, "searchtextinput")
                .AddIf(_canSearchWildCards)
                .AddSmallButton("", new ActionConsumable(AddButtonClicked), searchTextButton, EnumButtonStyle.Small, "addbutton")
                .AddStaticText(Lang.Get("vinteng:gui-add"), whitecenter, searchTextButtonText, "searchtextbtntext")
                .EndIf()

                // Cancel and Save Buttons
                .AddSmallButton("", new ActionConsumable(CancelButtonClicked), cancelButton, EnumButtonStyle.Small, "cancelbutton")
                .AddStaticText(Lang.Get("vinteng:gui-cancel"), whitecenter, cancelText, "cancelbtntext")
                .AddSmallButton("", new ActionConsumable(SaveButtonClicked), saveButton, EnumButtonStyle.Small, "savebutton")
                .AddStaticText(Lang.Get("vinteng:gui-save"), whitecenter, saveText, "savebtntext")

                // Search Results Window
                .AddInset(resultsItemsInset, ((int)padding), 0f)
                .BeginClip(resultsItemsClip)
                .AddFlatList(resultsItemsList, new Action<int>(OnLeftClickSearchEntry), _searchItems, "searchresults")
                .EndClip()
                .AddVerticalScrollbar(new Action<float>(OnSearchItemsScroll), resultsItemsScroll, "resultsitemscroll")                

                .EndChildElements()
                .Compose(true);
        }

        private void OnTitleBarClose()
        {
            this.TryClose();
        }

        private void OnBlackListSwitch(bool isenabled)
        {
            _filterItem.Attributes.SetBool("isblacklist", isenabled);
        }

        private void OnSearchBlockSwitch(bool isenabled)
        {
            // TODO
        }
        private void OnSearchItemSwitch(bool isenabled)
        {
            // TODO
        }
        private void OnSearchWildcardSwitch(bool isenabled)
        {
            _canSearchWildCards = isenabled;
            this.SingleComposer.ReCompose();
        }

        private void OnLeftClickFilterEntry(int selection)
        {
            // Item in SavedFilter was clicked
        }
        private void OnLeftClickSearchEntry(int selection)
        {
            // Item in the Search Results was clicked
        }

        private void OnFilterItemsScroll(float value)
        {
            GuiElementFlatList itemlist = this.SingleComposer.GetFlatList("filteritemslist");
            itemlist.insideBounds.fixedY = (double)(3f - value);
            itemlist.insideBounds.CalcWorldBounds();
        }

        private void OnSearchItemsScroll(float value)
        {
            GuiElementFlatList itemlist = this.SingleComposer.GetFlatList("searchresults");
            itemlist.insideBounds.fixedY = (double)(3f - value);
            itemlist.insideBounds.CalcWorldBounds();
        }

        private void OnSearchTextChange(string text)
        {
            // TODO, all the things here
            if (_currentSearchText != text) 
            { 
                _currentSearchText = text;
                FilterItems(); 
            }
        }
        private bool AddButtonClicked()
        {
            throw new NotImplementedException();
        }

        private bool CancelButtonClicked()
        {
            this.TryClose();
            return true;
        }

        private bool SaveButtonClicked()
        {
            throw new NotImplementedException();
        }

        public void FilterItems()
        {
            // Items

            // Blocks

            // Wildcards
        }

        public void PopulateFilterItemList()
        {
            if (_filterItem == null || _filterItem.Attributes == null || _filterItem.Attributes.Count == 0) return;
        }
    }
}
