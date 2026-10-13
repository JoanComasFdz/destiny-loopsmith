using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.ManifestJoining;

/// <summary>
/// The copy of Bungie's manifest the browser keeps between visits (ADRs D25): it is read instead of a download while the
/// manifest index names its version (or doesn't answer), and the listing saved with it is keyed by that version and the
/// code that made it.
/// </summary>
public sealed class ManifestSavingTests
{
    private static string WriteIndex(string version) =>
        """{ "Response": { "version": "1", "jsonWorldComponentContentPaths": { "en": { """
        + string.Join(", ", ManifestTransport.Components.Select(name => $"\"{name}\": \"/common/destiny2_content/json/en/{name}-{version}.json\""))
        + " } } } }";

    private static string WriteSaved(string version, long bytes) =>
        """{ "urls": { """
        + string.Join(", ", ManifestTransport.Components.Select(name => $"\"{name}\": \"https://www.bungie.net/common/destiny2_content/json/en/{name}-{version}.json\""))
        + $" }}, \"bytes\": {bytes} }}";

    [Fact]
    public void The_saved_copy_is_read_while_the_index_names_its_version()
    {
        var saved = LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v1", 199_000_000)));

        var source = LoopDesigning.ChooseManifestSource(Optional.Some(WriteIndex("v1")), "pinned", saved);

        var fromSaved = Assert.IsType<ManifestSource.Saved>(source);
        Assert.Equal(Optional.Some(199_000_000L), fromSaved.Copy.Bytes);
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemDefinition-v1.json", fromSaved.Copy.Urls["DestinyInventoryItemDefinition"]);
        Assert.Equal(fromSaved.Copy.Urls["DestinyInventoryItemDefinition"], fromSaved.Fallback["DestinyInventoryItemDefinition"]); // unreadable: download the same version
    }

    [Fact]
    public void A_new_version_is_downloaded_in_place_of_the_saved_copy()
    {
        var saved = LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v1", 1)));

        var source = LoopDesigning.ChooseManifestSource(Optional.Some(WriteIndex("v2")), "pinned", saved);

        var download = Assert.IsType<ManifestSource.Download>(source);
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemDefinition-v2.json", download.Urls["DestinyInventoryItemDefinition"]);
    }

    [Fact]
    public void Without_an_index_the_saved_copy_is_read_else_the_pinned_version_downloaded()
    {
        var saved = LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v1", 1)));

        var withCopy = LoopDesigning.ChooseManifestSource(Optional.None<string>(), "pinned", saved);
        var brokenIndex = LoopDesigning.ChooseManifestSource(Optional.Some("<html>500</html>"), "pinned", saved);
        var withoutCopy = LoopDesigning.ChooseManifestSource(Optional.None<string>(), "pinned", Optional.None<SavedManifest>());

        var fromSaved = Assert.IsType<ManifestSource.Saved>(withCopy);
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemDefinition-pinned.json", fromSaved.Fallback["DestinyInventoryItemDefinition"]);
        Assert.IsType<ManifestSource.Saved>(brokenIndex);
        var download = Assert.IsType<ManifestSource.Download>(withoutCopy);
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemDefinition-pinned.json", download.Urls["DestinyInventoryItemDefinition"]);
    }

