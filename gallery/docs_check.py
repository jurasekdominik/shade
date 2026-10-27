"""Check Shade's documentation wiring without network access or third-party packages."""

import argparse
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile


def check_links(document):
    text = document.read_text(encoding="utf-8-sig")
    text = re.sub(r"```.*?```", "", text, flags=re.S)
    errors = []
    for target in re.findall(r"\]\(([^\s)]+)\)", text):
        target = target.split("#", 1)[0]
        if not target or re.match(r"[a-zA-Z][\w+.-]*:", target):
            continue
        if not (document.parent / target).exists():
            errors.append(f"{document}: missing local link {target}")
    return errors


def package_documents(project):
    documents = {}
    for item in ET.parse(project).iter("None"):
        if item.get("Pack", "").lower() != "true":
            continue
        for source in item.get("Include", "").split(";"):
            if source.endswith(".md"):
                path = project.parent / source
                destination = item.get("PackagePath", "/").strip("/")
                documents[f"{destination}/{path.name}".lstrip("/")] = path
    return documents


def check(root, package=None):
    errors = []
    documents = [root / name for name in ("AGENTS.md", "CLAUDE.md", "README.md", "CONTRIBUTING.md", "DESIGN.md")]
    documents += sorted((root / "docs").glob("*.md"))
    documents += sorted((root / ".github/ISSUE_TEMPLATE").glob("*.md"))
    for document in documents:
        if not document.is_file():
            errors.append(f"Missing document: {document}")
        else:
            errors.extend(check_links(document))

    for name in ("AGENTS.md", "README.md", "CONTRIBUTING.md", "DESIGN.md", "docs/ADOPTION.md", "src/Shade/PACKAGE.md"):
        if "APP_AGENTS.md" not in (root / name).read_text(encoding="utf-8-sig"):
            errors.append(f"{name}: missing reference to the canonical app-agent guide")
    if (root / "CLAUDE.md").read_text(encoding="utf-8-sig").strip() != "@AGENTS.md":
        errors.append("CLAUDE.md must import AGENTS.md rather than duplicate its rules")

    project = root / "src/Shade/Shade.csproj"
    shipped = package_documents(project)
    for required in ("PACKAGE.md", "DESIGN.md", "docs/ADOPTION.md", "docs/APP_AGENTS.md"):
        if required not in shipped:
            errors.append(f"{required}: missing package entry")
    for name, source in shipped.items():
        if not source.is_file():
            errors.append(f"{name}: missing package source {source}")

    version = ET.parse(project).findtext(".//Version")
    readme = (root / "README.md").read_text(encoding="utf-8-sig")
    if f'<PackageReference Include="Shade" Version="{version}" />' not in readme:
        errors.append(f"README.md install version must match {version}")
    if f"| {version} |" not in readme:
        errors.append(f"README.md release table is missing {version}")

    if package:
        with zipfile.ZipFile(package) as archive:
            for name, source in shipped.items():
                if name not in archive.namelist():
                    errors.append(f"Package is missing {name}")
                elif source.is_file() and archive.read(name).decode("utf-8-sig").replace("\r\n", "\n") != source.read_text(encoding="utf-8-sig"):
                    errors.append(f"Package contains stale {name}")
    return errors


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path)
    args = parser.parse_args()
    failures = check(Path(__file__).resolve().parent.parent, args.package)
    for failure in failures:
        print(failure)
    if failures:
        raise SystemExit(1)
    print("Documentation links, guide references, versions and package documents passed.")
