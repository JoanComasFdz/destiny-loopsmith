#!/usr/bin/env python3
"""Writes rules/manifest.yaml: Loopsmith's excerpt of the Bungie manifest (docs/rule-format.md, "Manifest").

    python3 -I tools/manifest/extract_manifest.py <manifest-dir> <version> [<repo-root>]

<manifest-dir> holds the English JSON components get-manifest.sh downloads:
DestinyInventoryItemDefinition.json, DestinyInventoryBucketDefinition.json, DestinyDamageTypeDefinition.json,
DestinySocketTypeDefinition.json, DestinySocketCategoryDefinition.json, DestinyPlugSetDefinition.json,
DestinySandboxPerkDefinition.json, DestinyEquipableItemSetDefinition.json, DestinyBreakerTypeDefinition.json,
DestinyInventoryItemConstantsDefinition.json, DestinyTraitDefinition.json and DestinyStatDefinition.json. <version> is the manifest's version
string (written into the file).

The excerpt holds every hash Loopsmith names: each `hash:` in rules/ (elements, the glossary's subclasses,
armor set perks) and every hash in the saved DIM shares (builds/*/dim-loadout.json). For each: its name,
kind, type, icon, tier, Bungie's description and, for weapons, armor and armor mods, its slot and damage
type, and a weapon's or armor piece's season watermark (`watermark`: the overlay with its season's or expansion's
icon in its top-left corner). A weapon also gets its breaker (`breaker`: the champion it breaks of its own, an
exotic's, else the one its intrinsic frame gives it — a frame's perk named with the breaker, "[Shield-Piercing]
Barrier" on Adaptive Frame, as DIM reads it), its trait
columns (`traits`: the "frames" sockets among its weapon perks), each the list of perks it can roll (one, for
a column that doesn't roll: an exotic's), its intrinsic perk and, for an exotic, its catalyst; armor gets its
intrinsic perk (an exotic's) and the armor set it belongs to. Those perks, catalysts (with the perks they grant) and sets (with their 2- and 4-piece perks) are
written too. Perks a catalyst or a set grants are the manifest's sandbox perks (`kind: perk`). The artifact an
artifact perk belongs to (the artifact item whose sockets' plug sets hold it) is written with its perks
(`kind: artifact`, `perks`: every plug of the plug sets of its sockets that take artifact perks, but a socket's
initial, empty one, socket by socket, each once). A hash the
manifest doesn't have is reported, not written. The output is sorted and stable, so a rerun on the same
manifest changes nothing.

`breakerTypes` are the three breakers with their icons; `armorStats` the six armor stats with their names and icons (an
Armor 3.0 piece's highest stat shows in its square's top-right corner, as DIM shows it); `itemConstants` the overlays Bungie draws on an item's icon, as
DIM does: a gear tier's diamonds (`tierOverlays`, tier 1 to 5) and the masterwork glow (`masterworkOverlay`, and
`masterworkExoticOverlay` for an exotic).
`keywordTraits` are the game's own keywords the glossary's statuses, pickups and summons name (each `trait:` in rules/,
DestinyTraitDefinition: Jolted, Bolt Charge, Woven Mail), with their names and icons: the icon shows next to the
keyword's name.

`statPlugs` are every plug that changes an armor stat, for adding up a build's stats from a player's own armor
the way DIM does (docs/loop-format.md, "Stats"): a piece's rolled stats (`base`: the armor-stat and intrinsic plugs),
its masterwork (`masterwork`, Armor 3.0's: only to a piece's stats at 0), its tuning socket (`tuningSocket`, the
empty socket's plug: it marks the socket), the tuning mods (`tuning`: points from one stat to another; and
`balancedTuning`: like the masterwork, only to stats at 0), the stat mods (`statMod`) and the fragments with stats
(`fragment`); Spark of Focus and Echo of Persistence lower the stat of the class's own ability regeneration (`class`
on each stat), as DIM reads them.
"""

import json
import pathlib
import re
import sys

