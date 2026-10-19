"""Tests of post-scenarios.py's post (write_section), of how it collapses, hides and shrinks the earlier ones, of which release
each run goes to and of what each release keeps when the PR closes:
python3 -m unittest tools/e2e/test_post_scenarios.py

CI doesn't run them (it runs dotnet test): run them after changing the post.
"""
import importlib.util
import os
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("post_scenarios", os.path.join(os.path.dirname(__file__), "post-scenarios.py"))
post_scenarios = importlib.util.module_from_spec(spec)
spec.loader.exec_module(post_scenarios)

REPO = "Owner/repo"


class WriteSectionTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)

    def run_of(self, cls, method, screen, passed=True, **selection):
        gif = os.path.join(self.folder.name, f"{method}-{screen}.gif")
        open(gif, "wb").close()
        run = {"class": cls, "method": method, "title": f"{method} title", "gherkin": f"Scenario: {method}",
               "screen": screen, "file": gif, "mp4_file": None, "passed": passed,
               "asset": f"pr-1-v1-{method}-{screen}.gif", "mp4_asset": f"pr-1-v1-{method}-{screen}.mp4"}
        run.update(selection)
        return run

    def test_both_kinds_show_the_prs_own_first_then_the_rerecorded_with_their_fixtures(self):
        runs = [
            self.run_of("EnemyCountTests", "Counts", "desktop", selected="fixtures",
                        fixtures=["Scenarios/Loop/Weapons/a.build.yaml", "Scenarios/Loop/Weapons/b.build.yaml"]),
            self.run_of("BuildImportTests", "Imports", "desktop", selected="changed"),
            self.run_of("BuildImportTests", "Imports", "mobile", selected="changed"),
        ]
        post = post_scenarios.write_section(REPO, 2, runs, "Text.")
        own = post.index("### Added or changed by this PR")
        rerecorded = post.index("### Re-recorded: their fixtures changed")
        self.assertLess(own, post.index("#### Imports title"))
        self.assertLess(post.index("#### Imports title"), rerecorded)
        self.assertLess(rerecorded, post.index("#### Counts title"))
        self.assertIn("##### 🖥️ Desktop", post)
        self.assertIn("##### 📱 Mobile", post)
        self.assertIn("_`Scenarios/Loop/Weapons/a.build.yaml`, `Scenarios/Loop/Weapons/b.build.yaml` changed._", post)
        self.assertNotIn("\n### Imports title", post)

    def test_only_rerecorded_scenarios_leave_out_the_prs_own_heading(self):
        runs = [self.run_of("CodeLensTests", "Lens", "desktop", selected="fixtures", fixtures=["Workspace/x.cs"])]
        post = post_scenarios.write_section(REPO, 1, runs, "Text.")
        self.assertNotIn("Added or changed by this PR", post)
        self.assertIn("### Re-recorded: their fixtures changed", post)
        self.assertIn("#### Lens title", post)
        self.assertIn("_`Workspace/x.cs` changed._", post)

    def test_only_the_prs_own_scenarios_read_as_before_without_section_headings(self):
        runs = [self.run_of("BuildImportTests", "Imports", "desktop", selected="changed")]
        post = post_scenarios.write_section(REPO, 1, runs, "Text.")
        self.assertNotIn("Added or changed by this PR", post)
        self.assertNotIn("Re-recorded", post)
        self.assertIn("\n### Imports title\n", post)
        self.assertIn("\n#### 🖥️ Desktop\n", post)

    def test_a_scenarios_json_without_selected_reads_as_before(self):
        runs = [self.run_of("ReloadTests", "Reloads", "mobile")]
        post = post_scenarios.write_section(REPO, 1, runs, "Text.")
        self.assertNotIn("Re-recorded", post)
        self.assertIn("\n### Reloads title\n", post)
        self.assertIn("\n#### 📱 Mobile\n", post)

    def test_a_failing_scenario_keeps_its_mark_in_either_section(self):
        runs = [
            self.run_of("BuildImportTests", "Imports", "desktop", passed=False, selected="changed"),
            self.run_of("ReloadTests", "Reloads", "desktop", passed=False, selected="fixtures", fixtures=["f.yaml"]),
        ]
        post = post_scenarios.write_section(REPO, 1, runs, "Text.")
        self.assertIn("#### ❌ Imports title", post)
        self.assertIn("#### ❌ Reloads title", post)
        self.assertEqual(post.count("##### ❌ 🖥️ Desktop"), 2)

    def test_a_rule_runs_gif_and_player_link_point_at_its_rule_release(self):
        run = self.run_of("CombinationBlowTests", "Hits", "desktop", release="rules-hunter-arc-melees-resources")
        open(run["file"].replace(".gif", ".mp4"), "wb").close()
        run["mp4_file"] = run["file"].replace(".gif", ".mp4")
        post = post_scenarios.write_section(REPO, 1, [run], "Text.")
        self.assertIn("https://github.com/Owner/repo/releases/download/rules-hunter-arc-melees-resources/pr-1-v1-Hits-desktop.gif",
                      post)
        self.assertIn("(https://owner.github.io/repo/e2e-player.html?v=pr-1-v1-Hits-desktop.mp4"
                      "&r=rules-hunter-arc-melees-resources)", post)

    def test_any_other_runs_gif_and_player_link_point_at_prs_resources_with_no_release_in_the_link(self):
        run = self.run_of("ReloadTests", "Reloads", "desktop", release="prs-resources")
        open(run["file"].replace(".gif", ".mp4"), "wb").close()
        run["mp4_file"] = run["file"].replace(".gif", ".mp4")
        post = post_scenarios.write_section(REPO, 1, [run], "Text.")
        self.assertIn("/releases/download/prs-resources/pr-1-v1-Reloads-desktop.gif", post)
        self.assertIn("(https://owner.github.io/repo/e2e-player.html?v=pr-1-v1-Reloads-desktop.mp4)", post)

    def flow_of(self, run, rule):
        png = os.path.join(self.folder.name, f"{rule}.png")
        open(png, "wb").close()
        run.update(rule=rule, flow=f"Rules/Hunter/Arc/Aspects/{rule}.png", flow_file=png,
                   flow_asset=f"pr-1-v1-{rule}.png", release="rules-hunter-arc-aspects-resources")
        return run

    def test_a_rule_scenario_reads_title_then_its_flow_then_its_gherkin_then_its_screens(self):
        runs = [self.flow_of(self.run_of("LethalCurrentTests", "Arms", screen), "hunter-arc-aspects-lethal-current-2")
                for screen in ("desktop", "mobile")]
        post = post_scenarios.write_section(REPO, 1, runs, "Text.")
        flow = ("![Flow of hunter-arc-aspects-lethal-current-2](https://github.com/Owner/repo/releases/download/"
                "rules-hunter-arc-aspects-resources/pr-1-v1-hunter-arc-aspects-lethal-current-2.png)")
        self.assertEqual(post.count(flow), 1)
        self.assertLess(post.index("### Arms title"), post.index(flow))
        self.assertLess(post.index(flow), post.index("```gherkin"))
        self.assertLess(post.index("```gherkin"), post.index("#### 🖥️ Desktop"))
        self.assertLess(post.index("#### 🖥️ Desktop"), post.index("#### 📱 Mobile"))

    def test_a_rule_the_viewer_couldnt_draw_says_why_where_its_flow_would_be(self):
        run = self.run_of("SparkOfShockTests", "Jolts", "desktop", rule="all-classes-arc-fragments-spark-of-shock-1",
                          flowError="No rule is named 'all-classes-arc-fragments-spark-of-shock-1'.")
        post = post_scenarios.write_section(REPO, 1, [run], "Text.")
        line = ("_No flow drawn for `all-classes-arc-fragments-spark-of-shock-1`: No rule is named "
                "'all-classes-arc-fragments-spark-of-shock-1'._")
        self.assertIn(line, post)
        self.assertLess(post.index("### Jolts title"), post.index(line))
        self.assertLess(post.index(line), post.index("```gherkin"))
        self.assertNotIn("![Flow", post)

    def test_a_scenario_that_names_no_rule_shows_no_flow(self):
        post = post_scenarios.write_section(REPO, 1, [self.run_of("ReloadTests", "Reloads", "desktop")], "Text.")
        self.assertNotIn("Flow", post)
        self.assertNotIn("No flow drawn", post)

    def test_a_flow_is_named_by_its_rule_once_for_all_its_runs(self):
        run = {"flow": "Rules/Hunter/Arc/Aspects/hunter-arc-aspects-lethal-current-2.png"}
        self.assertEqual(post_scenarios.name_flow(7, 3, run), "pr-7-v3-hunter-arc-aspects-lethal-current-2.png")
        self.assertIsNone(post_scenarios.name_flow(7, 3, {"flow": None}))


