"""Tests of post-scenarios.py's post (write_section): python3 -m unittest tools/e2e/test_post_scenarios.py

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


if __name__ == "__main__":
    unittest.main()
