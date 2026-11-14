"""The art kit places Blocky's baked stills where the art asks, and every pose it uses exists."""

import pytest
from PIL import Image

from scripts import art, generate_screen_mockups, generate_store_art

GOLD_SEEN = 200  # a pixel this red and green, and little blue, is the slab


def _canvas() -> Image.Image:
    return Image.new("RGBA", (600, 600))


def _is_gold(pixel: tuple[int, ...]) -> bool:
    r, g, b, a = pixel
    return a > 200 and r > GOLD_SEEN and g > 120 and b < 120


@pytest.mark.parametrize(
    "pose",
    sorted({*generate_store_art.POSES, *generate_screen_mockups.POSES, "wonder", "happy"}),
)
def test_every_pose_the_art_uses_is_baked(pose: str) -> None:
    still = Image.open(art.STILLS / f"{pose}.png")
    assert still.mode == "RGBA"
    assert still.width == round(art.PER_BLOCK * 520 / 480)  # BlockyGlass's width, 520 at 480


def test_blocky_stands_on_its_foot_centred_and_block_wide() -> None:
    image = _canvas()
    art.blocky(image, 300, 500, 200, "happy", glow=False)

    left, _, right, bottom = image.getchannel("A").point(lambda v: v > 128 and 255).getbbox()
    assert abs(bottom - 500) <= 2
    assert abs((left + right) / 2 - 300) <= 2
    assert 195 <= right - left <= 215  # the block, give or take its bevel


def test_the_hat_lands_a_hair_right_of_centre_on_the_head() -> None:
    image = _canvas()
    art.blocky(image, 300, 500, 200, "wonder", glow=False, hat=True)

    gold = [
        (x, y)
        for y in range(0, 500, 2)
        for x in range(0, 600, 2)
        if _is_gold(image.getpixel((x, y)))
    ]
    xs, ys = [x for x, _ in gold], [y for _, y in gold]
    assert (min(xs) + max(xs)) / 2 > 300 + 200 * art.HAT_OFF * 0.6  # off centre, to the right
    assert max(ys) < 500 - 200 * 0.8  # on the head, not beside it


def test_the_glow_spreads_violet_light_around_blocky() -> None:
    plain, lit = _canvas(), _canvas()
    art.blocky(plain, 300, 500, 200, "happy", glow=False)
    art.blocky(lit, 300, 500, 200, "happy")

    beside = (300 + 118, 400)  # just past the glass's right edge
    assert plain.getpixel(beside)[3] == 0
    r, g, b, a = lit.getpixel(beside)
    assert a > 0 and b > r > g


def test_the_night_backdrop_has_stars_and_the_day_one_does_not() -> None:
    def specks(look: str) -> int:
        """Pixels in the top half much brighter than their left neighbour: the sky and its glow
        are smooth, a star isn't."""
        image = art.backdrop((900, 900), look).convert("L")
        return sum(
            1
            for y in range(450)
            for x in range(1, 900)
            if image.getpixel((x, y)) - image.getpixel((x - 1, y)) > 25
        )

    assert specks("neon") > 20
    assert specks("pop") == 0
