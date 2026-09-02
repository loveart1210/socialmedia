# ROADMAP — Lộ trình xây dựng SocialMedia

> Mỗi bước = một task giao cho Claude Code (hoặc tự làm). Nguồn nghiệp vụ của MỌI bước:
> `docs/SPEC.md` (schema mục 3, BR-* mục 2, phân quyền mục 4, acceptance mục 5, enum mục 7) — đưa
> nguyên văn AC/BR liên quan vào prompt làm tiêu chí nghiệm thu. Nghi thức chung: đọc
> CLAUDE.md + `.claude/rules/` → làm → chạy tiêu chí nghiệm thu → `make check` (phần đã có)
> → re-index GitNexus (`node .gitnexus/run.cjs analyze --index-only`) → commit.
> Lệch docs thì sửa docs trong cùng commit.

---

## Phase 0 — Nền móng (chưa có nghiệp vụ)

### 0.1. docker-compose.yml
- **Làm gì:** `postgres:16.15-alpine` (volume, 5432) + `redis:7` (6379) + **MailHog** (SMTP dev, UI 8025) + **MinIO** (9000 + console 9001).
- **Cách làm:** Claude Code tự quyết chi tiết (password dev, tên db). `make up`. Pin cả minor của Postgres để dev khớp prod.
- **Trước khi chạy:** máy nào đã cài sẵn PostgreSQL — **kể cả trong WSL** — phải tắt hẳn (`sudo systemctl disable --now postgresql`). Windows không thấy socket của WSL trong netstat nên container vẫn bind 5432 thành công, sau đó `localhost:5432` từ Windows và từ trong WSL trỏ về **hai DB khác nhau** mà không báo lỗi gì.
- **Đầu ra:** DBeaver kết nối được Postgres; `redis-cli ping` → PONG; MailHog UI và MinIO console mở được.

### 0.2. Solution + skeleton
- **Làm gì:** **`socialmedia_api/SocialMedia.sln`** (đặt trong `socialmedia_api/`, không ở gốc — Makefile gọi `dotnet build socialmedia_api`), project `socialmedia_api/src/SocialMedia.Api`, project test `socialmedia_api/tests/SocialMedia.Api.Tests`, cây `Modules/`, `Common/`, `Infra/`, `.editorconfig`. Web không có `.sln` — `package.json` đóng vai trò đó, dựng ở Phase 7.
- **Cách làm:** Bám api.md mục 1. Package: EF Core + Npgsql, FluentValidation, **Asp.Versioning.Mvc + .ApiExplorer**, **Swashbuckle.AspNetCore**, JwtBearer, StackExchange.Redis, **BCrypt.Net-Next**.
- **Đầu ra:** `make build-api`, `make format-api`, `make test-api` (test rỗng) pass; đường dẫn khớp Makefile.

### 0.3. Program.cs — quy ước toàn cục
- **Làm gì:** Prefix `/api` + **URI versioning bằng `Asp.Versioning.Mvc`** (route `api/v{version:apiVersion}/[controller]`, `/api/health` gắn `[ApiVersionNeutral]`), Swashbuckle sinh document **theo từng version** qua `ApiExplorer` · JWT (HS256, TTL 15p, claims `sub`/`role`/`jti`) + fallback policy · Swagger UI `/docs` chỉ Development · `UnmappedMemberHandling = Disallow` + `JsonStringEnumConverter` · exception middleware → ProblemDetails · `AddValidatorsFromAssembly` · CORS `credentials: true` · `AddRateLimiter` cho nhóm auth + đăng bài/bình luận (SPEC mục 6).
- **Cách làm:** Hành vi theo ARCHITECTURE.md mục 3 + api.md mục 3/4/6; hiện thực Claude Code tự chọn. Secret dev trong `appsettings.Development.json`.
- **Đầu ra:** `make dev-api` chạy; route bất kỳ chưa đăng nhập → 401 ProblemDetails; `/docs` mở được ở dev.

### 0.4. Infra/Database — AppDbContext
- **Làm gì:** `AppDbContext` + `ApplyConfigurationsFromAssembly`; override `SaveChangesAsync` gán `CreatedAt/UpdatedAt`; convention global query filter `DeletedAt == null` cho entity có field này (posts/users/reports dùng `status`, KHÔNG áp filter — api.md mục 2).
- **Đầu ra:** Build pass; unit test chứng minh timestamps tự gán + query filter hoạt động (entity giả trong test).