WEAPON_BUCKETS = {"Kinetic Weapons": "kinetic", "Energy Weapons": "energy", "Power Weapons": "power"}
ARMOR_BUCKETS = {"Helmet": "helmet", "Gauntlets": "arms", "Chest Armor": "chest", "Leg Armor": "legs", "Class Armor": "classItem"}
MOD_SLOTS = {"Helmet Armor Mod": "helmet", "Arms Armor Mod": "arms", "Chest Armor Mod": "chest",
             "Leg Armor Mod": "legs", "Class Item Armor Mod": "classItem", "General Armor Mod": "general"}
TIERS = {2: "basic", 3: "common", 4: "rare", 5: "legendary", 6: "exotic"}
DAMAGE_TYPES = {1: "kinetic", 2: "arc", 3: "solar", 4: "void", 6: "stasis", 7: "strand"}
EMPTY_CATALYST = "v400.empty.exotic.masterwork"  # the plug category of an exotic's empty catalyst socket
ARTIFACT_PERKS = "artifact_perks"                 # what an artifact's perk sockets take…
ARTIFACT_RESET = "artifact_reset"                 # …and its reset socket too, with its Reset Artifact plug
ARMOR_STATS = {2996146975: "weapons", 392767087: "health", 1943323491: "class", 1735777505: "grenade",
               144602215: "super", 4244567218: "melee"}
STAT_PLUG_KINDS = {748854354: "base", 1744546145: "base", 2198080209: "masterwork", 3481777685: "tuning",
                   2487827355: "mod", 3773173029: "mod", 2912171003: "mod", 3422420680: "mod", 1526202480: "mod",
                   2111701510: "mod", 912441879: "mod"}   # plug category hashes (DIM's PlugCategoryHashes)
BALANCED_TUNING = 3122197216
CLASS_STAT_PLUGS = {2272984671, 1727069360}     # Echo of Persistence, Spark of Focus: -10 to the class's regen stat
CLASS_OF_STAT = {"weapons": "hunter", "health": "titan", "class": "warlock"}
CLASS_TYPES = {0: "titan", 1: "hunter", 2: "warlock"}   # an item's classType (3: any class)


def read_json(path):
    with open(path, encoding="utf-8") as file:
        return json.load(file)


def list_rule_hashes(root):
    """Every number after `hash:` in rules/ (one, or a [list]); the manifest excerpt itself is skipped."""
    hashes = set()
    for path in sorted((root / "rules").rglob("*.yaml")):
        if path.name == "manifest.yaml" and path.parent.name == "rules":
            continue
        for match in re.finditer(r"hash:\s*(\[[^\]]*\]|\d+)", path.read_text(encoding="utf-8")):
            hashes.update(int(number) for number in re.findall(r"\d+", match.group(1)))
    return hashes


def list_rule_traits(root):
    """Every number after `trait:` in rules/: the glossary's keywords' traits."""
    traits = set()
    for path in sorted((root / "rules").rglob("*.yaml")):
        if path.name == "manifest.yaml" and path.parent.name == "rules":
            continue
        traits.update(int(number) for number in re.findall(r"trait:\s*(\d+)", path.read_text(encoding="utf-8")))
    return traits


def list_share_hashes(root):
    """Every hash a saved DIM share's loadout wears: equipped items, subclass plugs, mods, artifact perks, the optimizer's exotic."""
    hashes = set()
    for path in sorted((root / "builds").glob("*/dim-loadout.json")):
        loadout = read_json(path).get("loadout", {})
        for item in loadout.get("equipped", []):
            hashes.add(int(item["hash"]))
            hashes.update(int(plug) for plug in item.get("socketOverrides", {}).values())
        parameters = loadout.get("parameters", {})
        hashes.update(int(mod) for mod in parameters.get("mods", []))
        hashes.update(int(perk) for perk in parameters.get("artifactUnlocks", {}).get("unlockedItemHashes", []))
        if int(parameters.get("exoticArmorHash", 0)) > 0:
            hashes.add(int(parameters["exoticArmorHash"]))
    return hashes


