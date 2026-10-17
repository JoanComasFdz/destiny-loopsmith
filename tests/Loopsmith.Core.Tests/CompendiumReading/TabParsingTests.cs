using Loopsmith.Core.CompendiumReading;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Tests.CompendiumReading;

/// <summary>
/// Each tab's parser on a made-up tab of its layout: names, rows, sections and stated numbers. No Compendium text: every
/// name, sentence and number here is invented (README, Licensing).
/// </summary>
public sealed class TabParsingTests
{
    [Fact]
    public void A_csv_cell_may_hold_commas_line_breaks_and_doubled_quotes()
    {
        var rows = CsvReading.ReadRows("a,\"b, c\",\"line one\nline \"\"two\"\"\"\r\n,last\n");

        Assert.Equal(2, rows.Length);
        Assert.Equal(["a", "b, c", "line one\nline \"two\""], rows[0]);
        Assert.Equal(["", "last"], rows[1]);
    }

    [Fact]
    public void A_text_states_its_numbers_with_PvP_values_apart_and_a_percent_also_as_its_fraction()
    {
        var numbers = NumberReading.ReadNumbers("Hits for 1,500 [250] damage, 25% more for 5+2 s at 0.75x; total 900 [300 [350]].", BracketMeaning.PvpValue);

        Assert.Equal([1500m, 25m, 0.25m, 5m, 2m, 0.75m, 900m], numbers.Pve);
        Assert.Contains(250m, numbers.All);
        Assert.Contains(350m, numbers.All);
        Assert.DoesNotContain(250m, numbers.Pve);
    }

    [Fact]
    public void Where_brackets_hold_an_alternative_value_it_holds_in_PvE_too()
    {
        var numbers = NumberReading.ReadNumbers("Fires 4 [5] bolts, up to 7 ↑8 times {9}.", BracketMeaning.AlternativeValue);

        Assert.Equal([4m, 5m, 7m, 8m, 9m], numbers.Pve);
    }

    [Fact]
    public void A_subclass_tab_reads_its_entries_by_section_skipping_class_bands_and_notes()
    {
        var rows = TabRows.ReadTabRows(string.Join("\n",
            "Back to top,,,Arc Subclasses,,,",
            ",Hunter,,,Titan,Warlock,",
            ",Glossary,,,,,",
            ",Testing Buff,,\"Lasts 6 [3] seconds.\",,,",
            ",Grenade Abilities,,,,,",
            ",Numbers in [brackets] are with a made-up fragment,,,,,",
            ",Testing Grenade,,\"Deals 100 [120] damage.\",,,\"Base Cooldown: 90 seconds\n\nChunk Scalar: 0.5x\"",
            ",Touch of Testing,,Adds 2 more.,,,",
            "Titans,-,,,Titans,-,",
            ",Melee Abilities,,,,,",
            ",Testing Punch,,\"Deals 40 [20] damage.\",,,Base Cooldown: 50 seconds"));

        var entries = SubclassTabParsing.ParseSubclassTab("Arc", rows);

        Assert.Equal(["Testing Buff", "Testing Grenade", "Touch of Testing", "Testing Punch"], entries.Select(entry => entry.Name));
        Assert.Equal([4, 7, 8, 11], entries.Select(entry => entry.Rows.Single()));
        Assert.Equal(["Glossary", "Grenade Abilities", "Grenade Abilities", "Melee Abilities"], entries.Select(entry => entry.Section));
        Assert.Contains(120m, entries[1].Numbers.Pve);                                            // Arc's grenade brackets: the alternative value
        Assert.Contains(90m, entries[1].Numbers.Pve);
        Assert.Contains(0.5m, entries[1].Numbers.Pve);
        Assert.DoesNotContain(3m, entries[0].Numbers.Pve);                                         // anywhere else: the PvP value
        Assert.DoesNotContain(20m, entries[3].Numbers.Pve);
    }