    [Fact]
    public void A_saved_copy_that_lacks_a_component_or_doesnt_read_is_none()
    {
        var lacking = WriteSaved("v1", 1).Replace("\"DestinyPlugSetDefinition\"", "\"Other\"", StringComparison.Ordinal);

        Assert.False(LoopDesigning.ReadSavedManifest(Optional.Some(lacking)).IsSome());
        Assert.False(LoopDesigning.ReadSavedManifest(Optional.Some("not json")).IsSome());
        Assert.False(LoopDesigning.ReadSavedManifest(Optional.None<string>()).IsSome());
        var withoutSize = Assert.IsType<Optional<SavedManifest>.Some>(LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v1", -5))));
        Assert.False(withoutSize.Value.Bytes.IsSome());                                          // a size that doesn't read: unknown
    }

    [Fact]
    public void A_listing_is_saved_under_its_manifest_version_and_the_code_that_made_it()
    {
        var v1 = Assert.IsType<ManifestSource.Saved>(LoopDesigning.ChooseManifestSource(Optional.None<string>(), "p", LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v1", 1))))).Copy.Urls;
        var v2 = Assert.IsType<ManifestSource.Saved>(LoopDesigning.ChooseManifestSource(Optional.None<string>(), "p", LoopDesigning.ReadSavedManifest(Optional.Some(WriteSaved("v2", 1))))).Copy.Urls;

        Assert.Equal(LoopDesigning.ToListingKey(v1, "code-a"), LoopDesigning.ToListingKey(v1, "code-a"));
        Assert.NotEqual(LoopDesigning.ToListingKey(v1, "code-a"), LoopDesigning.ToListingKey(v2, "code-a")); // a new manifest version
        Assert.NotEqual(LoopDesigning.ToListingKey(v1, "code-a"), LoopDesigning.ToListingKey(v1, "code-b")); // a new Loopsmith
    }

    [Fact]
    public void A_saved_listing_reads_back_as_it_was_and_one_that_doesnt_read_is_none()
    {
        var weapon = new ManifestItem(
            ItemHash.From(100u),
            "Zeta Rifle",
            new ManifestKind.Weapon(WeaponSlot.Energy, Optional.Some(DamageType.Arc), [new TraitColumn([ItemHash.From(7u), ItemHash.From(8u)])], Optional.Some(ItemHash.From(20u)), Optional.None<ItemHash>(), Optional.Some(ItemHash.From(485622768u))),
            Optional.Some("Pulse Rifle"),
            Optional.Some(ItemTier.Legendary),
            Optional.None<string>(),
            Optional.Some("/rifle.jpg"),
            Optional.Some("/season.png"));
        var armor = new ManifestItem(
            ItemHash.From(200u),
            "Test Mask",
            new ManifestKind.Armor(ArmorSlot.Helmet, Optional.None<ItemHash>(), Optional.Some(ItemHash.From(500u)), Optional.Some(GuardianClass.Hunter)),
            Optional.Some("Helmet"),
            Optional.Some(ItemTier.Exotic),
            Optional.None<string>(),
            Optional.None<string>(),
            Optional.None<string>());
        var set = new ArmorSet(ItemHash.From(500u), "Testing", [new SetPerk(PieceCount.From(2), ItemHash.From(600u))]);
        var mod = new ListedMod(
            new ManifestItem(ItemHash.From(700u), "Arc Siphon", new ManifestKind.ArmorMod(Optional.Some(ArmorSlot.Helmet)), Optional.Some("Helmet Armor Mod"), Optional.Some(ItemTier.Common),
                Optional.Some("Rapid Arc final blows create an Orb of Power."), Optional.Some("/siphon.png"), Optional.None<string>()),
            PlugCategoryId.From("enhancements.v2_head"),
            ArmorEnergy.From(1),
            ["Must Be Selected in the Seasonal Artifact"]);
        var listing = new ItemListing(
            [weapon, armor],
            ImmutableDictionary<ItemHash, ArmorSet>.Empty.Add(set.Hash, set),
            [mod],
            ImmutableDictionary<ItemHash, ImmutableArray<PlugCategoryId>>.Empty.Add(armor.Hash, [ArmorModCategories.General, PlugCategoryId.From("enhancements.v2_head")]));

        var text = LoopDesigning.ToSavedListing(listing);
        var read = Assert.IsType<Optional<ItemListing>.Some>(LoopDesigning.ReadSavedListing(Optional.Some(text))).Value;

        Assert.Equal(text, LoopDesigning.ToSavedListing(read));
        Assert.Equal(weapon with { Kind = read.Items[0].Kind }, read.Items[0]);
        var readWeapon = Assert.IsType<ManifestKind.Weapon>(read.Items[0].Kind);
        Assert.Equal([7u, 8u], readWeapon.Traits.Single().Options.Select(hash => hash.Value));
        Assert.Equal((WeaponSlot.Energy, Optional.Some(DamageType.Arc), Optional.Some(ItemHash.From(20u)), Optional.Some(ItemHash.From(485622768u))), (readWeapon.Slot, readWeapon.DamageType, readWeapon.Intrinsic, readWeapon.Breaker));
        Assert.Equal(armor.Kind, read.Items[1].Kind);
        Assert.Equal("Testing", read.Sets[ItemHash.From(500u)].Name);
        Assert.Equal(PieceCount.From(2), read.Sets[ItemHash.From(500u)].Perks.Single().Pieces);
        var readMod = read.Mods.Single();
        Assert.Equal((mod.Item, mod.Category, mod.Cost), (readMod.Item, readMod.Category, readMod.Cost));
        Assert.Equal(mod.Requirements, readMod.Requirements);
        Assert.Equal(["enhancements.v2_general", "enhancements.v2_head"], read.ModSockets[armor.Hash].Select(category => category.Value));
        Assert.False(LoopDesigning.ReadSavedListing(Optional.Some("{ \"items\": 3 }")).IsSome());
        Assert.False(LoopDesigning.ReadSavedListing(Optional.Some("not json")).IsSome());
    }
}
