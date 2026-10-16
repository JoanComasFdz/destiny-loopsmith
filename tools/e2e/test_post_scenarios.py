"""Tests of post-scenarios.py's post (write_section) and of how it collapses and shrinks the earlier ones:
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
    return {"id": id, "body": body}


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