def classify(item, buckets):
    """(kind, slot): what Loopsmith calls the item, and its weapon or armor slot where it has one."""
    type_name = item.get("itemTypeDisplayName") or ""
    bucket = buckets.get(str(item.get("inventory", {}).get("bucketTypeHash")), {}).get("displayProperties", {}).get("name", "")
    if bucket == "Subclass":
        return "subclass", None
    if bucket in WEAPON_BUCKETS:
        return "weapon", WEAPON_BUCKETS[bucket]
    if bucket in ARMOR_BUCKETS:
        return "armor", ARMOR_BUCKETS[bucket]
    if type_name.endswith("Armor Mod"):
        return "armorMod", MOD_SLOTS.get(type_name)
    exact = {"Super Ability": "super", "Class Ability": "classAbility", "Movement Ability": "movement",
             "Artifact Perk": "artifactPerk", "Artifact": "artifact", "Trait": "weaponPerk", "Enhanced Trait": "weaponPerk",
             "Intrinsic": "intrinsic"}
    if type_name in exact:
        return exact[type_name], None
    for suffix, kind in ((" Grenade", "grenade"), (" Melee", "melee"), (" Aspect", "aspect"), (" Fragment", "fragment")):
        if type_name.endswith(suffix):
            return kind, None
    return "other", None


def read_traits(item, socket_types, perk_category, plug_sets):
    """The weapon's trait ("frames") columns, each the perks it can roll in order (a fixed column: its one perk).

    None when the weapon has no sockets or a column's perks can't be read: its columns are then unknown, not empty.
    """
    sockets = item.get("sockets")
    if not sockets:
        return None
    entries = sockets.get("socketEntries", [])
    indexes = [i for category in sockets.get("socketCategories", []) if category.get("socketCategoryHash") == perk_category
               for i in category.get("socketIndexes", [])]
    traits = [entries[i] for i in indexes if i < len(entries)
              and "frames" in [w.get("categoryIdentifier") for w in socket_types.get(str(entries[i].get("socketTypeHash")), {}).get("plugWhitelist", [])]]
    columns = [read_column(entry, plug_sets) for entry in traits]
    return None if any(not column for column in columns) else columns


def read_column(entry, plug_sets):
    """A trait socket's perks: those of its random (else curated) plug set that can roll now, each once, or its one fixed perk."""
    plug_set = entry.get("randomizedPlugSetHash") or entry.get("reusablePlugSetHash")
    if plug_set:
        plugs = plug_sets.get(str(plug_set), {}).get("reusablePlugItems", [])
        rollable = [plug["plugItemHash"] for plug in plugs if plug.get("currentlyCanRoll", True)]
        return list(dict.fromkeys(rollable))
    return [entry["singleInitialItemHash"]] if entry.get("singleInitialItemHash") else []


def read_artifact_perks(item, plug_sets, socket_types):
    """An artifact's perks: every plug of the plug sets of its sockets that take artifact perks (not its reset socket),
    but a socket's initial (empty) one, socket by socket, each once."""
    entries = (item.get("sockets") or {}).get("socketEntries", [])
    whitelist = lambda entry: [w.get("categoryIdentifier") for w in socket_types.get(str(entry.get("socketTypeHash")), {}).get("plugWhitelist", [])]
    takes_perks = lambda entry: ARTIFACT_PERKS in whitelist(entry) and ARTIFACT_RESET not in whitelist(entry)
    hashes = [plug["plugItemHash"] for entry in entries if entry.get("reusablePlugSetHash") and takes_perks(entry)
              for plug in plug_sets.get(str(entry["reusablePlugSetHash"]), {}).get("reusablePlugItems", [])
              if plug["plugItemHash"] != entry.get("singleInitialItemHash")]
    return list(dict.fromkeys(hashes))


def to_text(text):
    """Bungie's text as YAML: quoted, ASCII-escaped, without the private-use glyphs of Destiny's icon font (they show as boxes elsewhere)."""
    shown = re.sub(r" {2,}", " ", re.sub("[\ue000-\uf8ff]", "", text)).strip()
    return json.dumps(shown, ensure_ascii=True)


def find_category(categories, name):
    return next(int(h) for h, c in categories.items() if c.get("displayProperties", {}).get("name") == name)