### 0.5. Modules/Health
- **Làm gì:** `GET /api/health` (version-neutral, `[AllowAnonymous]`), check Postgres + Redis.
- **Đầu ra:** `curl /api/health` → 200 khi docker up, lỗi rõ khi tắt Postgres.

### 0.5b. Test harness tích hợp
- **Làm gì:** `WebApplicationFactory<Program>` + Postgres thật cho test (**Testcontainers**, DB riêng mỗi lần chạy) + helper đăng nhập/lấy token. Điều kiện tiên quyết của mọi bước sau: từ 1.3 trở đi bước nào cũng yêu cầu test tự động.
- **Cách làm:** Package: xunit + Microsoft.AspNetCore.Mvc.Testing + Testcontainers. Migration chạy lúc khởi tạo container, không dùng DB dev.
- **Đầu ra:** Một test tích hợp mẫu gọi `/api/health` → 200 và một test gọi route bảo vệ không token → 401 (**TC-A01**); `make test-api` xanh.

### 0.6. Khớp docs
- **Làm gì:** Sửa Makefile nếu đường dẫn lệch; sửa CLAUDE.md "Trạng thái hiện tại".
- **Đầu ra:** `make check` (phần API) pass từ gốc repo; docs không nói dối.

### 0.7. CI
- **Làm gì:** GitHub Actions: `make check` + `make test-api` trên mỗi push/PR, có service Postgres/Redis (hoặc để Testcontainers tự dựng).
- **Cách làm:** Bắt buộc vì SPEC mục 6 và api.md mục 4 đều yêu cầu **TC-A01→A08 chạy trong CI**, không phải chạy tay.
- **Đầu ra:** PR đỏ khi test hỏng; badge/lần chạy xanh trên nhánh master. **Chốt Phase 0.**

---

## Phase 1 — Auth + Users (lát cắt chốt pattern)

> Lát quan trọng nhất: mọi module sau copy pattern từ đây — bạn review kỹ nhất ở phase này.
> Schema lấy nguyên từ SPEC.md 3.2 (`users`) + 3.4 (`profiles`, `refresh_tokens`, `roles`).

### 1.1. Entity + migration đầu tiên
- **Làm gì:** `User` (đủ cột SPEC 3.2: status 5 giá trị, `failed_login_count`, `locked_until`), `RefreshToken` (lưu **băm** token), `AuthToken` (SPEC 3.4a — dùng chung `email_verify`/`password_reset`, cột `purpose`), `Role` + `Permission` + `RolePermission` với seed idempotent theo SPEC mục 4/7, `Profile` (1-1, đủ cột SPEC 3.4a gồm `avatar_key`). Migration `Init`.
- **Cách làm:** Tên bảng không prefix; `CREATE EXTENSION citext` cho email; admin đầu tiên tạo từ biến môi trường (SPEC 3.7). `make migrate-api` → review SQL.
- **Đầu ra:** Bảng đúng tên/kiểu/constraint trong DBeaver; `roles` có 3 dòng, `permissions` đủ danh sách SPEC mục 7, `role_permissions` khớp ma trận seed.

### 1.2. Infra/Mail + đăng ký & xác minh email
- **Làm gì:** `MailService` (`Infra/Mail`, **MailKit** → MailHog ở dev); `POST /auth/register` tạo User (status `pending`) + Profile **cùng transaction** (SPEC 3.5), sinh `AuthToken` purpose `email_verify` (TTL 24h, lưu **băm**) rồi gửi email; `POST /auth/verify-email` → status `active`; gửi lại email xác minh.
- **Cách làm:** Tạm gửi mail **đồng bộ** ở phase này; ghi nợ "chuyển qua queue" vào ARCHITECTURE.md mục 7 — trả nợ ở bước 5.3 khi Infra/Queue đã có (api.md mục 7 là trạng thái đích).
- **Đầu ra:** Đăng ký trên Swagger → mail hiện trong MailHog → bấm verify → status `active`.