class FindReleaseTests(unittest.TestCase):
    def test_a_rule_scenarios_release_is_its_folders_under_rules_kebab_cased(self):
        self.assertEqual(post_scenarios.find_release("Rules/Hunter/Arc/Melees/hits-desktop.gif"),
                         "rules-hunter-arc-melees-resources")
        self.assertEqual(post_scenarios.find_release("Rules/AllClasses/Arc/Fragments/x-mobile.gif"),
                         "rules-all-classes-arc-fragments-resources")
        self.assertEqual(post_scenarios.find_release("Rules/Hunter/ClassAbilities/x-desktop.gif"),
                         "rules-hunter-class-abilities-resources")
        self.assertEqual(post_scenarios.find_release("Rules/Mods/Arms/x-desktop.gif"), "rules-mods-arms-resources")

    def test_any_other_scenarios_release_is_prs_resources(self):
        self.assertEqual(post_scenarios.find_release("Starting/x-desktop.gif"), "prs-resources")
        self.assertEqual(post_scenarios.find_release("Loop/Weapons/x-mobile.gif"), "prs-resources")
        self.assertEqual(post_scenarios.find_release("Recordings/x-desktop.gif"), "prs-resources")

    def test_rule_releases_are_told_apart_from_any_other_tag(self):
        self.assertTrue(post_scenarios.is_rule_release("rules-hunter-arc-melees-resources"))
        self.assertFalse(post_scenarios.is_rule_release("prs-resources"))
        self.assertFalse(post_scenarios.is_rule_release("rules-resources"))
        self.assertFalse(post_scenarios.is_rule_release("v1.0"))


