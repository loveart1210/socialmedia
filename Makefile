# Makefile gốc — điều phối 2 subproject
# Yêu cầu: dotnet SDK 10, pnpm, dotnet-ef (dotnet tool install -g dotnet-ef)

API_DIR := socialmedia_api
WEB_DIR := socialmedia_web

.PHONY: dev-api dev-web \
        build-api test-api format-api migrate-api \
        typecheck-web lint-web build-web \
        check

# ── Chạy dev ──────────────────────────────────────────────

dev-api:
	dotnet watch --project $(API_DIR)/src/SocialMedia.Api

dev-web:
	pnpm --dir $(WEB_DIR) dev

# ── API (.NET) ────────────────────────────────────────────

build-api:
	dotnet build $(API_DIR) -warnaserror

test-api:
	dotnet test $(API_DIR)

format-api:
	dotnet format $(API_DIR) --verify-no-changes

# Áp migration lên DB local (connection string lấy từ appsettings.Development.json)
migrate-api:
	dotnet ef database update --project $(API_DIR)/src/SocialMedia.Api

# ── Web (Next.js) ─────────────────────────────────────────

typecheck-web:
	pnpm --dir $(WEB_DIR) exec tsc --noEmit

lint-web:
	pnpm --dir $(WEB_DIR) lint

build-web:
	pnpm --dir $(WEB_DIR) build

# ── Tổng kiểm tra trước khi commit ────────────────────────

check: build-api format-api test-api typecheck-web lint-web