### 1.3. Login / Refresh / Logout + lockout
- **Làm gì:** `POST /auth/login` (BCrypt cost 12; chưa xác minh → **403**; sai mật khẩu → 401 không lộ email tồn tại + `failed_login_count++`; sai 5 lần/15p → **423** khóa 15p), `POST /auth/refresh` (rotation, reuse detection → thu hồi cả chuỗi), `POST /auth/logout` (thu hồi phiên), `GET /auth/me`. Refresh token = cookie httpOnly.
- **Đầu ra:** Toàn bộ **US-002/AC-01→04** (SPEC mục 5) pass trên Swagger; TC-A01, TC-A02 (SPEC mục 4) có test tự động.

### 1.4. Users — profile
- **Làm gì:** `GET /users/{id}`, `PATCH /users/me` (`Optional<T>`, các cột SPEC 3.4a), `PUT /users/me/avatar` (magic bytes, ≤10MB; ghi `profiles.avatar_key` — tạm trỏ thư mục local, Phase 3 nối storage thật).
- **Cách làm:** Lưu **key**, không lưu URL; response dựng URL từ key ngay từ giờ để 3.1 chỉ phải đổi cách dựng.
- **Đầu ra:** Sửa tên/bio/avatar qua Swagger; field lạ → 400.

### 1.5. Quên / đặt lại mật khẩu (UC-21)
- **Làm gì:** `POST /auth/forgot-password` (sinh `AuthToken` purpose `password_reset`, TTL 30 phút, gửi mail) và `POST /auth/reset-password` (đổi `password_hash` + `used_at` + **thu hồi mọi refresh token**, cùng transaction — SPEC 3.5).
- **Cách làm:** Tái dùng nguyên `MailService` + bảng `auth_tokens` của 1.2. Phản hồi cho email không tồn tại **giống hệt** email tồn tại (không lộ email đã đăng ký). Rate limit nhóm auth.
- **Đầu ra:** **US-021/AC-01→04** pass; token dùng lần hai → 400; refresh token cũ sau khi đổi mật khẩu → 401. Cập nhật CLAUDE.md trạng thái. **Chốt Phase 1.**

---

## Phase 2 — Quan hệ: Friendships + Follows + tìm kiếm

### 2.1. Friendships
- **Làm gì:** Entity `Friendship` — **composite PK `(user_min, user_max)` + CK `user_min < user_max`** (BR-03), trạng thái Pending/Accepted + migration. Endpoint: gửi/thu hồi lời mời, accept/decline (Pending→Accepted là **1 UPDATE** — SPEC 3.5), unfriend, danh sách bạn/lời mời. Export `AreFriendsAsync(a, b)` + **cache Redis TTL 60s** (SPEC 3.6).
- **Cách làm:** Chặn: tự kết bạn (400), gửi trùng (409), C chấp nhận lời mời của B (403).
- **Đầu ra:** **US-010/AC-01→04** pass trên Swagger với 2 tài khoản — **trừ vế "được thông báo" của AC-01**: Notifications tới 5.3 mới có, vế đó nghiệm thu lại ở 5.3.

### 2.2. Follows
- **Làm gì:** Entity `Follow` — PK `(follower, followee)` + CK `follower <> followee` + migration. Follow/unfollow, danh sách, đếm.
- **Đầu ra:** Follow/unfollow chạy; follow trùng → 409; tự follow → 400.

### 2.3. Tìm kiếm người dùng (UC-16)
- **Làm gì:** `GET /users/search?q=` — khớp tiền tố không dấu trên `display_name`.
- **Cách làm:** **GIN pg_trgm + unaccent** (SPEC 3.6) — index viết SQL thô trong migration (EF không tả được). **Bẫy:** `unaccent()` không phải hàm IMMUTABLE nên **không index trực tiếp được** — migration phải `CREATE EXTENSION pg_trgm, unaccent` rồi tạo một wrapper `IMMUTABLE` bọc `unaccent`, sau đó mới index trên biểu thức đó.
- **Đầu ra:** Tìm "nguyen" ra "Nguyễn Văn A"; explain plan dùng index (dán vào PR).

---

## Phase 3 — Posts + storage + feed

### 3.1. Infra/Storage
- **Làm gì:** `StorageService` — **một adapter `AWSSDK.S3` dùng cho cả hai môi trường**: local trỏ MinIO (docker-compose 0.1), prod trỏ **Cloudflare R2**. Phục vụ ảnh qua **pre-signed URL** (SPEC mục 1).
- **Cách làm:** MinIO là *server* S3-compatible, không phải SDK riêng — khác nhau đúng `ServiceUrl` + credential, cả hai đều `ForcePathStyle = true`, R2 dùng `region = auto`. Bind bằng `IOptions<StorageOptions>` + `ValidateOnStart`. **Bẫy:** URL ký từ MinIO trong docker mang host nội bộ → cấu hình public endpoint riêng để link mở được từ trình duyệt.
- **Đầu ra:** Test tích hợp: upload → pre-signed URL mở được. Chuyển `profiles.avatar_key` của 1.4 sang storage thật (chỉ đổi cách dựng URL, không đổi schema).

