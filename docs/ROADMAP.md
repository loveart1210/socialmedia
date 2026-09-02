# ROADMAP — Lộ trình xây dựng SocialMedia

> Mỗi bước = một task giao cho Claude Code (hoặc tự làm). Nguồn nghiệp vụ của MỌI bước:
> `docs/SPEC.md` (schema mục 3, BR-* mục 2, ma trận quyền mục 4, acceptance mục 5) — đưa
> nguyên văn AC/BR liên quan vào prompt làm tiêu chí nghiệm thu. Nghi thức chung: đọc
> CLAUDE.md + `.claude/rules/` → làm → chạy tiêu chí nghiệm thu → `make check` (phần đã có)
> → re-index GitNexus (`node .gitnexus/run.cjs analyze --index-only`) → commit.
> Lệch docs thì sửa docs trong cùng commit.

---

## Phase 0 — Nền móng (chưa có nghiệp vụ)

### 0.1. docker-compose.yml
- **Làm gì:** `postgres:17` (volume, 5432) + `redis:7` (6379) + **MailHog** (SMTP dev, UI 8025).
- **Cách làm:** Claude Code tự quyết chi tiết (password dev, tên db). `docker compose up -d`.
- **Đầu ra:** DBeaver kết nối được Postgres; `redis-cli ping` → PONG; MailHog UI mở được.

### 0.2. Solution + skeleton
- **Làm gì:** `SocialMedia.sln`, project `socialmedia_api/src/SocialMedia.Api`, project test `SocialMedia.Api.Tests`, cây `Modules/`, `Common/`, `Infra/`, `.editorconfig`.
- **Cách làm:** Bám api.md mục 1. Package: EF Core + Npgsql, FluentValidation, Swashbuckle, JwtBearer, StackExchange.Redis, BCrypt.Net.
- **Đầu ra:** `make build-api`, `make format-api`, `make test-api` (test rỗng) pass; đường dẫn khớp Makefile.

### 0.3. Program.cs — quy ước toàn cục
- **Làm gì:** Prefix `/api` + versioning v1 · JWT (HS256, TTL 15p, claims `sub`/`role`/`jti`) + fallback policy · Swagger chỉ Development · `UnmappedMemberHandling = Disallow` + `JsonStringEnumConverter` · exception middleware → ProblemDetails · `AddValidatorsFromAssembly` · CORS `credentials: true` · `AddRateLimiter` cho nhóm auth + đăng bài/bình luận (SPEC mục 6).
- **Cách làm:** Hành vi theo ARCHITECTURE.md mục 3 + api.md mục 3/4/6; hiện thực Claude Code tự chọn. Secret dev trong `appsettings.Development.json`.
- **Đầu ra:** `make dev-api` chạy; route bất kỳ chưa đăng nhập → 401 ProblemDetails; `/docs` mở được ở dev.

### 0.4. Infra/Database — AppDbContext
- **Làm gì:** `AppDbContext` + `ApplyConfigurationsFromAssembly`; override `SaveChangesAsync` gán `CreatedAt/UpdatedAt`; convention global query filter `DeletedAt == null` cho entity có field này (posts/users/reports dùng `status`, KHÔNG áp filter — api.md mục 2).
- **Đầu ra:** Build pass; unit test chứng minh timestamps tự gán + query filter hoạt động (entity giả trong test).

### 0.5. Modules/Health
- **Làm gì:** `GET /api/health` (version-neutral, `[AllowAnonymous]`), check Postgres + Redis.
- **Đầu ra:** `curl /api/health` → 200 khi docker up, lỗi rõ khi tắt Postgres. **Chốt Phase 0.**

### 0.6. Khớp docs
- **Làm gì:** Sửa Makefile nếu đường dẫn lệch; sửa CLAUDE.md "Trạng thái hiện tại".
- **Đầu ra:** `make check` (phần API) pass từ gốc repo; docs không nói dối.

