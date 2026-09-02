<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **socialmedia** (171 symbols, 167 relationships, 0 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact analysis before editing.** Use `impact({target: "symbolName", direction: "upstream"})` (MCP) or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .` (CLI fallback); report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "master"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "master" --repo .`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/socialmedia/context` | Codebase overview, check index freshness |
| `gitnexus://repo/socialmedia/clusters` | All functional areas |
| `gitnexus://repo/socialmedia/processes` | All execution flows |
| `gitnexus://repo/socialmedia/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->

# CLAUDE.md

Dự án mạng xã hội (đồ án môn Phân tích thiết kế HTTT, triển khai thật lên
production). Monorepo 2 subproject: `socialmedia_api` (.NET 10 + PostgreSQL +
Redis) và `socialmedia_web` (Next.js 15).

## Đọc trước khi làm

| File | Khi nào đọc |
|---|---|
| `.claude/rules/ARCHITECTURE.md` | **Luôn luôn** — ranh giới service, hợp đồng HTTP, module, luật bất biến |
| `.claude/rules/api.md` | Khi tạo/sửa bất cứ gì trong `socialmedia_api` |
| `.claude/rules/web.md` | Khi tạo/sửa bất cứ gì trong `socialmedia_web` |
| `docs/SPEC.md` | **Nguồn sự thật nghiệp vụ** — schema từng cột, business rule BR-*, ma trận quyền, acceptance criteria |
| `docs/ROADMAP.md` | Trước mỗi task — biết đang ở bước nào, tiêu chí nghiệm thu của bước đó |

Nếu việc đang làm mâu thuẫn với docs (ví dụ phải thêm module, đổi contract):
**dừng lại hỏi trước**, không tự quyết rồi để docs lệch. Khi được duyệt, sửa
docs trong **cùng commit** với code.

## Trạng thái hiện tại

Repo mới có docs + Makefile, **chưa có code**. Giai đoạn đầu chỉ làm:
solution dotnet + database (EF Core migration) + API contract trên Swagger.
Chưa dựng frontend, chưa viết nghiệp vụ đầy đủ — đừng tự ý scaffold
`socialmedia_web` hay viết logic ngoài phạm vi được giao.

## Lệnh

```bash
make up               # docker compose up -d (Postgres + Redis + MinIO + MailHog)
make down             # dừng hạ tầng local
make tools-restore    # lấy local tool (dotnet-ef) theo dotnet-tools.json
make dev-api          # dotnet watch
make dev-web          # next dev (chưa dùng ở giai đoạn này)
make migrate-api      # dotnet ef database update lên DB local
make check            # build-api + format-api + test-api (+ web khi đã có)
```

- **`make check` phải pass trước khi kết thúc mọi task.** Chừng nào
  `socialmedia_web` chưa tồn tại, `check` tự bỏ qua phần web — không phải nhớ
  chạy tay từng target nữa.
- **Hạ tầng local chỉ đến từ `docker compose`**, không cài trực tiếp lên máy.
  Máy nào đã cài sẵn PostgreSQL (kể cả **trong WSL**) phải tắt hẳn service đó
  (`sudo systemctl disable --now postgresql`): Windows không thấy socket của
  WSL trong netstat nên container vẫn bind 5432 thành công, sau đó
  `localhost:5432` từ Windows và từ trong WSL trỏ về **hai DB khác nhau** mà
  không báo lỗi gì.
- `dotnet-ef` là **local tool** (khai trong `dotnet-tools.json`), không cài
  global — cả nhóm dùng chung version.
- Migration mới phải `make migrate-api` chạy thử trên DB local trước khi
  commit; review SQL sinh ra, không commit mù.

## Luật bất biến (tóm tắt — chi tiết trong `.claude/`)

- Chỉ `socialmedia_api` chạm PostgreSQL. Mọi route `/api/v1/<resource>`,
  mặc định cần Bearer token, public thì `[AllowAnonymous]`.
- Tổ chức theo feature folder `Modules/<X>`; nghiệp vụ nằm trong service của
  module sở hữu, module khác cần thì inject service đó — không tạo
  `Shared/`/`Core/`, không query chéo bảng của module khác.
- Entity: tên bảng/cột **theo đúng `docs/SPEC.md` mục 3** — snake_case,
  **không prefix domain** (`users`, `posts`, `comments`…). Guid v7 mặc định,
  trừ các composite PK SPEC đã chỉ định.
- **Hai cơ chế "xoá", chọn theo SPEC**: entity có cột `status`
  (`posts`/`users`/`reports`) dùng `status` và **không** đặt global query
  filter (bài `hidden` tác giả vẫn thấy — BR-07, lọc trong service); entity
  không có `status` (`comments`…) dùng `DeletedAt` + global query filter.
- Không lưu trạng thái suy được từ dữ liệu khác — **trừ hai ngoại lệ SPEC chỉ
  định**: `posts.comment_count` và `posts.reaction_counts` (jsonb), cập nhật
  cùng transaction với bản ghi thật + job đối soát.
- **RBAC động**: danh sách permission cố định trong code (mỗi permission phải
  có chỗ kiểm), nhưng role và tập permission của role **cấu hình được lúc
  chạy** qua màn Admin. Kiểm quyền bằng `[HasPermission("...")]`, không phải
  `[Authorize(Roles = ...)]`; permission **không** nhét vào JWT, đọc từ cache
  Redis. Một user giữ đúng một role.
- Field không khai trong DTO → request 400 (không bỏ qua). Không trả entity
  ra ngoài, luôn map sang record response.
- Không tin dữ liệu từ client: `userId` lấy từ claim `sub`, độ sâu reply
  (≤ 3) và giới hạn ảnh (≤ 10MB, ≤ 10 ảnh/bài) kiểm ở server.
- Lỗi trả ProblemDetails qua middleware chung; không tự dựng response lỗi.
- Job nền chỉ mang id, consumer đọc lại từ DB.

## Quyết định đã chốt (không hỏi lại, chi tiết trong SPEC/ROADMAP)

| Chủ đề | Chốt |
|---|---|
| Hạ tầng local | `postgres:16.15-alpine` (pin cả minor) + Redis + MinIO + MailHog, chỉ qua `docker compose` |
| Solution | **một** file `socialmedia_api/SocialMedia.sln`; web dùng `package.json`, không có `.sln` |
| Versioning | `Asp.Versioning.Mvc` + `.ApiExplorer`; `/api/health` là `[ApiVersionNeutral]` |
| OpenAPI | Swashbuckle.AspNetCore, UI ở `/docs`, chỉ bật Development |
| Phân quyền | **RBAC động** — 1 user = 1 role; permission cố định trong code, `role_permissions` sửa được lúc chạy |
| Object storage | một adapter `AWSSDK.S3` cho cả hai môi trường: local **MinIO**, prod **Cloudflare R2** (`ForcePathStyle = true`, `region = auto`) |
| Ảnh | magic bytes → **re-encode bằng SixLabors.ImageSharp** → đẩy storage; phục vụ qua pre-signed URL |
| Avatar | cột `profiles.avatar_key` — lưu **key trong bucket**, không lưu URL (pre-signed URL có hạn) |
| Cảm xúc | 6 loại: `like, love, haha, wow, sad, angry` |
| Mật khẩu | BCrypt cost 12; có xác minh email **và** đặt lại mật khẩu (UC-21), dùng chung bảng token có cột `purpose` |
| `audit_logs` | append-only bằng **trigger `RAISE EXCEPTION`** trong migration (REVOKE vô nghĩa khi app chạy bằng role owner) |

## Quy ước làm việc

- Làm theo **lát cắt dọc nhỏ**: mỗi task một module hoặc một nhóm endpoint,
  chạy được và verify được trên Swagger (`/docs`, chỉ bật ở development)
  trước khi sang task sau.
- Commit message tiếng Anh, dạng `<type>(<scope>): <summary>` —
  ví dụ `feat(api): add posts module with soft delete`.
- Không tự thêm package ngoài danh sách đã duyệt mà chưa hỏi. Danh sách:
  EF Core + Npgsql · FluentValidation · StackExchange.Redis · SignalR ·
  JwtBearer · **Asp.Versioning.Mvc + .ApiExplorer** · **Swashbuckle.AspNetCore** ·
  **BCrypt.Net-Next** · **MailKit** · **AWSSDK.S3** · **SixLabors.ImageSharp** ·
  test: **xunit + Microsoft.AspNetCore.Mvc.Testing + Testcontainers**.
  (k6 dùng để đo tải, không phải NuGet.)
- Secret/connection string nằm trong `appsettings.Development.json` (local)
  và biến môi trường (prod qua Dokploy) — không hardcode, không commit secret.
- Trả lời và comment trao đổi bằng tiếng Việt; tên biến/code/commit bằng
  tiếng Anh.
- GitNexus: tạo file/symbol MỚI thì không cần impact analysis; sửa symbol
  ĐÃ CÓ thì phải chạy. Sau mỗi task tạo module mới, re-index:
  `node .gitnexus/run.cjs analyze --index-only`.
