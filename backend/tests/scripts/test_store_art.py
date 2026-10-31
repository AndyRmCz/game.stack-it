"""Store art must meet Google Play's upload rules, or the Play Console rejects the file."""

from PIL import Image

from scripts.generate_app_icons import icon
from scripts.generate_store_art import feature_graphic, framed_screenshot, wordmark


def test_feature_graphic_is_1024x500_without_alpha() -> None:
    image = feature_graphic()
    assert image.size == (1024, 500)
    assert image.mode == "RGB"


def test_store_icon_is_a_square_that_scales_to_512() -> None:
    image = icon("neon")
    assert image.width == image.height
    assert image.resize((512, 512)).size == (512, 512)


def test_wordmark_is_ink_on_transparent() -> None:
    for look in ("neon", "pop"):
        mark = wordmark(look, 120)
        assert mark.mode == "RGBA"
        # The i-dot blocks are dot-sized: they barely add height to the word.
        assert mark.width > 1.8 * mark.height
        alpha = mark.getchannel("A")
        assert alpha.getextrema() == (0, 255)
        assert alpha.getpixel((0, 0)) < 40  # the corner is background (glow at most)


def test_a_20_by_9_phone_screenshot_is_framed_inside_plays_2_to_1_limit() -> None:
    shot = Image.new("RGB", (1080, 2400), (255, 0, 0))
    framed = framed_screenshot(shot, "Every millimetre counts.")
    assert framed.size == (1080, 1920)
    assert max(framed.size) <= 2 * min(framed.size)
    assert framed.mode == "RGB"
    # The screenshot itself is in the frame, not only the caption.
    assert framed.getpixel((540, 1300)) == (255, 0, 0)