def assets(*names):
    return [{"id": at, "name": name} for at, name in enumerate(names)]


def names(deleted):
    return sorted(asset["name"] for asset in deleted)


class CleanTests(unittest.TestCase):
    def test_prs_resources_keeps_the_prs_newest_version_only(self):
        held = assets("pr-7-v1-a-desktop.gif", "pr-7-v2-a-desktop.gif", "pr-7-v10-b-desktop.gif",
                      "pr-70-v1-a-desktop.gif", "owners-own.png")
        self.assertEqual(names(post_scenarios.plan_pr_deletions(held, 7)), ["pr-7-v1-a-desktop.gif", "pr-7-v2-a-desktop.gif"])

    def test_a_merged_pr_keeps_its_newest_recording_of_each_rule_and_deletes_older_ones_of_closed_prs(self):
        held = assets("pr-5-v1-hits-desktop.gif",   # a closed PR's, replaced
                      "pr-6-v1-hits-desktop.gif",   # an open PR's, kept for its posts
                      "pr-7-v1-hits-desktop.gif",   # this PR's older one
                      "pr-7-v2-hits-desktop.gif",
                      "pr-7-v1-only-in-v1-mobile.gif",  # a rule V2 didn't re-record: still its newest
                      "pr-5-v1-other-rule-desktop.gif",  # a rule this PR didn't touch
                      "owners-own.png")
        deleted = post_scenarios.plan_rule_deletions(held, 7, merged=True, open_prs={6})
        self.assertEqual(names(deleted), ["pr-5-v1-hits-desktop.gif", "pr-7-v1-hits-desktop.gif"])

    def test_a_long_named_rule_recording_of_another_pr_is_the_same_run_by_its_hash(self):
        def long(pr, version, extension=".gif"):
            stem = f"pr-{pr}-v{version}-" + "a-rule-whose-title-goes-on-" * 8
            return stem[:200 - len(f"-7cec2320{extension}")].rstrip("-") + f"-7cec2320{extension}"
        held = assets(long(5, 1), long(140, 12), long(140, 9), long(140, 12, ".mp4"))
        self.assertEqual(len({post_scenarios.read_asset(asset["name"])[2] for asset in held[:3]}), 1)
        deleted = post_scenarios.plan_rule_deletions(held, 140, merged=True, open_prs=set())
        self.assertEqual(names(deleted), sorted([long(5, 1), long(140, 9)]))

    def test_a_merged_pr_keeps_its_newest_flow_of_each_rule_like_a_recording(self):
        held = assets("pr-5-v1-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-hunter-arc-aspects-lethal-current-2.png",
                      "pr-7-v2-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-arms-desktop.gif")
        self.assertEqual(names(post_scenarios.plan_rule_deletions(held, 7, True, set())),
                         ["pr-5-v1-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-hunter-arc-aspects-lethal-current-2.png"])

    def test_a_pr_closed_without_merging_deletes_all_its_rule_recordings(self):
        held = assets("pr-7-v1-hits-desktop.gif", "pr-7-v2-hits-desktop.mp4", "pr-5-v1-hits-desktop.gif", "pr-70-v1-x.gif")
        deleted = post_scenarios.plan_rule_deletions(held, 7, merged=False, open_prs=set())
        self.assertEqual(names(deleted), ["pr-7-v1-hits-desktop.gif", "pr-7-v2-hits-desktop.mp4"])

    def test_closing_without_merging_with_rule_recordings_shrinks_the_newest_post_too(self):
        v1 = comment(1, post_of(1, "A"))
        v2 = comment(2, post_of(2, "A", "B"))
        edits = post_scenarios.plan_shrinks([v1, v2], newest_too=True)
        self.assertEqual([edited["id"] for edited, _ in edits], [1, 2])
        self.assertEqual(edits[1][1].splitlines()[0],
                         "**Scenarios (V2)** · 2 scenarios · recordings deleted when the PR closed without merging")
        self.assertEqual(post_scenarios.find_last_version([comment(2, edits[1][1])]), 2)


