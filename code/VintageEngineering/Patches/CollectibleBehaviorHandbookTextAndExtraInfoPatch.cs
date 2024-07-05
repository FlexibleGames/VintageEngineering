using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering;

// Disable warnings about addCreatedByInfoPrefix not starting with a capital letter.
#pragma warning disable IDE1006

/// <summary>
/// Publicly exposes some formatting methods from <see cref="CollectibleBehaviorHandbookTextAndExtraInfo/>
/// </summary>
public class CollectibleBehaviorHandbookTextAndExtraInfoAccessor : CollectibleBehaviorHandbookTextAndExtraInfo
{
    public CollectibleBehaviorHandbookTextAndExtraInfoAccessor() : base(null)
    {
    }

    public static new void AddHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, string heading, ref bool haveText)
    {
        CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, heading, ref haveText);
    }

    public new void AddSubHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, string subheading, string detailpage)
    {
        base.AddSubHeading(components, capi, openDetailPageFor, subheading, detailpage);
    }

    public new const int SmallPadding = CollectibleBehaviorHandbookTextAndExtraInfo.SmallPadding;
    public new const int TinyPadding = CollectibleBehaviorHandbookTextAndExtraInfo.TinyPadding;
    public new const int MarginBottom = CollectibleBehaviorHandbookTextAndExtraInfo.MarginBottom;
}

[HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo))]
class CollectibleBehaviorHandbookTextAndExtraInfoPatch
{
    static readonly CollectibleBehaviorHandbookTextAndExtraInfoAccessor accessor = new();

    public const int SmallPadding = CollectibleBehaviorHandbookTextAndExtraInfoAccessor.SmallPadding;
    public const int TinyPadding = CollectibleBehaviorHandbookTextAndExtraInfoAccessor.TinyPadding;
    public const int MarginBottom = CollectibleBehaviorHandbookTextAndExtraInfoAccessor.MarginBottom;

    public static void AddHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, string heading, ref bool haveText)
    {
        CollectibleBehaviorHandbookTextAndExtraInfoAccessor.AddHeading(components, capi, heading, ref haveText);
    }

    public static void AddSubHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, string subheading, string detailpage)
    {
        accessor.AddSubHeading(components, capi, openDetailPageFor, subheading, detailpage);
    }

    /// <summary>
    /// Allow adding more components to the handbook page before the "Created by" section.
    /// </summary>
    /// <param name="stack">Item that the page is being generated for.</param>
    /// <param name="components">The GUI elements added to the page so far. Put any new elements at the end of this list.</param>
    /// <param name="haveText">True if a margin should be added before the next element. Generally this should be set to true after anything is added to <paramref name="components"/></param>
    public delegate void AddProcessesIntoInfoDelegate(ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, ref bool haveText);

    /// <summary>
    /// Allow adding more components to the handbook page inside the "Created by" section.
    /// </summary>
    /// <param name="allStacks">Array of all known items and blocks. This is useful for searching for checking attributes on all items to see if they somehow transform into <paramref name="stack"/></param>
    /// <param name="stack">Item that the page is being generated for.</param>
    /// <param name="components">The GUI elements added by delegates to the created by section so far. If this is null, then initialize it to a non-null list before adding elements. Otherwise, add new elements to the existing list.</param>
    public delegate void AddCreatedByInfoDelegate(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, ref List<RichTextComponentBase> components);

    public static AddProcessesIntoInfoDelegate ProcessesInto;
    public static AddCreatedByInfoDelegate CreatedBy;

    [HarmonyPrefix]
    [HarmonyPatch("addCreatedByInfo")]
    static void addCreatedByInfoPrefix(out int __state, ICoreClientAPI capi,
                                       ItemStack[] allStacks,
                                       ActionConsumable<string> openDetailPageFor,
                                       ItemStack stack,
                                       List<RichTextComponentBase> components,
                                       float marginTop, ref bool haveText)
    {
        ProcessesInto?.Invoke(capi, openDetailPageFor, stack, components, ref haveText);
        // Remember how many elements are in the components list before invoking the original method.
        __state = components.Count;
    }

    [HarmonyPostfix]
    [HarmonyPatch("addCreatedByInfo")]
    static void addCreatedByInfoPostfix(
        int __state, ref bool __result, ICoreClientAPI capi,
        ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor,
        ItemStack stack, List<RichTextComponentBase> components, float marginTop,
        bool haveText)
    {
        List<RichTextComponentBase> delegateAdded = null;
        CreatedBy?.Invoke(capi, allStacks, openDetailPageFor, stack, ref delegateAdded);
        if ((delegateAdded?.Count ?? 0) != 0)
        {
            // __state contains the number of components in the list at the end of the prefix method. If the number of
            // elements is the same, then the original method did not add a "Created by" heading.
            if (components.Count == __state)
            {
                AddHeading(components, capi, "Created by", ref haveText);
                components.Add(new ClearFloatTextComponent(capi, TinyPadding + 1));
            }
            else
            {
                components.Add(new ClearFloatTextComponent(capi, SmallPadding));
            }
            components.AddRange(delegateAdded);
            // Now items were definitely added to <paramref name="components"/>
            __result = true;
        }
    }
}

#pragma warning restore IDE1006
