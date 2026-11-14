"""The cloud build's upload script, run for real against stand-ins for Apple's tools."""

import base64
import os
import shutil
import stat
import subprocess
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[3] / "unity" / "Tools" / "upload-to-testflight.sh"

GIT_BASH = Path("C:/Program Files/Git/bin/bash.exe")  # plain `bash` on Windows may be WSL's
BASH = str(GIT_BASH) if GIT_BASH.exists() else shutil.which("bash")

pytestmark = pytest.mark.skipif(BASH is None, reason="needs bash")


def _tool(folder: Path, name: str, body: str) -> None:
    path = folder / name
    path.write_text(f"#!/usr/bin/env bash\n{body}\n", encoding="utf8", newline="\n")
    path.chmod(path.stat().st_mode | stat.S_IEXEC)


def _run(tmp_path: Path, *, altool: str = 'echo "UPLOAD SUCCEEDED"', **env: str):
    tools = tmp_path / "tools"
    tools.mkdir()
    calls = tmp_path / "calls.txt"
    _tool(tools, "unzip", 'echo "info"')
    _tool(
        tools,
        "plutil",
        'case "$2" in CFBundleIdentifier) echo game.milli.com;;'
        " CFBundleVersion) echo 42;; *) echo 1.0.0;; esac",
    )
    _tool(
        tools,
        "xcrun",
        f'echo "$@" > "{calls.as_posix()}"\n'
        f'cat "$API_PRIVATE_KEYS_DIR/AuthKey_KEY123.p8" >> "{calls.as_posix()}"\n{altool}',
    )
    ipa = tmp_path / "Milli.ipa"
    ipa.write_bytes(b"ipa")
    values = {
        "ASC_KEY_ID": "KEY123",
        "ASC_ISSUER_ID": "issuer-1",
        "ASC_KEY_BASE64": base64.b64encode(b"secret key").decode(),
        "ASC_APP_ID": "1234567890",
        "UNITY_PLAYER_PATH": ipa.as_posix(),
        **env,
    }
    result = subprocess.run(
        [BASH, SCRIPT.as_posix()],
        env={**os.environ, **values, "PATH": f"{tools}{os.pathsep}{os.environ['PATH']}"},
        capture_output=True,
        text=True,
        check=False,
    )
    return result, calls


def test_a_build_is_uploaded_with_its_own_identity(tmp_path: Path) -> None:
    result, calls = _run(tmp_path)

    assert result.returncode == 0, result.stdout + result.stderr
    sent = calls.read_text(encoding="utf8")
    assert "--upload-package" in sent and "Milli.ipa" in sent
    assert "--type ios" in sent
    assert "--apple-id 1234567890" in sent
    assert "--bundle-id game.milli.com" in sent
    assert "--bundle-version 42" in sent
    assert "--bundle-short-version-string 1.0.0" in sent
    assert "--apiKey KEY123" in sent and "--apiIssuer issuer-1" in sent
    assert "secret key" in sent, "the key was written where altool looks for it"


def test_the_key_never_reaches_the_build_log_in_the_clear(tmp_path: Path) -> None:
    result, _ = _run(tmp_path)

    assert "Uploaded to App Store Connect" in result.stdout
    assert "secret key" not in result.stdout + result.stderr


def test_a_failed_upload_fails_the_build(tmp_path: Path) -> None:
    result, _ = _run(tmp_path, altool='echo "*** Error: bad bundle"; exit 1')

    assert result.returncode == 1
    assert "The upload to App Store Connect failed." in result.stdout


def test_altool_exiting_zero_after_an_error_still_fails_the_build(tmp_path: Path) -> None:
    result, _ = _run(tmp_path, altool='echo "UPLOAD FAILED with 1 error"')

    assert result.returncode == 1
    assert "The upload to App Store Connect failed." in result.stdout


def test_without_the_variables_the_build_just_skips_the_upload(tmp_path: Path) -> None:
    result, calls = _run(tmp_path, ASC_APP_ID="")

    assert result.returncode == 0
    assert "skipping" in result.stdout
    assert not calls.exists()


def test_a_missing_ipa_fails_the_build(tmp_path: Path) -> None:
    result, calls = _run(tmp_path, UNITY_PLAYER_PATH=(tmp_path / "nothing.ipa").as_posix())

    assert result.returncode == 1
    assert "No .ipa found" in result.stdout
    assert not calls.exists()


def test_a_folder_with_an_ipa_inside_works_too(tmp_path: Path) -> None:
    folder = tmp_path / "output"
    folder.mkdir()
    (folder / "build.ipa").write_bytes(b"ipa")

    result, calls = _run(tmp_path, UNITY_PLAYER_PATH=folder.as_posix())

    assert result.returncode == 0, result.stdout + result.stderr
    assert "build.ipa" in calls.read_text(encoding="utf8")