def post_of(version, *titles, sections=False):
    """A workflow post as write_section writes it: titles ### (#### under a section), screens one level down."""
    level = "####" if sections else "###"
    lines = [f"## Scenarios (V{version})", "", "**What changed**", "", "Text.", ""]
    if sections:
        lines += ["### Added or changed by this PR", ""]
    for title in titles:
        lines += [f"{level} {title}", "", "```gherkin", "Scenario: x", "```", "", f"{level}# 🖥️ Desktop", "", "![gif](u)", ""]
    lines += ["---", post_scenarios.FOOTER, ""]
    return "\n".join(lines)


def comment(id, body):
    return {"id": id, "node_id": f"IC_{id}", "body": body}


class CollapseTests(unittest.TestCase):
    def test_a_new_version_collapses_the_previous_post_as_it_was(self):
        v1 = comment(1, post_of(1, "A", "B"))
        [(edited, body)] = post_scenarios.plan_collapses([v1], 2)
        self.assertIs(edited, v1)
        self.assertTrue(body.startswith("<details><summary><b>Scenarios (V1)</b> · 2 scenarios · replaced by V2</summary>\n\n"))
        self.assertIn(v1["body"].rstrip(), body)
        self.assertTrue(body.endswith("</details>\n"))
        self.assertNotIn("<details open", body)

    def test_a_post_already_collapsed_is_left_as_it_is(self):
        v1 = comment(1, post_scenarios.plan_collapses([comment(1, post_of(1, "A"))], 2)[0][1])
        v2 = comment(2, post_of(2, "A", "B", "C"))
        [(edited, body)] = post_scenarios.plan_collapses([v1, v2], 3)
        self.assertIs(edited, v2)
        self.assertIn("<b>Scenarios (V2)</b> · 3 scenarios · replaced by V3", body)

    def test_scenarios_under_sections_count_once_each_and_one_reads_singular(self):
        self.assertEqual(post_scenarios.count_scenarios(post_of(1, "A", "B", sections=True)), 2)
        [(_, body)] = post_scenarios.plan_collapses([comment(1, post_of(1, "A"))], 2)
        self.assertIn("· 1 scenario ·", body)

    def test_another_comment_of_the_workflow_is_never_edited(self):
        other = comment(9, f"Something else\n\n---\n{post_scenarios.FOOTER}\n")
        self.assertEqual(post_scenarios.plan_collapses([other], 1), [])
        self.assertEqual(post_scenarios.plan_shrinks([other]), [])