def read_intrinsic(item, category, items):
    """The item's intrinsic perk: the first named plug of an `intrinsics` socket in that category (a weapon's frame or exotic perk, an exotic armor's perk)."""
    sockets = item.get("sockets") or {}
    entries = sockets.get("socketEntries", [])
    for group in sockets.get("socketCategories", []):
        if group.get("socketCategoryHash") != category:
            continue
        for index in group.get("socketIndexes", []):
            plug = items.get(str(entries[index].get("singleInitialItemHash")) if index < len(entries) else "", {})
            if plug.get("plug", {}).get("plugCategoryIdentifier") == "intrinsics" and plug.get("displayProperties", {}).get("name"):
                return plug["hash"]
    return None


def read_frame_breaker(plug, perks, breaker_types):
    """The breaker an intrinsic frame gives its weapon: a perk of the plug named with a breaker in brackets
    ("[Shield-Piercing] Barrier" on Adaptive Frame, "[Disruption] Overload" on Rapid-Fire Frame), as DIM reads it."""
    names = {to_breaker_name(d.get("displayProperties", {}).get("name", "")): int(h) for h, d in breaker_types.items()}
    for entry in (plug or {}).get("perks", []):
        name = perks.get(str(entry.get("perkHash")), {}).get("displayProperties", {}).get("name", "")
        for token in re.findall(r"\[([^\]]+)\]", name):
            if to_breaker_name(token) in names:
                return names[to_breaker_name(token)]
    return None


def to_breaker_name(name):
    return name.replace("-", " ").strip().lower()


def read_granted_perks(item, perks):
    """The sandbox perks a plug grants that the game shows (a catalyst's), in order."""
    return [entry["perkHash"] for entry in item.get("perks", [])
            if perks.get(str(entry.get("perkHash")), {}).get("isDisplayable")
            and perks[str(entry["perkHash"])].get("displayProperties", {}).get("name")]


def read_catalyst(item, socket_types, plugs_by_category, items, perks):
    """An exotic weapon's catalyst: a plug of its catalyst socket's own category that grants a perk the game shows."""
    for entry in (item.get("sockets") or {}).get("socketEntries", []):
        whitelist = [w.get("categoryIdentifier") for w in socket_types.get(str(entry.get("socketTypeHash")), {}).get("plugWhitelist", [])]
        if EMPTY_CATALYST not in whitelist:
            continue
        for category in whitelist:
            for plug in plugs_by_category.get(category, []) if category != EMPTY_CATALYST else []:
                if read_granted_perks(items[str(plug)], perks):
                    return plug
    return None


def read_description(item, perks):
    """Bungie's description of an item; for a subclass plug (an aspect, a fragment) whose own is empty, its perks' texts:
    the perk of its own name gives its effect, the others their name or "name: text" (a fragment's stat change)."""
    own = item.get("displayProperties", {}).get("description")
    if own:
        return own
    lines = []
    for entry in item.get("perks", []):
        perk = perks.get(str(entry.get("perkHash")), {})
        display = perk.get("displayProperties", {})
        name, text = display.get("name", ""), display.get("description", "")
        if not perk.get("isDisplayable") or not (name or text):
            continue
        lines.append(text if name == item.get("displayProperties", {}).get("name") or name == text or not name else f"{name}: {text}" if text else name)
    return "\n".join(lines)


def to_perk_entry(perk_hash, perk):
    """A sandbox perk (a catalyst's or an armor set's): name, Bungie's description, icon; it has no item type."""
    display = perk.get("displayProperties", {})
    fields = [("hash", str(perk_hash)), ("name", to_text(display.get("name", ""))), ("kind", "perk")]
    if display.get("description"):
        fields.append(("description", to_text(display["description"])))
    if display.get("icon"):
        fields.append(("icon", display["icon"]))
    return "  - { " + ", ".join(f"{key}: {value}" for key, value in fields) + " }"


