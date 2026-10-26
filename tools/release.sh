#!/usr/bin/env bash
# Publishes a release for the custom plugin repository: optionally a new version, a Release build, a GitHub release
# with latest.zip, and repo.json on main pointing at it.
# Runs where dotnet, gh (logged in) and python3 are, from anywhere in the repository:
#   tools/release.sh [version]     e.g. tools/release.sh 0.2.0.0 (without: the version in the project file)
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"
project=src/GlamourBrowser/GlamourBrowser.csproj
out=src/GlamourBrowser/bin/Release/ShyneesGlamourDresser
repo=ShyneeAK/GlamourBrowser

# Git uses the gh login for github.com in these commands only.
git_gh() { git -c credential.helper= -c 'credential.https://github.com.helper=!gh auth git-credential' "$@"; }

if [ -n "$(git status --porcelain)" ]; then
  echo "Uncommitted changes: commit them first." >&2
  exit 1
fi

if [ $# -ge 1 ]; then
  sed -i "s|<Version>.*</Version>|<Version>$1</Version>|" "$project"
fi
version=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$project")
tag="v$version"
if git rev-parse -q --verify "refs/tags/$tag" >/dev/null || gh release view "$tag" --repo "$repo" >/dev/null 2>&1; then
  echo "Release $tag exists already: pass a new version." >&2
  git checkout -- "$project"
  exit 1
fi

# Every release has its changelog entry in each language: the plugin shows it after the update.
for file in src/GlamourBrowser.Core/Changelog/*.md; do
  if ! grep -qx "## $version" "$file"; then
    echo "No changelog entry '## $version' in $file: write it first." >&2
    git checkout -- "$project"
    exit 1
  fi
done
# The English entry is the release's description on GitHub.
notes=$(awk -v head="## $version" '$0 == head { on = 1; next } /^## / { on = 0 } on' src/GlamourBrowser.Core/Changelog/en.md)

# The packager zips the whole output folder: leftovers of earlier builds (old manifests, the last latest.zip) would
# end up in the release.
rm -rf src/GlamourBrowser/bin/Release
dotnet build "$project" -c Release -warnaserror

# The packager's manifest plus the download links Dalamud needs.
python3 - "$out/ShyneesGlamourDresser.json" "$repo" "$tag" > repo.json <<'EOF'
import json, sys, time
manifest = json.load(open(sys.argv[1], encoding="utf-8"))
link = f"https://github.com/{sys.argv[2]}/releases/download/{sys.argv[3]}/latest.zip"
manifest.update(DownloadLinkInstall=link, DownloadLinkUpdate=link, DownloadLinkTesting=link,
                IsTestingExclusive=False, LastUpdate=int(time.time()))
json.dump([manifest], sys.stdout, indent=2, ensure_ascii=False)
print()
EOF

git add "$project" repo.json
git commit -q -m "release: $tag"
git tag "$tag"
git_gh push origin "$tag"
gh release create "$tag" "$out/latest.zip" --repo "$repo" --verify-tag --title "Shynees Glamour Dresser $version" --notes "$notes"
# Only now: from here on repo.json points at a release that exists.
git_gh push origin HEAD:main
echo "Released $tag"