---

## Phase 1 — Auth + Users (lát cắt chốt pattern)

> Lát quan trọng nhất: mọi module sau copy pattern từ đây — bạn review kỹ nhất ở phase này.
> Schema lấy nguyên từ SPEC.md 3.2 (`users`) + 3.4 (`profiles`, `refresh_tokens`, `roles`).

### 1.1. Entity + migration đầu tiên
- **Làm gì:** `User` (đủ cột SPEC 3.2: status 5 giá trị, `failed_login_count`, `locked_until`), `RefreshToken` (lưu **băm** token), `Role` + seed 1=User/2=Moderator/3=Admin (idempotent), `Profile` (1-1). Migration `Init`.
- **Cách làm:** Tên bảng không prefix; citext cho email; admin đầu tiên tạo từ biến môi trường (SPEC 3.7). `make migrate-api` → review SQL.
- **Đầu ra:** Bảng đúng tên/kiểu/constraint trong DBeaver; bảng `roles` có sẵn 3 dòng sau migrate.

### 1.2. Infra/Mail + đăng ký & xác minh email
- **Làm gì:** `MailService` (`Infra/Mail`, SMTP → MailHog ở dev); `POST /auth/register` tạo User (status `pending`) + Profile **cùng transaction** (SPEC 3.5), gửi email xác minh; `POST /auth/verify-email` → status `active`; gửi lại email xác minh.
- **Cách làm:** Tạm gửi mail **đồng bộ** ở phase này; ghi nợ "chuyển qua queue" vào ARCHITECTURE.md mục 7 — trả nợ ở bước 5.3 khi Infra/Queue đã có (api.md mục 7 là trạng thái đích).
- **Đầu ra:** Đăng ký trên Swagger → mail hiện trong MailHog → bấm verify → status `active`.

### 1.3. Login / Refresh / Logout + lockout
- **Làm gì:** `POST /auth/login` (BCrypt cost 12; chưa xác minh → **403**; sai mật khẩu → 401 không lộ email tồn tại + `failed_login_count++`; sai 5 lần/15p → **423** khóa 15p), `POST /auth/refresh` (rotation, reuse detection → thu hồi cả chuỗi), `POST /auth/logout` (thu hồi phiên), `GET /auth/me`. Refresh token = cookie httpOnly.
- **Đầu ra:** Toàn bộ **US-002/AC-01→04** (SPEC mục 5) pass trên Swagger; TC-A01, TC-A02 (SPEC mục 4) có test tự động.

### 1.4. Users — profile
- **Làm gì:** `GET /users/{id}`, `PATCH /users/me` (`Optional<T>`), `PUT /users/me/avatar` (magic bytes, ≤10MB; tạm lưu local — Phase 3 nối storage thật).
- **Đầu ra:** Sửa tên/bio/avatar qua Swagger; field lạ → 400. Cập nhật CLAUDE.md trạng thái. **Chốt Phase 1.**

---

## Phase 2 — Quan hệ: Friendships + Follows + tìm kiếm

### 2.1. Friendships
- **Làm gì:** Entity `Friendship` — **composite PK `(user_min, user_max)` + CK `user_min < user_max`** (BR-03), trạng thái Pending/Accepted + migration. Endpoint: gửi/thu hồi lời mời, accept/decline (Pending→Accepted là **1 UPDATE** — SPEC 3.5), unfriend, danh sách bạn/lời mời. Export `AreFriendsAsync(a, b)` + **cache Redis TTL 60s** (SPEC 3.6).
- **Cách làm:** Chặn: tự kết bạn (400), gửi trùng (409), C chấp nhận lời mời của B (403).
- **Đầu ra:** **US-010/AC-01→04** pass trên Swagger với 2 tài khoản.

### 2.2. Follows
- **Làm gì:** Entity `Follow` — PK `(follower, followee)` + CK `follower <> followee` + migration. Follow/unfollow, danh sách, đếm.
- **Đầu ra:** Follow/unfollow chạy; follow trùng → 409; tự follow → 400.