def to_entry(item_hash, item, context):
    buckets, socket_types, perk_category, plug_sets = context["buckets"], context["socket_types"], context["perk_category"], context["plug_sets"]
    display = item.get("displayProperties", {})
    kind, slot = classify(item, buckets)
    kind = "catalyst" if item_hash in context["catalysts"] else kind
    fields = [
        ("hash", str(item_hash)),
        ("name", to_text(display.get("name", ""))),
        ("kind", kind),
    ]
    if item.get("itemTypeDisplayName"):
        fields.append(("type", to_text(item["itemTypeDisplayName"])))
    tier = TIERS.get(item.get("inventory", {}).get("tierType"))
    if tier:
        fields.append(("tier", tier))
    if slot and slot != "general":
        fields.append(("slot", slot))
    damage = DAMAGE_TYPES.get(item.get("defaultDamageType"))
    if kind == "weapon" and damage:
        fields.append(("damageType", damage))
    breaker = item.get("breakerTypeHash") or (
        read_frame_breaker(context["items"].get(str(context["intrinsics"].get(item_hash))), context["perks"], context["breaker_types"])
        if item_hash in context["intrinsics"] else None)
    if kind == "weapon" and breaker:
        fields.append(("breaker", str(breaker)))
    columns = read_traits(item, socket_types, perk_category, plug_sets) if kind == "weapon" else None
    if columns is not None:
        fields.append(("traits", "[" + ", ".join("[" + ", ".join(str(h) for h in column) + "]" for column in columns) + "]"))
    intrinsic = context["intrinsics"].get(item_hash)
    if intrinsic:
        fields.append(("intrinsic", str(intrinsic)))
    catalyst = context["weapon_catalysts"].get(item_hash)
    if catalyst:
        fields.append(("catalyst", str(catalyst)))
    item_set = item.get("equippingBlock", {}).get("equipableItemSetHash") if kind == "armor" else None
    if item_set:
        fields.append(("set", str(item_set)))
    armor_class = CLASS_TYPES.get(item.get("classType")) if kind == "armor" else None
    if armor_class:
        fields.append(("class", armor_class))
    granted = (read_granted_perks(item, context["perks"]) if kind == "catalyst"
               else read_artifact_perks(item, plug_sets, socket_types) if kind == "artifact" else [])
    if granted:
        fields.append(("perks", "[" + ", ".join(str(h) for h in granted) + "]"))
    description = read_description(item, context["perks"])
    if description:
        fields.append(("description", to_text(description)))
    if kind in ("weapon", "armor") and item.get("iconWatermark"):
        fields.append(("watermark", item["iconWatermark"]))
    if display.get("icon"):
        fields.append(("icon", display["icon"]))
    return "  - { " + ", ".join(f"{key}: {value}" for key, value in fields) + " }"


def to_set_entry(set_hash, item_set):
    """An armor set: its name and the perk each piece count grants (`pieces: 2` → its 2-piece perk)."""
    perks = ", ".join(f"{{ pieces: {perk['requiredSetCount']}, perk: {perk['sandboxPerkHash']} }}"
                      for perk in sorted(item_set.get("setPerks", []), key=lambda p: p["requiredSetCount"]))
    name = to_text(item_set.get("displayProperties", {}).get("name", ""))
    return f"  - {{ hash: {set_hash}, name: {name}, perks: [{perks}] }}"


def read_plug_stats(item):
    """The armor stats a plug changes, as [(stat, value)], in the manifest's order."""
    return [(ARMOR_STATS[s["statTypeHash"]], s.get("value", 0)) for s in item.get("investmentStats", [])
            if s.get("statTypeHash") in ARMOR_STATS]


def classify_stat_plug(item):
    """A plug's kind for adding up stats, or None: armor stats, masterworks and tuning always (an empty one marks its
    socket), mods only with a stat, subclass plugs (fragments) with one."""
    plug = item.get("plug") or {}
    kind = STAT_PLUG_KINDS.get(plug.get("plugCategoryHash"))
    stats = read_plug_stats(item)
    if kind in ("masterwork", "tuning") or (kind and stats):
        return kind if kind != "base" or plug.get("plugCategoryHash") == 748854354 or stats else None
    category = plug.get("plugCategoryIdentifier") or ""
    return "subclass" if category.endswith(".fragments") and stats else None


def to_stat_plug_entry(plug_hash, item, kind):
    stats = read_plug_stats(item)
    if plug_hash in CLASS_STAT_PLUGS:
        listed = [f"{{ stat: {stat}, value: {value}, class: {CLASS_OF_STAT[stat]} }}" for stat, value in stats if stat in CLASS_OF_STAT]
    else:
        listed = [f"{{ stat: {stat}, value: {value} }}" for stat, value in stats]
    return f"  - {{ hash: {plug_hash}, kind: {to_written_kind(plug_hash, kind, stats)}, stats: [{', '.join(listed)}] }}"


