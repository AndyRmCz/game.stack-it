"""The screen mockups render at phone size and frame inside Play's limits."""

from scripts.generate_screen_mockups import SCREENS, STRINGS, H, K, W, near_miss
from scripts.generate_store_art import framed_screenshot


def test_every_language_has_the_same_strings_and_a_caption_per_screen() -> None:
    keys = {lang: set(t) for lang, t in STRINGS.items()}
    assert keys["es"] == keys["en"]
    for t in STRINGS.values():
        assert len(t["captions"]) == len(SCREENS)


def test_screens_render_at_the_reference_size_and_frame_for_play() -> None:
    for draw in SCREENS:
        screen = draw(STRINGS["en"])
        assert screen.size == (W * K, H * K)
        assert screen.mode == "RGB"
    framed = framed_screenshot(screen, STRINGS["en"]["captions"][-1])
    assert max(framed.size) <= 2 * min(framed.size)


def test_the_longer_spanish_callout_still_renders() -> None:
    # "A 0.4 mm de perfecto" is wider than the English one: it shrinks to fit, like the HUD.
    assert near_miss(STRINGS["es"]).size == (W * K, H * K)
