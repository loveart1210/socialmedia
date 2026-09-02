# Makefile gốc — điều phối 2 subproject
# Yêu cầu: dotnet SDK 10, pnpm, Docker. dotnet-ef KHÔNG cài global — nó là
# local tool khai trong dotnet-tools.json, lấy về bằng `make tools-restore`.

API_DIR := socialmedia_api
WEB_DIR := socialmedia_web

# Web chỉ tồn tại từ Phase 7 — các target web tự bỏ qua chừng nào chưa có.
WEB_READY := $(wildcard $(WEB_DIR)/package.json)

.PHONY: up down tools-restore \
        dev-api dev-web \
        build-api test-api format-api migrate-api \
        typecheck-web lint-web build-web \
        check

# ── Hạ tầng local (Postgres + Redis + MinIO + MailHog) ────
# Chỉ chạy bằng Docker. Máy nào cài sẵn PostgreSQL (kể cả trong WSL) phải tắt
# service đó trước, nếu không localhost:5432 sẽ trỏ về hai DB khác nhau tùy
# chỗ gọi mà không báo lỗi gì.

up:
	docker compose up -d

down:
	docker compose down

tools-restore:
	dotnet tool restore

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
migrate-api: tools-restore
	dotnet ef database update --project $(API_DIR)/src/SocialMedia.Api

# ── Web (Next.js) ─────────────────────────────────────────

typecheck-web:
	pnpm --dir $(WEB_DIR) exec tsc --noEmit

lint-web:
	pnpm --dir $(WEB_DIR) lint

build-web:
	pnpm --dir $(WEB_DIR) build

# ── Tổng kiểm tra trước khi commit ────────────────────────
# Chừng nào socialmedia_web chưa tồn tại, check chỉ chạy phần API.

check: build-api format-api test-api
ifeq ($(WEB_READY),)
	@echo "check: bỏ qua typecheck-web + lint-web (chưa có $(WEB_DIR)/package.json)"
else
	$(MAKE) typecheck-web lint-web
endif