### 2.3. Tìm kiếm người dùng (UC-16)
- **Làm gì:** `GET /users/search?q=` — khớp tiền tố không dấu trên `display_name`.
- **Cách làm:** **GIN pg_trgm + unaccent** (SPEC 3.6) — index viết SQL thô trong migration (EF không tả được).
- **Đầu ra:** Tìm "nguyen" ra "Nguyễn Văn A"; explain plan dùng index (dán vào PR).

---

## Phase 3 — Posts + storage + feed

### 3.1. Infra/Storage
- **Làm gì:** `StorageService` — MinIO trong docker-compose (local), S3-compatible ở prod; phục vụ ảnh qua **pre-signed URL** (SPEC mục 1 — Object Storage).
- **Đầu ra:** Test tích hợp: upload → pre-signed URL mở được. Chuyển avatar 1.4 sang storage.

### 3.2. Posts + MediaAttachment
- **Làm gì:** Entity `Post` **đúng SPEC 3.3** (content ≤5000 CK, `privacy` public/friends/private, `status` published/hidden/deleted, `comment_count`, `reaction_counts` jsonb default `{}`), `MediaAttachment` (CK size ≤ 10485760) + migration. Tạo bài (BR-01: có chữ HOẶC ≥1 ảnh; ≤10 ảnh; tạo bài + media **atomically**), sửa, xóa (status→deleted), xem bài/danh sách theo user (cursor).
- **Cách làm:** Ảnh: magic bytes + **re-encode server** trước khi đẩy storage. Quyền xem đánh giá **tại thời điểm đọc** theo BR-02 qua `AreFriendsAsync`; bài `hidden` chỉ tác giả thấy kèm lý do (BR-07). Không đặt global filter — lọc status/privacy trong service.
- **Đầu ra:** **US-004/AC-01→04** pass; user lạ xem bài `friends` → 403; TC-A03 (PATCH bài người khác → 403) có test.

### 3.3. Newsfeed
- **Làm gì:** `GET /feed` — bài `published` của bạn bè + người follow, cursor `(created_at, id)`, index partial `WHERE status='published'` (SPEC 3.6), cache Redis 30s trang đầu.
- **Cách làm:** Fan-out-on-read. Chấp nhận trễ ≤ 5s (SPEC 3.5).
- **Đầu ra:** **US-008/AC-01→03** pass (bài friends của người lạ và bài hidden KHÔNG xuất hiện); log cho thấy cache hit.

---

## Phase 4 — Comments + Reactions

### 4.1. Comments
- **Làm gì:** Entity `Comment` (`DeletedAt`, `parent_id` nullable) + migration. Tạo/sửa/xóa comment, reply, danh sách theo bài + reply theo comment (cursor).
- **Cách làm:** `AssertDepth` server-side — cấp 4 → 400 (BR-08). Xóa giữ nhánh: node trả về "Bình luận đã bị xóa" (query cây dùng `IgnoreQueryFilters()` có chủ đích — api.md mục 2). `comment_count` trên `posts` cập nhật **cùng transaction** (SPEC 3.5). Comment vào bài phải qua kiểm quyền xem bài (BR-02).
- **Đầu ra:** Reply 3 cấp OK, cấp 4 → 400; xóa comment cha vẫn thấy reply con; `comment_count` khớp thực tế.

