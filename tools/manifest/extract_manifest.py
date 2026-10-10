#!/usr/bin/env python3
"""Writes rules/manifest.yaml: Loopsmith's excerpt of the Bungie manifest (docs/rule-format.md, "Manifest").

    python3 -I tools/manifest/extract_manifest.py <manifest-dir> <version> [<repo-root>]

<manifest-dir> holds the English JSON components get-manifest.sh downloads:
DestinyInventoryItemDefinition.json, DestinyInventoryBucketDefinition.json, DestinyDamageTypeDefinition.json,
DestinySocketTypeDefinition.json and DestinySocketCategoryDefinition.json. <version> is the manifest's
version string (written into the file).

The excerpt holds every hash Loopsmith names: each `hash:` in rules/ (elements and the glossary's
subclasses) and every hash in the saved DIM shares (builds/*/dim-loadout.json). For each: its name,
kind, type, icon, tier and, for weapons, armor and armor mods, its slot and damage type. A weapon also
gets its number of trait columns (`traits`: the "frames" sockets among its weapon perks) and the
perks of the columns that don't roll (`fixedTraits`: an exotic's), which are written as items too. A
hash the manifest doesn't have is reported, not written. The output is sorted and stable, so a rerun
on the same manifest changes nothing.
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
             "Artifact Perk": "artifactPerk", "Trait": "weaponPerk", "Enhanced Trait": "weaponPerk"}
    if type_name in exact:
        return exact[type_name], None
    for suffix, kind in ((" Grenade", "grenade"), (" Melee", "melee"), (" Aspect", "aspect"), (" Fragment", "fragment")):
        if type_name.endswith(suffix):
            return kind, None
    return "other", None


def read_traits(item, socket_types, perk_category):
    """(columns, fixed): how many trait ("frames") sockets the weapon's perks have, and the perks of those that don't roll."""
    sockets = item.get("sockets", {})
    entries = sockets.get("socketEntries", [])
    indexes = [i for category in sockets.get("socketCategories", []) if category.get("socketCategoryHash") == perk_category
               for i in category.get("socketIndexes", [])]
    traits = [entries[i] for i in indexes if i < len(entries)
              and "frames" in [w.get("categoryIdentifier") for w in socket_types.get(str(entries[i].get("socketTypeHash")), {}).get("plugWhitelist", [])]]
    fixed = [entry["singleInitialItemHash"] for entry in traits
             if not entry.get("randomizedPlugSetHash") and entry.get("singleInitialItemHash")]
    return (len(traits), fixed) if sockets else (None, [])


def to_entry(item_hash, item, buckets, socket_types, perk_category):
    display = item.get("displayProperties", {})
    kind, slot = classify(item, buckets)
    fields = [
        ("hash", str(item_hash)),
        ("name", json.dumps(display.get("name", ""), ensure_ascii=True)),
        ("kind", kind),
        ("type", json.dumps(item.get("itemTypeDisplayName") or "", ensure_ascii=True)),
    ]
    tier = TIERS.get(item.get("inventory", {}).get("tierType"))
    if tier:
        fields.append(("tier", tier))
    if slot and slot != "general":
        fields.append(("slot", slot))
    damage = DAMAGE_TYPES.get(item.get("defaultDamageType"))
    if kind == "weapon" and damage:
        fields.append(("damageType", damage))
    if kind == "weapon":
        columns, fixed = read_traits(item, socket_types, perk_category)
        if columns is not None:
            fields.append(("traits", str(columns)))
        if fixed:
            fields.append(("fixedTraits", "[" + ", ".join(str(h) for h in fixed) + "]"))
    if display.get("icon"):
        fields.append(("icon", display["icon"]))
    return "  - { " + ", ".join(f"{key}: {value}" for key, value in fields) + " }"


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
    perk_category = next(int(h) for h, c in read_json(source / "DestinySocketCategoryDefinition.json").items()
                         if c.get("displayProperties", {}).get("name") == "WEAPON PERKS")

    named = list_rule_hashes(root) | list_share_hashes(root)
    fixed = {h for n in named if str(n) in items and classify(items[str(n)], buckets)[0] == "weapon"
             for h in read_traits(items[str(n)], socket_types, perk_category)[1]}
    wanted = sorted(named | fixed)
    missing = [h for h in wanted if str(h) not in items]
    lines = [
        "# Loopsmith's excerpt of the Bungie manifest (docs/rule-format.md, \"Manifest\"): names, icons, slots and",
        "# damage types for every hash the rules and the saved DIM shares name. Generated by",
        "# tools/manifest/extract_manifest.py - don't edit by hand, rerun it. Bungie's data, used under its API terms.",
        f"version: {json.dumps(version)}",
        "damageTypes:",
    ]
    for definition in sorted(damage_types.values(), key=lambda d: d.get("enumValue", 0)):
        name = DAMAGE_TYPES.get(definition.get("enumValue"))
        icon = definition.get("displayProperties", {}).get("icon")
        if name and icon:
            lines.append(f"  - {{ type: {name}, icon: {icon} }}")
    lines.append("items:")
    lines.extend(to_entry(h, items[str(h)], buckets, socket_types, perk_category) for h in wanted if str(h) in items)
    (root / "rules" / "manifest.yaml").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {len(wanted) - len(missing)} items to rules/manifest.yaml")
    for h in missing:
        print(f"not in the manifest: {h}", file=sys.stderr)


if __name__ == "__main__":
    main()
