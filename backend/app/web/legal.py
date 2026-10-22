"""Privacy policy and terms. Plain-language starting point: have them reviewed before launch."""

from app.web.i18n import Lang

PRIVACY: dict[Lang, tuple[str, list[tuple[str, str]]]] = {
    "en": (
        "Privacy Policy",
        [
            (
                "What we collect",
                "A random device identifier and secret to create your guest account; your display "
                "name and friend code; your runs (seed, tap timings, scores); coins, skins and "
                "daily streak; and, if you link one, the account ID and email returned by Google "
                "or Apple sign-in.",
            ),
            (
                "Why",
                "To run the game: save progress, verify scores, show friends leaderboards and "
                "challenges, and keep the economy fair. We do not sell personal data.",
            ),
            (
                "Advertising",
                "The app shows ads through Google AdMob. Where required (EEA, UK, Switzerland) we "
                "ask for consent before personalized ads; on iOS we ask for tracking permission. "
                "Google may collect device identifiers as described in its own policy.",
            ),
            (
                "Sharing clips",
                "Gameplay clips are recorded and kept on your device. They leave it only when you "
                "share them through your phone's share sheet.",
            ),
            (
                "Retention and deletion",
                "Data is kept while your account exists. Settings → Delete account permanently "
                "removes your account and all associated data from our servers.",
            ),
            ("Contact", "Questions or requests: {support_email}."),
        ],
    ),
    "es": (
        "Política de privacidad",
        [
            (
                "Qué recopilamos",
                "Un identificador y un secreto aleatorios del dispositivo para crear tu cuenta de "
                "invitado; tu nombre visible y código de amigo; tus partidas (semilla, tiempos de "
                "toque, puntuaciones); monedas, aspectos y racha diaria; y, si la vinculas, el ID "
                "de cuenta y el correo que devuelve el inicio de sesión de Google o Apple.",
            ),
            (
                "Para qué",
                "Para que el juego funcione: guardar tu progreso, verificar puntuaciones, mostrar "
                "clasificaciones y retos de amigos y mantener una economía justa. No vendemos "
                "datos personales.",
            ),
            (
                "Publicidad",
                "La app muestra anuncios mediante Google AdMob. Donde la ley lo exige (EEE, Reino "
                "Unido, Suiza) pedimos tu consentimiento para anuncios personalizados; en iOS "
                "pedimos permiso de seguimiento. Google puede recopilar identificadores del "
                "dispositivo según su propia política.",
            ),
            (
                "Clips compartidos",
                "Los clips de partida se graban y se guardan en tu dispositivo. Solo salen de él "
                "cuando los compartes desde el menú de compartir del teléfono.",
            ),
            (
                "Conservación y eliminación",
                "Conservamos los datos mientras exista tu cuenta. Ajustes → Eliminar cuenta borra "
                "de forma permanente tu cuenta y todos sus datos de nuestros servidores.",
            ),
            ("Contacto", "Preguntas o solicitudes: {support_email}."),
        ],
    ),
}

TERMS: dict[Lang, tuple[str, list[tuple[str, str]]]] = {
    "en": (
        "Terms of Use",
        [
            ("The game", "Stack IT is provided as is, for personal entertainment."),
            (
                "Fair play",
                "Scores are verified on our servers. Modified clients, automation or manipulated "
                "submissions are not allowed; affected runs are removed and accounts may be "
                "suspended.",
            ),
            (
                "Virtual items",
                "Coins and skins have no real-world value, cannot be exchanged for money and may "
                "be adjusted to correct errors.",
            ),
            (
                "Your name",
                "Display names are visible to friends and on challenge pages. Do not use names "
                "that are offensive or impersonate others.",
            ),
            ("Contact", "{support_email}"),
        ],
    ),
    "es": (
        "Términos de uso",
        [
            ("El juego", "Stack IT se ofrece tal cual, para entretenimiento personal."),
            (
                "Juego limpio",
                "Las puntuaciones se verifican en nuestros servidores. No se permiten clientes "
                "modificados, automatizaciones ni envíos manipulados; esas partidas se eliminan y "
                "las cuentas pueden suspenderse.",
            ),
            (
                "Objetos virtuales",
                "Las monedas y aspectos no tienen valor real, no se pueden canjear por dinero y "
                "pueden ajustarse para corregir errores.",
            ),
            (
                "Tu nombre",
                "Tu nombre visible lo ven tus amigos y las páginas de retos. No uses nombres "
                "ofensivos ni te hagas pasar por otras personas.",
            ),
            ("Contacto", "{support_email}"),
        ],
    ),
}

DELETION: dict[Lang, tuple[str, list[tuple[str, str]]]] = {
    "en": (
        "Delete your Stack IT account",
        [
            (
                "In the app",
                "Open Stack IT → Settings → Delete account and confirm. Your account, scores, "
                "friends, coins and skins are removed from our servers immediately.",
            ),
            (
                "Without the app",
                "Email {support_email} from the address linked to your Google or Apple sign-in, or "
                "include your friend code (Settings and Friends screens). We delete the account "
                "within 30 days and confirm by email.",
            ),
            (
                "What is kept",
                "Nothing tied to you. Challenge links you shared stop working.",
            ),
        ],
    ),
    "es": (
        "Eliminar tu cuenta de Stack IT",
        [
            (
                "Desde la app",
                "Abre Stack IT → Ajustes → Eliminar cuenta y confirma. Tu cuenta, puntuaciones, "
                "amigos, monedas y aspectos se borran de nuestros servidores al instante.",
            ),
            (
                "Sin la app",
                "Escribe a {support_email} desde el correo vinculado a tu inicio de sesión de Google "
                "o Apple, o incluye tu código de amigo (pantallas Ajustes y Amigos). Eliminamos la "
                "cuenta en un plazo de 30 días y lo confirmamos por correo.",
            ),
            (
                "Qué se conserva",
                "Nada vinculado a ti. Los enlaces de reto que compartiste dejan de funcionar.",
            ),
        ],
    ),
}
