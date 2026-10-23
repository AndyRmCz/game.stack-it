from typing import Literal

Lang = Literal["en", "es"]

STRINGS: dict[str, dict[Lang, str]] = {
    "og_title": {
        "en": "Can you beat {name}'s {score}?",
        "es": "¿Puedes superar los {score} de {name}?",
    },
    "og_description": {
        "en": "Same blocks, same speed. Stack higher in Stack IT.",
        "es": "Mismos bloques, misma velocidad. Apila más alto en Stack IT.",
    },
    "eyebrow": {"en": "Stack IT · Challenge", "es": "Stack IT · Reto"},
    "headline_pre": {"en": "{name} stacked", "es": "{name} apiló"},
    "headline_post": {"en": "Your turn.", "es": "Te toca."},
    "lede": {
        "en": "{n} blocks high on the exact same block sequence you will get. Beat the score.",
        "es": "{n} bloques de altura con la misma secuencia de bloques que te tocará. Supera la puntuación.",
    },
    "ghost": {"en": "@{name}", "es": "@{name}"},
    "tower_alt": {"en": "{name}'s tower, {n} blocks high", "es": "Torre de {name}, {n} bloques"},
    "open_app": {"en": "Open in Stack IT", "es": "Abrir en Stack IT"},
    "score_to_beat": {"en": "Score to beat", "es": "Puntos a superar"},
    "og_beat": {"en": "Beat my score", "es": "Supera mi puntuación"},
    "og_generic": {"en": "Can you beat me?", "es": "¿Me superas?"},
    "play_store": {"en": "Get it on Google Play", "es": "Descárgalo en Google Play"},
    "app_store": {"en": "Download on the App Store", "es": "Descárgalo en el App Store"},
    "code_hint": {
        "en": "Already have the app? Enter code",
        "es": "¿Ya tienes la app? Usa el código",
    },
    "expired": {
        "en": "This challenge has expired, but the tower is still standing. Get the game and start your own.",
        "es": "Este reto ha caducado, pero la torre sigue en pie. Descarga el juego y empieza el tuyo.",
    },
    "not_found_title": {"en": "Challenge not found", "es": "Reto no encontrado"},
    "not_found_body": {
        "en": "Check the link or the code. If it was shared a while ago, it may have been removed.",
        "es": "Revisa el enlace o el código. Si se compartió hace tiempo, puede que ya no exista.",
    },
    "get_game": {"en": "Get Stack IT", "es": "Descargar Stack IT"},
    "privacy": {"en": "Privacy", "es": "Privacidad"},
    "terms": {"en": "Terms", "es": "Términos"},
}


def t(lang: Lang, key: str, **params: object) -> str:
    return STRINGS[key][lang].format(**params)


def pick_lang(accept_language: str | None, override: str | None = None) -> Lang:
    if override in ("en", "es"):
        return override  # type: ignore[return-value]
    for part in (accept_language or "").split(","):
        code = part.split(";")[0].strip().lower()[:2]
        if code in ("en", "es"):
            return code  # type: ignore[return-value]
    return "en"