### 3.2. Posts + MediaAttachment
- **Làm gì:** Entity `Post` **đúng SPEC 3.3** (content ≤5000 CK, `privacy` public/friends/private, `status` published/hidden/deleted, `comment_count`, `reaction_counts` jsonb default `{}`), `MediaAttachment` (CK size ≤ 10485760) + migration. Tạo bài (BR-01: có chữ HOẶC ≥1 ảnh; ≤10 ảnh; tạo bài + media **atomically**), sửa, xóa (status→deleted), xem bài/danh sách theo user (cursor).
- **Cách làm:** Ảnh: magic bytes + **re-encode bằng SixLabors.ImageSharp** trước khi đẩy storage (vứt metadata và payload nhét kèm — SPEC mục 6), cột `media_attachments` theo SPEC 3.4a. Quyền xem đánh giá **tại thời điểm đọc** theo BR-02 qua `AreFriendsAsync`; bài `hidden` chỉ tác giả thấy kèm lý do (BR-07). Không đặt global filter — lọc status/privacy trong service.
- **Đầu ra:** **US-004/AC-01→04** pass — riêng vế "xuất hiện trên **feed** bạn bè" của AC-01 nghiệm thu ở 3.3, ở bước này chỉ cần bài hiện trong danh sách bài của tác giả. User lạ xem bài `friends` → 403; TC-A03 (PATCH bài người khác → 403) có test.

### 3.3. Newsfeed
- **Làm gì:** `GET /feed` — bài `published` của bạn bè + người follow, cursor `(created_at, id)`, index partial `WHERE status='published'` (SPEC 3.6), cache Redis 30s trang đầu.
- **Cách làm:** Fan-out-on-read. Chấp nhận trễ ≤ 5s (SPEC 3.5).
- **Đầu ra:** **US-008/AC-01→03** pass (bài friends của người lạ và bài hidden KHÔNG xuất hiện); log cho thấy cache hit.

### 3.3b. Đo tải feed (US-008/AC-04)
- **Làm gì:** Script **k6** bắn vào `GET /feed` với **100–200 VU**, trên dữ liệu seed ≥ 2.000 user / 20.000 bài; báo cáo p95.
- **Cách làm:** Mục tiêu đã hạ từ 1.000 user trong báo cáo xuống 100–200 VU (SPEC US-008/AC-04) — giá trị của bước này là **phát hiện thiếu index sớm**, không phải con số đẹp. Ghi rõ cấu hình máy đo kèm kết quả.
- **Đầu ra:** p95 ≤ 500ms ở mức VU đã chốt; nếu trượt thì có explain plan chỉ ra truy vấn chậm và index bù vào — sửa xong đo lại.

---

## Phase 4 — Comments + Reactions

### 4.1. Comments
- **Làm gì:** Entity `Comment` (`DeletedAt`, `parent_id` nullable) + migration. Tạo/sửa/xóa comment, reply, danh sách theo bài + reply theo comment (cursor).
- **Cách làm:** `AssertDepth` server-side — cấp 4 → 400 (BR-08). Xóa giữ nhánh: node trả về "Bình luận đã bị xóa" (query cây dùng `IgnoreQueryFilters()` có chủ đích — api.md mục 2). `comment_count` trên `posts` cập nhật **cùng transaction** (SPEC 3.5). Comment vào bài phải qua kiểm quyền xem bài (BR-02).
- **Đầu ra:** Reply 3 cấp OK, cấp 4 → 400; xóa comment cha vẫn thấy reply con; `comment_count` khớp thực tế.