    [Fact]
    public void An_armor_mods_row_holds_one_mod_per_name_column_and_a_heading_without_an_effect_is_no_mod()
    {
        var rows = TabRows.ReadTabRows(string.Join("\n",
            ",Helmet,Effect,Arms,Effect,LEGACY,Raid,Effect",
            ",\"Testing Siphon\n[1 Energy]\",\"Two kills spawn an orb within 3 seconds.\",\"Testing\n Kickstart\n[3 Energy]\",\"10% | 15% | 20% [5%]\",,Made-up Raid,",
            ",,,Testing Loader,+20 | +30,,Testing Relic,Lasts 12 seconds."));

        var entries = ArmorModsParsing.ParseArmorModsTab("Armor Mods", rows);

        Assert.Equal(["Testing Siphon", "Testing Kickstart", "Testing Loader", "Testing Relic"], entries.Select(entry => entry.Name));
        Assert.Equal([2, 2, 3, 3], entries.Select(entry => entry.Rows.Single()));
        Assert.Equal(["Helmet", "Arms", "Arms", "Raid"], entries.Select(entry => entry.Section));
        Assert.Equal([10m, 0.1m, 15m, 0.15m, 20m, 0.2m], entries[1].Numbers.Pve);
    }

    [Fact]
    public void An_artifact_perk_spans_its_name_row_and_its_description_row_under_its_artifact()
    {
        var rows = TabRows.ReadTabRows(string.Join("\n",
            "Artifact Perks,,,,,,,",
            ",,,Made-up Tome (Test Season),,,,",
            ",,,Arc | Void,,,,",
            ",,First Perk,,,Second Perk,,Third Perk",
            ",Grants 10% energy.,,,Lasts 5 seconds.,,Spawns 2 pickups.,",
            ",,,Other Tome (Later Season),,,,",
            ",,,Solar,,,,",
            ",,Fourth Perk,,,,,",
            ",Deals 30 [15] damage.,,,,,,"));

        var entries = ArtifactPerksParsing.ParseArtifactPerksTab("Artifact Perks", rows);

        Assert.Equal(["First Perk", "Second Perk", "Third Perk", "Fourth Perk"], entries.Select(entry => entry.Name));
        Assert.Equal([4, 5], entries[0].Rows);
        Assert.Equal(["Made-up Tome (Test Season)", "Made-up Tome (Test Season)", "Made-up Tome (Test Season)", "Other Tome (Later Season)"], entries.Select(entry => entry.Section));
        Assert.Contains(5m, entries[1].Numbers.Pve);
        Assert.DoesNotContain(15m, entries[3].Numbers.Pve);
    }

    [Fact]
    public void A_weapon_perks_row_is_a_perk_under_its_section_and_a_long_lone_text_is_a_note()
    {
        var rows = TabRows.ReadTabRows(string.Join("\n",
            "Weapon Traits,,,,",
            "Testing Trait,Lasts 8 ↑9 seconds.,,,",
            "Weapon Mods,,,,",
            "This long made-up note explains which weapons take the enhanced mods.,,,,",
            "Testing Mag,+30 ↑+40 Magazine.,,,"));

        var entries = WeaponPerksParsing.ParseWeaponPerksTab("Weapon Perks", rows);

        Assert.Equal(["Testing Trait", "Testing Mag"], entries.Select(entry => entry.Name));
        Assert.Equal(["Weapon Traits", "Weapon Mods"], entries.Select(entry => entry.Section));
        Assert.Equal([8m, 9m], entries[0].Numbers.Pve);
        Assert.Equal([30m, 40m], entries[1].Numbers.Pve);
    }

    [Fact]
    public void A_snapshot_reads_the_tabs_it_has_a_parser_for_lists_the_others_and_never_an_old_tab()
    {
        var snapshot = SnapshotReading.ReadSnapshot([
            new SourceText("06_Arc.csv", ",Fragments,,,\n,Testing,,Lasts 3 seconds.,"),
            new SourceText("16_Game_Mechanics.csv", "anything"),
            new SourceText("20_OLD_Armor_Mods.csv", ",Helmet,Effect\n,Old Mod,Old effect 1."),
            new SourceText("INDEX.md", "not a tab")]);

        Assert.Equal(["Arc"], snapshot.ReadTabs);
        Assert.Equal(["Game Mechanics"], snapshot.UnreadTabs);
        Assert.Equal(["Testing"], snapshot.Entries.Select(entry => entry.Name));
    }
}
