using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class GUIPipeFilter : GuiDialogGeneric
    {        
        public override string ToggleKeyCombinationCode => null;

        public override double DrawOrder => 0.2;

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

        public GUIPipeFilter(ICoreClientAPI capi, ItemStack filterItem) : base(Lang.Get("vinteng:gui-filtersettings"), capi)
        {            
            _filterItem = filterItem;
            PopulateFilterItemList();
            SetupDialog();
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
            ElementBounds filterItemsInset = ElementBounds.Fixed(88, 7 + tbh, 345, 273);//filterItemsList.FlatCopy().FixedGrow(padding+padding).WithFixedOffset(-padding, -padding); //ElementBounds.Fixed(88, 7 + tbh, 345, 273);
            ElementBounds filterItemsList = ElementBounds.Fixed(92, 12 + tbh, 317, 265);
            ElementBounds filterItemsClip = ElementBounds.Fixed(93, 13 + tbh, 315, 263);  //filterItemsList.ForkBoundingParent(0, 0, 0, 0);             
            ElementBounds filterItemsScroll = ElementStdBounds.VerticalScrollbar(filterItemsList); //ElementBounds.Fixed(409, 12 + tbh, 20, 265); //filterItemsInset.CopyOffsetedSibling(filterItemsList.fixedWidth + 10, 0, 0, 0).WithFixedWidth(20);

            // search bar and button
            ElementBounds searchTextInset = ElementBounds.Fixed(7, 285 + tbh, 353, 34);
            ElementBounds searchTextText = ElementBounds.Fixed(11, 289 + tbh, 64, 26);
            ElementBounds searchTextInput = ElementBounds.Fixed(88, 289 + tbh, 268, 26);
            ElementBounds searchTextButton = ElementStdBounds.ToggleButton(365, 285 + tbh, 68, 34);
            ElementBounds searchTextButtonText = ElementBounds.Fixed(369, 289 + tbh, 60, 26);// searchTextButton.FlatCopy().FixedShrink(padding); //ElementBounds.Fixed(369, 289 + tbh, 60, 26);

            // Save/Cancel Buttons
            ElementBounds cancelButton = ElementStdBounds.ToggleButton(7, 436 + tbh, 76, 34);
            ElementBounds cancelText = ElementBounds.Fixed(11, 440 + tbh, 68, 26); // cancelButton.FlatCopy().FixedShrink(padding);
            ElementBounds saveButton = ElementStdBounds.ToggleButton(7, 475 + tbh, 76, 34);
            ElementBounds saveText = ElementBounds.Fixed(11, 479 + tbh, 68, 26); // saveButton.FlatCopy().FixedShrink(padding);

            // Search Filter Results
            ElementBounds resultsItemsInset = ElementBounds.Fixed(88, 324 + tbh, 345, 185); //resultsItemsList.FlatCopy().FixedGrow(padding+padding).WithFixedOffset(-padding, -padding); //ElementBounds.Fixed(88, 7 + tbh, 345, 273);
            ElementBounds resultsItemsList = ElementBounds.Fixed(92, 328 + tbh, 317, 177);
            ElementBounds resultsItemsClip = ElementBounds.Fixed(93, 329 + tbh, 315, 175); //resultsItemsList.ForkBoundingParent(0, 0, 0, 0);            
            ElementBounds resultsItemsScroll = ElementStdBounds.VerticalScrollbar(resultsItemsList); // ElementBounds.Fixed(409, 328 + tbh, 20, 177); //resultsItemsInset.CopyOffsetedSibling(resultsItemsList.fixedWidth + 10, 0, 0, 0).WithFixedWidth(20);

            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                blklistInset,blklistText,blklistToggle,
                optionInset,optionBlockText,optionBlockToggle,optionItemText,optionItemToggle,optionWildcardText,optionWildcardToggle,
                filterItemsList,filterItemsInset,filterItemsScroll,
                searchTextInset,searchTextText,searchTextInput,searchTextButton,
                cancelButton,saveButton,
                resultsItemsList,resultsItemsInset,resultsItemsScroll
            });

            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            CairoFont whitebigcenter = CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center).WithColor(new double[3] { 1,1,1 });
            CairoFont whitecenter = CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Center);
            CairoFont whiteright = CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Right);
            CairoFont whiteleft = CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Left);

            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont yellowleft = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);

            this.SingleComposer = capi.Gui.CreateCompo("vepipefiltergui", window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-filtersettings"), new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog);

            // Blacklist toggle
            this.SingleComposer.AddInset(blklistInset, ((int)padding), 0.6f)
                .AddStaticText(Lang.Get("vinteng:gui-blacklist"), whitecenter, blklistText, "blklisttext")
                .AddSwitch(new Action<bool>(OnBlackListSwitch), blklistToggle, "blklisttoggle", 30, 0);

            //Search Option Panel
            this.SingleComposer.AddInset(optionInset, ((int)padding), 0.6f)
                .AddStaticText(Lang.Get("vinteng:gui-blocks"), whitecenter, optionBlockText, "optionblocktext")
                .AddSwitch(new Action<bool>(OnSearchBlockSwitch), optionBlockToggle, "optionblocktoggle", 30, 0)
                .AddStaticText(Lang.Get("vinteng:gui-items"), whitecenter, optionItemText, "optionitemtext")
                .AddSwitch(new Action<bool>(OnSearchItemSwitch), optionItemToggle, "optionitemtoggle", 30, 0)
                .AddStaticText(Lang.Get("vinteng:gui-wildcards"), whitecenter, optionWildcardText, "optionwildcardtext")
                .AddSwitch(new Action<bool>(OnSearchWildcardSwitch), optionWildcardToggle, "optionwildcardtoggle", 30, 0);

            //Saved / Loaded Filter settings
            this.SingleComposer.AddInset(filterItemsInset, ((int)padding), 0f)
                .BeginClip(filterItemsClip)
                .AddFlatList(filterItemsList, new Action<int>(OnLeftClickFilterEntry), _filterItems, "filteritemslist")
                .EndClip()
                .AddVerticalScrollbar(new Action<float>(OnFilterItemsScroll), filterItemsScroll, "filteritemsscroll");

            // Search Bar and Button
            this.SingleComposer.AddInset(searchTextInset, ((int)padding), 0.6f)
                .AddStaticText(Lang.Get("vinteng:gui-search") + ":", whiteright, searchTextText, "searchtexttext")
                .AddTextInput(searchTextInput, new Action<string>(OnSearchTextChange), whiteleft, "searchtextinput")
                .AddIf(_canSearchWildCards)
                .AddSmallButton(Lang.Get("vinteng:gui-add"), new ActionConsumable(AddButtonClicked), searchTextButton, EnumButtonStyle.Normal, "addbutton")
                //.AddStaticText(Lang.Get("vinteng:gui-add"), whitebigcenter, searchTextButtonText, "searchtextbtntext")
                .EndIf();

            // Cancel and Save Buttons
            this.SingleComposer.AddSmallButton(Lang.Get("vinteng:gui-cancel"), new ActionConsumable(CancelButtonClicked), cancelButton, EnumButtonStyle.Normal, "cancelbutton")
                //.AddStaticText(Lang.Get("vinteng:gui-cancel"), whitebigcenter, cancelText, "cancelbtntext")
                .AddSmallButton(Lang.Get("vinteng:gui-save"), new ActionConsumable(SaveButtonClicked), saveButton, EnumButtonStyle.Normal, "savebutton");
                // .AddStaticText(Lang.Get("vinteng:gui-save"), whitebigcenter, saveText, "savebtntext");

            // Search Results Window
            this.SingleComposer.AddInset(resultsItemsInset, ((int)padding), 0f)
                .BeginClip(resultsItemsClip)
                .AddFlatList(resultsItemsList, new Action<int>(OnLeftClickSearchEntry), _searchItems, "searchresults")
                .EndClip()
                .AddVerticalScrollbar(new Action<float>(OnSearchItemsScroll), resultsItemsScroll, "resultsitemscroll");

            this.SingleComposer.EndChildElements();
            try 
            { 
                this.SingleComposer.Compose(true);
                SetSwitches();
            }
            catch(Exception c)
            {
                capi.SendChatMessage(c.ToString());
            }
        }

        private void SetSwitches()
        {
            if (SingleComposer == null) return;

            SingleComposer.GetSwitch("blklisttoggle").On = _filterItem.Attributes.GetBool("isblacklist");


            SingleComposer.GetSwitch("optionblocktoggle").On = _canSearchBlocks;
            SingleComposer.GetSwitch("optionitemtoggle").On = _canSearchItems;
            SingleComposer.GetSwitch("optionwildcardtoggle").On = _canSearchWildCards;
        }

        private void OnTitleBarClose()
        {
            this.TryClose();
        }

        private void OnBlackListSwitch(bool isenabled)
        {
            _filterItem.Attributes.SetBool("isblacklist", isenabled);
            //capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipefilterdlg");
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
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipefilterdlg");
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
