"""Zip whatever players have been built into dist/, named for the version in ProjectSettings:

    python3 Tools/package_release.py

    dist/LostAndFound-v<version>-linux-x86_64.zip     from Builds/Linux   (Tools/unity.sh build-linux)
    dist/LostAndFound-v<version>-macos-universal.zip  from Builds/macOS   (Tools/unity.sh build-mac)
    dist/LostAndFound-v<version>-windows-x86_64.zip   from Builds/Windows (Tools/unity.sh build-windows)

Unix permissions (the executable bits) and symlinks inside the .app bundle are kept, so the archives
unzip ready to run. Unity's *_DoNotShip / *_ButDontShipItWithYourGame debug folders are left out.
Nothing is uploaded or published.
"""
import os
import re
import stat
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLATFORMS = (("Linux", "linux-x86_64"), ("macOS", "macos-universal"), ("Windows", "windows-x86_64"))
SKIP = ("_ButDontShipItWithYourGame", "_DoNotShip")


def version():
    with open(os.path.join(ROOT, "ProjectSettings", "ProjectSettings.asset"), encoding="utf-8") as f:
        return re.search(r"^\s*bundleVersion:\s*(\S+)", f.read(), re.M).group(1)


def pack(src, out):
    count = 0
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for dirpath, dirnames, filenames in os.walk(src, followlinks=False):
            dirnames[:] = sorted(d for d in dirnames if not d.endswith(SKIP))
            rel_dir = os.path.relpath(dirpath, src)
            # symlinked directories are listed with the directories: store them as links
            for name in sorted(filenames) + [d for d in dirnames if os.path.islink(os.path.join(dirpath, d))]:
                path = os.path.join(dirpath, name)
                arc = os.path.normpath(os.path.join(rel_dir, name))
                st = os.lstat(path)
                info = zipfile.ZipInfo(arc)
                info.create_system = 3   # Unix, so the mode bits below are honoured on unzip
                info.external_attr = (st.st_mode & 0xFFFF) << 16
                if stat.S_ISLNK(st.st_mode):
                    z.writestr(info, os.readlink(path))
                else:
                    info.compress_type = zipfile.ZIP_DEFLATED
                    with open(path, "rb") as f:
                        z.writestr(info, f.read())
                count += 1
            dirnames[:] = [d for d in dirnames if not os.path.islink(os.path.join(dirpath, d))]
    return count


def main():
    v = version()
    dist = os.path.join(ROOT, "dist")
    os.makedirs(dist, exist_ok=True)
    made = 0
    for folder, platform in PLATFORMS:
        src = os.path.join(ROOT, "Builds", folder)
        if not os.path.isdir(src) or not os.listdir(src):
            print(f"skip {platform}: no build in Builds/{folder}")
            continue
        out = os.path.join(dist, f"LostAndFound-v{v}-{platform}.zip")
        n = pack(src, out)
        print(f"{os.path.relpath(out, ROOT)}  {n} files, {os.path.getsize(out) / 2**20:.0f} MB")
        made += 1
    return 0 if made else 1


if __name__ == "__main__":
    sys.exit(main())
