import hashlib
import json
import time

import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa
from jwt.algorithms import RSAAlgorithm

from app.clients.identity import IdentityTokenError, OidcVerifier

ISSUER = "https://appleid.apple.com"
AUDIENCE = "com.stackit.game"


def _keypair(kid: str):
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    jwk = json.loads(RSAAlgorithm.to_jwk(key.public_key()))
    jwk |= {"kid": kid, "alg": "RS256", "use": "sig"}
    return key, jwk


KEY, JWK = _keypair("k1")
OTHER_KEY, _ = _keypair("k1")


def _token(key=KEY, kid="k1", **overrides) -> str:
    now = int(time.time())
    claims = {"iss": ISSUER, "aud": AUDIENCE, "sub": "001234.abc", "iat": now, "exp": now + 600}
    claims |= overrides
    claims = {k: v for k, v in claims.items() if v is not None}
    return jwt.encode(claims, key, algorithm="RS256", headers={"kid": kid})


class FakeJwks:
    def __init__(self, keys: list[dict]) -> None:
        self.keys = keys
        self.calls = 0

    async def __call__(self) -> dict:
        self.calls += 1
        return {"keys": self.keys}


def _verifier(jwks: FakeJwks, hash_nonce: bool = True) -> OidcVerifier:
    return OidcVerifier(
        issuers=(ISSUER,), audiences=[AUDIENCE], fetch_jwks=jwks, hash_nonce=hash_nonce
    )


async def test_valid_token_returns_subject_and_caches_keys() -> None:
    jwks = FakeJwks([JWK])
    verifier = _verifier(jwks)

    first = await verifier.verify(_token(email="a@b.c"))
    await verifier.verify(_token())

    assert (first.subject, first.email) == ("001234.abc", "a@b.c")
    assert jwks.calls == 1


@pytest.mark.parametrize(
    ("token", "reason"),
    [
        (_token(aud="com.someone.else"), "audience"),
        (_token(iss="https://evil.example"), "issuer"),
        (_token(exp=int(time.time()) - 120), "expired"),
        (_token(key=OTHER_KEY), "signature"),
        (_token(sub=None), "missing sub"),
    ],
)
async def test_rejects_untrustworthy_tokens(token: str, reason: str) -> None:
    with pytest.raises(IdentityTokenError):
        await _verifier(FakeJwks([JWK])).verify(token)


async def test_unknown_key_id_refetches_once_for_rotation() -> None:
    new_key, new_jwk = _keypair("k2")
    jwks = FakeJwks([JWK])
    verifier = _verifier(jwks)
    await verifier.verify(_token())
    jwks.keys = [JWK, new_jwk]

    identity = await verifier.verify(_token(key=new_key, kid="k2"))

    assert identity.subject == "001234.abc"
    assert jwks.calls == 2


async def test_apple_nonce_is_compared_hashed() -> None:
    raw = "client-generated-nonce"
    token = _token(nonce=hashlib.sha256(raw.encode()).hexdigest())
    verifier = _verifier(FakeJwks([JWK]), hash_nonce=True)

    assert (await verifier.verify(token, nonce=raw)).subject
    with pytest.raises(IdentityTokenError):
        await verifier.verify(token, nonce="replayed-nonce")


async def test_google_nonce_is_compared_raw() -> None:
    token = _token(nonce="n-123")
    verifier = _verifier(FakeJwks([JWK]), hash_nonce=False)

    assert (await verifier.verify(token, nonce="n-123")).subject
    with pytest.raises(IdentityTokenError):
        await verifier.verify(_token(), nonce="n-123")  # nonce expected but absent