def to_written_kind(plug_hash, kind, stats):
    """The excerpt's kind of a stat plug: a tuning plug is the empty socket's (no stats), Balanced Tuning or a tuning
    mod; a mod is a stat mod; a subclass plug is a fragment."""
    if kind == "tuning":
        return "tuningSocket" if not stats else "balancedTuning" if plug_hash == BALANCED_TUNING else "tuning"
    return {"mod": "statMod", "subclass": "fragment"}.get(kind, kind)


def main():
    if len(sys.argv) not in (3, 4):
        sys.exit(__doc__)
    source = pathlib.Path(sys.argv[1])
    version = sys.argv[2]
    root = pathlib.Path(sys.argv[3] if len(sys.argv) == 4 else ".")
    items = read_json(source / "DestinyInventoryItemDefinition.json")
    buckets = read_json(source / "DestinyInventoryBucketDefinition.json")
    damage_types = read_json(source / "DestinyDamageTypeDefinition.json")
    socket_types = read_json(source / "DestinySocketTypeDefinition.json")
    plug_sets = read_json(source / "DestinyPlugSetDefinition.json")
    perks = read_json(source / "DestinySandboxPerkDefinition.json")
    item_sets = read_json(source / "DestinyEquipableItemSetDefinition.json")
    categories = read_json(source / "DestinySocketCategoryDefinition.json")
    breaker_types = read_json(source / "DestinyBreakerTypeDefinition.json")
    item_constants = next(iter(read_json(source / "DestinyInventoryItemConstantsDefinition.json").values()), {})
    trait_definitions = read_json(source / "DestinyTraitDefinition.json")
    stat_definitions = read_json(source / "DestinyStatDefinition.json")
    perk_category = find_category(categories, "WEAPON PERKS")
    intrinsic_categories = {"weapon": find_category(categories, "INTRINSIC TRAITS"), "armor": find_category(categories, "ARMOR PERKS")}
    plugs_by_category = {}
    for h, item in sorted(items.items(), key=lambda pair: int(pair[0])):
        category = (item.get("plug") or {}).get("plugCategoryIdentifier")
        if category:
            plugs_by_category.setdefault(category, []).append(int(h))
    set_of_perk = {perk["sandboxPerkHash"]: int(h) for h, item_set in item_sets.items() for perk in item_set.get("setPerks", [])}

    named = list_rule_hashes(root) | list_share_hashes(root)
    named_items = {h for h in named if str(h) in items}
    kinds = {h: classify(items[str(h)], buckets)[0] for h in named_items}
    weapons = {h for h in named_items if kinds[h] == "weapon"}
    traits = {h for n in weapons for column in (read_traits(items[str(n)], socket_types, perk_category, plug_sets) or []) for h in column}
    intrinsics = {h: read_intrinsic(items[str(h)], intrinsic_categories[kinds[h]], items) for h in named_items if kinds[h] in intrinsic_categories}
    intrinsics = {h: plug for h, plug in intrinsics.items() if plug}
    weapon_catalysts = {h: read_catalyst(items[str(h)], socket_types, plugs_by_category, items, perks) for h in weapons}
    weapon_catalysts = {h: plug for h, plug in weapon_catalysts.items() if plug}
    catalysts = set(weapon_catalysts.values())
    catalyst_perks = {perk for plug in catalysts for perk in read_granted_perks(items[str(plug)], perks)}
    wanted_sets = {items[str(h)].get("equippingBlock", {}).get("equipableItemSetHash") for h in named_items if kinds[h] == "armor"} - {None, 0}
    wanted_sets |= {set_of_perk[h] for h in named if h in set_of_perk}
    set_perks = {perk["sandboxPerkHash"] for h in wanted_sets for perk in item_sets[str(h)].get("setPerks", [])}
    artifact_perks = {h for h in named_items if kinds[h] == "artifactPerk"}
    artifacts = {int(h) for h, item in items.items() if item.get("itemTypeDisplayName") == "Artifact"
                 and artifact_perks & set(read_artifact_perks(item, plug_sets, socket_types))}
    context = {"buckets": buckets, "socket_types": socket_types, "perk_category": perk_category, "plug_sets": plug_sets,
               "intrinsics": intrinsics, "weapon_catalysts": weapon_catalysts, "catalysts": catalysts, "perks": perks,
               "items": items, "breaker_types": breaker_types}

    wanted_items = sorted(named_items | traits | set(intrinsics.values()) | catalysts | artifacts)
    wanted_perks = sorted(((named - named_items) & {int(h) for h in perks}) | catalyst_perks | set_perks)
    missing = sorted(named - named_items - set(wanted_perks))
    lines = [
        "# Loopsmith's excerpt of the Bungie manifest (docs/rule-format.md, \"Manifest\"): names, descriptions, icons,",
        "# slots, perks and armor sets for every hash the rules and the saved DIM shares name, and every plug that",
        "# changes an armor stat (statPlugs: for adding up a player's own armor, as DIM does). Generated by",
        "# tools/manifest/extract_manifest.py - don't edit by hand, rerun it. Bungie's data, used under its API terms.",
        f"version: {json.dumps(version)}",
        "damageTypes:",
    ]
    for definition in sorted(damage_types.values(), key=lambda d: d.get("enumValue", 0)):
        name = DAMAGE_TYPES.get(definition.get("enumValue"))
        icon = definition.get("displayProperties", {}).get("icon")
        if name and icon:
            lines.append(f"  - {{ type: {name}, icon: {icon} }}")
    lines.append("breakerTypes:")
    for h, definition in sorted(breaker_types.items(), key=lambda pair: pair[1].get("enumValue", 0)):
        display = definition.get("displayProperties", {})
        if display.get("icon"):
            lines.append(f"  - {{ hash: {h}, name: {to_text(display.get('name', ''))}, icon: {display['icon']} }}")
    lines.append("armorStats:")
    for h, stat in ARMOR_STATS.items():
        display = stat_definitions.get(str(h), {}).get("displayProperties", {})
        if display.get("icon"):
            lines.append(f"  - {{ stat: {stat}, hash: {h}, name: {to_text(display.get('name', ''))}, icon: {display['icon']} }}")
        else:
            missing.append(h)
    lines.append("itemConstants:")
    lines.append("  tierOverlays: [" + ", ".join(item_constants.get("gearTierOverlayImagePaths", [])) + "]")
    for key, written in (("masterworkOverlayPath", "masterworkOverlay"), ("masterworkExoticOverlayPath", "masterworkExoticOverlay")):
        if item_constants.get(key):
            lines.append(f"  {written}: {item_constants[key]}")
    lines.append("keywordTraits:")
    wanted_traits = list_rule_traits(root)
    for h in sorted(wanted_traits):
        display = trait_definitions.get(str(h), {}).get("displayProperties", {})
        if display.get("name") and display.get("icon"):
            lines.append(f"  - {{ hash: {h}, name: {to_text(display['name'])}, icon: {display['icon']} }}")
    missing += sorted(h for h in wanted_traits if not (trait_definitions.get(str(h), {}).get("displayProperties", {}).get("name")
                                                       and trait_definitions.get(str(h), {}).get("displayProperties", {}).get("icon")))
    lines.append("armorSets:")
    lines.extend(to_set_entry(h, item_sets[str(h)]) for h in sorted(wanted_sets))
    lines.append("items:")
    entries = [(h, to_entry(h, items[str(h)], context)) for h in wanted_items]
    entries += [(h, to_perk_entry(h, perks[str(h)])) for h in wanted_perks]
    lines.extend(entry for _, entry in sorted(entries))
    stat_plugs = sorted((int(h), item, kind) for h, item in items.items() for kind in [classify_stat_plug(item)] if kind)
    lines.append("statPlugs:")
    lines.extend(to_stat_plug_entry(h, item, kind) for h, item, kind in stat_plugs)
    (root / "rules" / "manifest.yaml").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {len(entries)} items, {len(wanted_sets)} armor sets and {len(stat_plugs)} stat plugs to rules/manifest.yaml")
    for h in missing:
        print(f"not in the manifest: {h}", file=sys.stderr)


if __name__ == "__main__":
    main()