### 4.2. Reactions
- **Làm gì:** Entity `Reaction` — **PK `(user_id, target_type, target_id)`** + migration. React/un-react; thả loại khác = **thay thế** (BR-05). `reaction_counts` jsonb cập nhật cùng transaction.
- **Cách làm:** `ReactionType` **6 giá trị** `like/love/haha/wow/sad/angry` và `TargetType` khai ở `Common/Enums` (SPEC mục 7) — khóa của `reaction_counts` chính là tên các giá trị này. Service kiểm target tồn tại (không FK đa hình). Response feed/comment đọc số đếm từ counter, không GroupBy mỗi request (api.md mục 5).
- **Đầu ra:** React bài + comment chạy; đổi loại không nhân đôi; counter khớp sau chuỗi react/un-react.

---

## Phase 5 — Realtime: Conversations + Notifications

### 5.1. Hạ tầng SignalR + Infra/Queue
- **Làm gì:** `AddSignalR().AddStackExchangeRedis(...)`, auth JWT qua `access_token` handshake, map `/hubs/chat`, `/hubs/notifications`. Dựng `Infra/Queue` (Redis Stream + `QueueConstants.cs` — api.md mục 8).
- **Đầu ra:** Client test nối hub bằng token hợp lệ; token sai → từ chối.

### 5.2. Conversations (SPEC: ENT-06/07, BR-06/09, US-015)
- **Làm gì:** Entity `Conversation` (**UQ(user_a, user_b) + CK a<b** — 1 hội thoại/cặp), `Message` (**`seq` UQ trong hội thoại; `client_msg_id` UQ idempotency; trạng thái Sent/Delivered/Seen**) + migration. REST: tạo/lấy hội thoại, lịch sử **cursor theo `seq`, không OFFSET** (SPEC 3.6). Hub: gửi/nhận realtime, cập nhật Delivered/Seen.
- **Cách làm:** Ghi tin + tăng seq + last_message **atomically** (SPEC 3.5). Chỉ bạn bè tạo hội thoại/nhắn tin; **unfriend → hội thoại chỉ đọc** (BR-09). Gửi trùng `client_msg_id` → trả bản cũ.
- **Đầu ra:** **US-015/AC-01→04** pass (kể cả B offline nhận lại khi online, idempotency, 403 khi hết bạn); TC-A04, TC-A07 có test.

### 5.3. Notifications
- **Làm gì:** Entity `Notification` (**UQ(recipient_id, group_key)** — gộp; partial index `WHERE is_read=false`) + migration. Sinh khi: lời mời kết bạn, được accept, reaction/comment vào bài mình. Danh sách + đánh dấu đã đọc + badge. Push qua hub. Fan-out qua Redis Stream + `NotificationFanoutWorker` (job chỉ mang id). **Trả nợ 1.2:** chuyển gửi email sang queue.
- **Đầu ra:** A react bài B → B nhận realtime + badge; upsert theo group_key không chèn trùng; tắt worker job chờ, bật lại xử lý nốt; email đăng ký vẫn tới MailHog (giờ qua queue).

---

## Phase 6 — Moderation + Admin (SPEC: UC-18/19/20, ENT-12/13)

### 6.1. Reports (UC-18)
- **Làm gì:** Entity `Report` (`reason_code` CK: spam/harassment/nudity/violence/other, trạng thái Open/Resolved/Dismissed **một chiều**) + migration. `POST /reports` (User báo cáo nội dung/người dùng).
- **Đầu ra:** Tạo report trên Swagger; reason_code ngoài danh sách → 400.

### 6.2. Kiểm duyệt + AuditLog (UC-19)
- **Làm gì:** Entity `AuditLog` (cột theo SPEC 3.4a, **append-only**). `GET /reports` (`report.read`), `PATCH /reports/{id}`: "ẩn nội dung" → post/comment `hidden` + Resolved; "bỏ qua" → Dismissed — **kết luận + audit_log cùng transaction** (SPEC 3.5); xử lý lại → 409.
- **Cách làm:** Append-only cài bằng **trigger `BEFORE UPDATE OR DELETE … RAISE EXCEPTION`** viết trong migration, **không** dùng REVOKE: app dev chạy bằng role owner nên REVOKE không chặn được gì và bạn sẽ tưởng là xong.
- **Đầu ra:** **US-019/AC-01→04** pass; TC-A06 có test; chạy `UPDATE audit_logs …` trong DBeaver bị từ chối.

