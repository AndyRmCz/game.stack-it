from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse


class AppError(Exception):
    """Business error with a stable machine-readable `code` the client can switch on."""

    status_code = 400
    code = "bad_request"

    def __init__(
        self, code: str | None = None, message: str | None = None, headers: dict | None = None
    ) -> None:
        self.code = code or self.code
        self.message = message or self.code.replace("_", " ")
        self.headers = headers
        super().__init__(self.message)


class BadRequest(AppError):
    status_code = 400


class Unauthorized(AppError):
    status_code = 401
    code = "unauthorized"


class Forbidden(AppError):
    status_code = 403
    code = "forbidden"


class NotFound(AppError):
    status_code = 404
    code = "not_found"


class Conflict(AppError):
    status_code = 409
    code = "conflict"


class Gone(AppError):
    status_code = 410
    code = "gone"


class RateLimited(AppError):
    status_code = 429
    code = "rate_limited"


class ServiceUnavailable(AppError):
    status_code = 503
    code = "service_unavailable"


def install_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(AppError)
    async def _app_error(_: Request, exc: AppError) -> JSONResponse:
        return JSONResponse(
            status_code=exc.status_code,
            content={"error": {"code": exc.code, "message": exc.message}},
            headers=exc.headers,
        )
