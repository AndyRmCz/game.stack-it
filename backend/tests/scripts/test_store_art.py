"""Store art must meet Google Play's upload rules, or the Play Console rejects the file."""

from PIL import Image

from app.web.og import _font
from scripts.generate_app_icons import icon
from scripts.generate_store_art import (
    CAPTIONS,
    POSES,
    _wrap,
    feature_graphic,
    framed_screenshot,
    wordmark,
)


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


def test_an_app_store_screenshot_has_apples_exact_size_and_no_alpha() -> None:
    card = framed_screenshot(
        Image.new("RGB", (1170, 2532), "white"), "Stack to the sky.", (1320, 2868)
    )

    assert card.size == (1320, 2868)
    assert card.mode == "RGB"


def _phone(card: Image.Image) -> tuple[int, int, int]:
    """The red screenshot's top row and its left and right ends."""
    top = next(y for y in range(card.height) if card.getpixel((card.width // 2, y)) == (255, 0, 0))
    reds = [x for x in range(card.width) if card.getpixel((x, top + 40)) == (255, 0, 0)]
    return top, reds[0], reds[-1]


def _violet(pixel: tuple[int, int, int]) -> bool:
    r, g, b = pixel
    return b > 150 and b > r > g


def test_blocky_sits_on_the_phone_on_the_side_that_lets_it_look_at_it() -> None:
    shot = Image.new("RGB", (1080, 2400), (255, 0, 0))
    plain = framed_screenshot(shot, "Every millimetre counts.")
    top, left, right = _phone(plain)
    above_left, above_right = (left + 60, top - 40), (right - 60, top - 40)
    assert not _violet(plain.getpixel(above_left)) and not _violet(plain.getpixel(above_right))

    happy = framed_screenshot(shot, "Every millimetre counts.", pose="happy")
    assert _violet(happy.getpixel(above_right)) and not _violet(happy.getpixel(above_left))
    nervous = framed_screenshot(shot, "Every millimetre counts.", pose="nervous")  # looks right
    assert _violet(nervous.getpixel(above_left)) and not _violet(nervous.getpixel(above_right))


def test_a_caption_too_long_for_a_line_breaks_into_two_even_ones() -> None:
    font = _font("Sora-Bold.ttf", 88)
    lines = _wrap("One tap. Stack to the sky.", font, 880)
    assert lines == ["One tap. Stack", "to the sky."]
    assert _wrap("Every millimetre counts.", font, 2000) == ["Every millimetre counts."]


def test_every_caption_has_a_blocky_pose() -> None:
    for captions in CAPTIONS.values():
        assert len(captions) == len(POSES)