### 6.3. Admin — người dùng & vai trò (UC-20)
- **Làm gì:** `PATCH /admin/users/{id}/lock|unlock` (status suspended), `PATCH /admin/users/{id}/role` (gán role), `GET /admin/audit-logs` — mọi thao tác ghi audit_log.
- **Cách làm:** Kiểm quyền bằng `[HasPermission("user.lock")]` / `role.assign` / `audit.read`, **không** `[Authorize(Roles=…)]`. Chặn user tự đổi role của chính mình (SPEC mục 4).
- **Đầu ra:** TC-A05 có test; user bị lock không đăng nhập được; audit ghi đủ ai/hành động/đối tượng.

### 6.4. RBAC động — hạ tầng phân quyền (SPEC mục 4)
- **Làm gì:** `[HasPermission("...")]` + `IAuthorizationPolicyProvider` động + `AuthorizationHandler<PermissionRequirement>` ở `Common`; đọc tập permission của user từ **cache Redis** (TTL 300s) fallback DB; API quản lý role: tạo/sửa/xoá role, gán permission cho role (`role.manage`).
- **Cách làm:** **Không** nhét permission vào JWT (token phình + đổi quyền phải chờ hết TTL 15 phút). Đổi role của user hoặc đổi `role_permissions` → **xoá cache ngay** trong cùng luồng, không chờ TTL. Hai chốt an toàn bắt buộc (SPEC mục 4): không gỡ `role.assign` khỏi role cuối cùng còn giữ nó, không xoá role `is_system`.
- **Đầu ra:** **TC-A08** có test — Admin gỡ permission khỏi role thì user thuộc role đó bị 403 **ngay**, không cần đăng nhập lại. Toàn bộ route quản trị/kiểm duyệt đã chuyển sang `[HasPermission]`, không còn `[Authorize(Roles=…)]` nào trong codebase.

### 6.5. Job định kỳ (trả nợ ARCHITECTURE.md mục 7 — SPEC 3.7)
- **Làm gì:** `BackgroundService` định kỳ: đối soát `comment_count`/`reaction_counts` với bản ghi thật; dọn `media_attachments` mồ côi; dọn `auth_tokens` hết hạn; dọn avatar cũ không còn ai trỏ tới. (Xóa cứng 90 ngày + ẩn danh PII 30 ngày: làm nếu còn thời gian, không thì giữ trong nợ.)
- **Đầu ra:** Làm lệch counter bằng tay trong DB → job chạy → counter đúng lại; log job đọc được.

> **Chốt backend.** Rà Swagger toàn bộ; TC-A01→A08 đều có test trong CI (dựng ở 0.7); cập nhật ARCHITECTURE.md + CLAUDE.md.

---

## Phase 7 — Frontend nền móng

### 7.1. Scaffold socialmedia_web
- **Làm gì:** `create-next-app` (App Router, TS, Tailwind), cây `features/`, `components/ui/`, `lib/` theo web.md mục 1.
- **Đầu ra:** `make dev-web`, `make typecheck-web`, `make lint-web` pass; `make check` chạy ĐỦ cả web.

### 7.2. lib/axios + auth
- **Làm gì:** `lib/axios.ts` (baseURL `/api/v1`, Bearer, token bridge, single-flight refresh), `AuthProvider`, slice `auth`: đăng ký (+ màn "kiểm tra email"), đăng nhập (xử lý **401 / 403 chưa xác minh / 423 bị khóa** — US-002), **quên mật khẩu + đặt lại mật khẩu** (US-021), guard route.
- **Đầu ra:** Đăng ký → verify qua MailHog → đăng nhập trên UI; quên mật khẩu → mail → đặt lại → đăng nhập bằng mật khẩu mới; F5 giữ phiên; hết hạn tự refresh.

### 7.3. UI kit tối thiểu
- **Làm gì:** Design token + primitive: `Button`, `Input`, `Modal`, `Toast`, `Avatar`, `Skeleton`, `Spinner`, `ConfirmDialog`, `DropdownMenu`, `ErrorPanel`, `ImageGrid`, `Lightbox`, `Badge`.
- **Đầu ra:** Trang demo đủ primitive; không hex thô; chốt xong điền tên token vào web.md.

---

## Phase 8 — Frontend theo lát tính năng

> Mỗi slice: `api/ + queryKeys + hooks + components + errors + index` (web.md mục 1), danh sách đủ 4 trạng thái (mục 5). Hành vi đối chiếu AC tương ứng trong SPEC mục 5.