### 4.2. Reactions
- **Làm gì:** Entity `Reaction` — **PK `(user_id, target_type, target_id)`** + migration. React/un-react; thả loại khác = **thay thế** (BR-05). `reaction_counts` jsonb cập nhật cùng transaction.
- **Cách làm:** `TargetType` ở `Common/Enums`; service kiểm target tồn tại (không FK đa hình). Response feed/comment đọc số đếm từ counter, không GroupBy mỗi request (api.md mục 5).
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
- **Làm gì:** Entity `AuditLog` (**append-only** — revoke UPDATE/DELETE ở role DB, SQL trong migration). `GET /reports` (Moderator), `PATCH /reports/{id}`: "ẩn nội dung" → post/comment `hidden` + Resolved; "bỏ qua" → Dismissed — **kết luận + audit_log cùng transaction** (SPEC 3.5); xử lý lại → 409.
- **Đầu ra:** **US-019/AC-01→04** pass; TC-A06 có test; bảng audit_logs không UPDATE được.

### 6.3. Admin (UC-20)
- **Làm gì:** `PATCH /admin/users/{id}/lock|unlock` (status suspended), gán vai trò, xem audit log — tất cả `[Authorize(Roles="Admin")]`, mọi thao tác ghi audit_log.
- **Đầu ra:** TC-A05 có test; user bị lock không đăng nhập được; audit ghi đủ ai/hành động/đối tượng.

### 6.4. Job định kỳ (trả nợ ARCHITECTURE.md mục 7 — SPEC 3.7)
- **Làm gì:** `BackgroundService` định kỳ: đối soát `comment_count`/`reaction_counts` với bản ghi thật; dọn `media_attachments` mồ côi. (Xóa cứng 90 ngày + ẩn danh PII 30 ngày: làm nếu còn thời gian, không thì giữ trong nợ.)
- **Đầu ra:** Làm lệch counter bằng tay trong DB → job chạy → counter đúng lại; log job đọc được.

> **Chốt backend.** Rà Swagger toàn bộ; TC-A01→A07 đều có test trong CI; cập nhật ARCHITECTURE.md + CLAUDE.md.

---

## Phase 7 — Frontend nền móng

### 7.1. Scaffold socialmedia_web
- **Làm gì:** `create-next-app` (App Router, TS, Tailwind), cây `features/`, `components/ui/`, `lib/` theo web.md mục 1.
- **Đầu ra:** `make dev-web`, `make typecheck-web`, `make lint-web` pass; `make check` chạy ĐỦ cả web.

### 7.2. lib/axios + auth
- **Làm gì:** `lib/axios.ts` (baseURL `/api/v1`, Bearer, token bridge, single-flight refresh), `AuthProvider`, slice `auth`: đăng ký (+ màn "kiểm tra email"), đăng nhập (xử lý **401 / 403 chưa xác minh / 423 bị khóa** — US-002), guard route.
- **Đầu ra:** Đăng ký → verify qua MailHog → đăng nhập trên UI; F5 giữ phiên; hết hạn tự refresh.

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
### 8.7. `moderation` — màn Moderator: hàng đợi report, xử lý ẩn/bỏ qua; màn Admin: khóa user, gán vai trò (route guard theo role trong JWT).

> **Chốt frontend.** Đi lại toàn bộ user journey: đăng ký → verify → kết bạn → đăng bài → tương tác → chat → thông báo → báo cáo → kiểm duyệt.

---

## Phase 9 — Deploy production (Dokploy)

### 9.1. Dockerfile x2
- **Làm gì:** Multi-stage cho API (sdk → runtime, non-root) và web (Next.js standalone).
- **Đầu ra:** `docker build` cả hai chạy local bằng image prod.

### 9.2. Hạ tầng prod trên VPS
- **Làm gì:** Qua Dokploy: Postgres + Redis + MinIO/S3 + **SMTP thật** (thay MailHog); secret qua biến môi trường (kể cả tài khoản admin đầu tiên); backup Postgres hằng ngày.
- **Đầu ra:** Service chạy; secret không nằm trong repo; đã test restore 1 bản backup.

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
4. Phụ thuộc cứng: Storage trước Posts · Posts trước Comments/Reactions · SignalR + Queue trước Chat/Notifications · Moderation sau khi có Posts/Comments · FE sau khi backend chốt.