class HideTests(unittest.TestCase):
    def test_a_new_version_hides_every_earlier_post_open_or_collapsed(self):
        v1 = comment(1, post_scenarios.plan_collapses([comment(1, post_of(1, "A"))], 2)[0][1])
        v2 = comment(2, post_of(2, "A"))
        self.assertEqual([c["id"] for c in post_scenarios.plan_hides([v1, v2], 3, set())], [1, 2])

    def test_a_post_already_hidden_is_left_as_it_is(self):
        v1 = comment(1, post_of(1, "A"))
        v2 = comment(2, post_of(2, "A"))
        self.assertEqual([c["id"] for c in post_scenarios.plan_hides([v1, v2], 3, {"IC_1"})], [2])

    def test_the_newest_a_shrunk_post_and_another_comment_of_the_workflow_are_never_hidden(self):
        shrunk = comment(1, post_scenarios.plan_shrinks([comment(1, post_of(1, "A")), comment(2, post_of(2, "A"))])[0][1])
        other = comment(9, f"Something else\n\n---\n{post_scenarios.FOOTER}\n")
        newest = comment(3, post_of(3, "A"))
        self.assertEqual(post_scenarios.plan_hides([shrunk, other, newest], 3, set()), [])


class ShrinkTests(unittest.TestCase):
    def test_closing_shrinks_every_post_but_the_newest_to_its_line(self):
        v1 = comment(1, post_scenarios.plan_collapses([comment(1, post_of(1, "A", "B"))], 2)[0][1])
        v2 = comment(2, post_of(2, "A"))
        v3 = comment(3, post_of(3, "A"))
        edits = post_scenarios.plan_shrinks([v1, v2, v3])
        self.assertEqual([edited["id"] for edited, _ in edits], [1, 2])
        self.assertEqual(edits[0][1].splitlines()[0],
                         "**Scenarios (V1)** · 2 scenarios · replaced by V2 · recordings deleted when the PR closed")
        self.assertIn("replaced by V3", edits[1][1])
        self.assertIn(post_scenarios.FOOTER, edits[0][1])

    def test_a_shrunk_post_is_left_as_it_is_and_still_counts_its_version(self):
        shrunk = comment(1, post_scenarios.plan_shrinks([comment(1, post_of(1, "A")), comment(2, post_of(2, "A"))])[0][1])
        v2 = comment(2, post_of(2, "A"))
        self.assertEqual(post_scenarios.plan_shrinks([shrunk, v2]), [])
        self.assertEqual(post_scenarios.find_last_version([shrunk]), 1)
        self.assertEqual(post_scenarios.plan_collapses([shrunk], 3), [])

    def test_versions_count_collapsed_and_shrunk_posts_after_a_reopen(self):
        shrunk = comment(1, post_scenarios.plan_shrinks([comment(1, post_of(1, "A")), comment(2, post_of(2, "A"))])[0][1])
        collapsed = comment(2, post_scenarios.plan_collapses([comment(2, post_of(2, "A"))], 3)[0][1])
        self.assertEqual(post_scenarios.find_last_version([shrunk, collapsed]), 2)


if __name__ == "__main__":
    unittest.main()