### 8.1. `profile` — xem/sửa trang cá nhân, avatar (preview + chặn sớm >10MB).
### 8.2. `friends` — tìm user (UC-16), lời mời, danh sách bạn; optimistic accept/decline.
### 8.3. `post` + `feed` — composer (chữ HOẶC ảnh — BR-01; chọn privacy **3 mức**), `useInfiniteQuery` + sentinel; bài `hidden` của mình hiển thị kèm lý do (BR-07).
### 8.4. `comments` + `reactions` — cây 3 cấp, node "Bình luận đã bị xóa", react optimistic + rollback.
### 8.5. `chat` — `SignalRProvider` + `useChatHub`; trạng thái Sent/Delivered/Seen; gửi kèm `clientMsgId` (retry an toàn); hội thoại hết bạn hiển thị **chỉ đọc** (BR-09); reconnect → invalidate hội thoại mở.
### 8.6. `notifications` — chuông + badge chưa đọc (realtime), danh sách gộp, đánh dấu đã đọc.
### 8.7. `moderation` — màn Moderator: hàng đợi report, xử lý ẩn/bỏ qua; màn Admin: khóa user, gán role, **quản lý role + tick permission cho role** (RBAC động 6.4). Route guard đọc `role` trong JWT chỉ để **ẩn/hiện menu** — quyền thật do server quyết, FE không tự suy.

> **Chốt frontend.** Đi lại toàn bộ user journey: đăng ký → verify → kết bạn → đăng bài → tương tác → chat → thông báo → báo cáo → kiểm duyệt.

---

## Phase 9 — Deploy production (Dokploy)

### 9.1. Dockerfile x2
- **Làm gì:** Multi-stage cho API (sdk → runtime, non-root) và web (Next.js standalone).
- **Đầu ra:** `docker build` cả hai chạy local bằng image prod.

### 9.2. Hạ tầng prod trên VPS
- **Làm gì:** Qua Dokploy: Postgres (**16.15**, khớp local) + Redis + **SMTP thật** (thay MailHog); object storage trỏ **Cloudflare R2** (không chạy MinIO ở prod); secret qua biến môi trường (kể cả tài khoản admin đầu tiên); backup Postgres hằng ngày.
- **Đầu ra:** Service chạy; secret không nằm trong repo; đã test restore 1 bản backup; upload ảnh trên prod vào đúng bucket R2 và pre-signed URL mở được.

### 9.3. Deploy API + web
- **Làm gì:** 2 app Dokploy; migration chạy trong bước deploy, KHÔNG auto-migrate lúc start; healthcheck `/api/health`; domain + HTTPS (TLS 1.2+ — SPEC mục 6); CORS đúng origin; Swagger tắt trên prod; rate limit bật.
- **Đầu ra:** Domain thật + HTTPS; `/docs` không mở được; email xác minh gửi tới hộp thư thật; user journey chạy trên prod.

### 9.4. Vòng lặp sau deploy
- **Làm gì:** Sửa code → `make check` → merge → Dokploy build → verify prod. Nợ mới ghi vào ARCHITECTURE.md mục 7.
- **Đầu ra:** Một thay đổi nhỏ đi hết vòng < 15 phút. **Sản phẩm hoàn chỉnh.**

---

## Nguyên tắc xuyên suốt

1. **Không sang bước sau khi bước trước chưa đạt đầu ra** — đầu ra trỏ thẳng mã AC/BR/TC trong SPEC.md, copy nguyên văn vào prompt.
2. **Schema + business rule lấy từ SPEC.md, cách viết từ `.claude/rules/`** — Claude Code chỉ tự quyết chi tiết hiện thực.
3. **Docs sống cùng code**: lệch là sửa cùng commit; CLAUDE.md "Trạng thái hiện tại" cập nhật cuối mỗi phase; nợ (email-qua-queue 1.2, job 90 ngày/PII…) ghi ở ARCHITECTURE.md mục 7 và phải có bước trả.
4. Phụ thuộc cứng: **Test harness (0.5b) + CI (0.7) trước mọi bước đòi test** · Storage trước Posts · Posts trước Comments/Reactions · SignalR + Queue trước Chat/Notifications · Moderation sau khi có Posts/Comments · **RBAC động (6.4) trước màn Admin/Moderator ở FE (8.7)** · FE sau khi backend chốt.
5. **Hạ tầng local chỉ đến từ `docker compose`** — máy nào cài sẵn Postgres/Redis (kể cả trong WSL) phải tắt service đó, xem 0.1.
