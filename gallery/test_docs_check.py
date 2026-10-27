import tempfile
import unittest
from pathlib import Path
import zipfile

from docs_check import check, check_links


class DocumentationChecks(unittest.TestCase):
    def test_links_detect_missing_files_but_allow_external_urls_and_code_examples(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "exists.md").write_text("# Target", encoding="utf-8")
            document = root / "guide.md"
            document.write_text(
                "[ok](exists.md#target) [missing](absent.md) [web](https://example.com) [anchor](#here)\n"
                "```markdown\n[example](placeholder.md)\n```", encoding="utf-8")
            errors = check_links(document)
            self.assertEqual(len(errors), 1)
            self.assertIn("absent.md", errors[0])

    def test_missing_guide_and_stale_packaged_document_fail(self):
        root = Path(__file__).resolve().parent.parent
        with tempfile.TemporaryDirectory() as folder:
            package = Path(folder) / "test.nupkg"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("DESIGN.md", "stale content")
            errors = check(root, package)
            self.assertIn("Package is missing docs/APP_AGENTS.md", errors)
            self.assertIn("Package contains stale DESIGN.md", errors)


if __name__ == "__main__":
    unittest.main()
