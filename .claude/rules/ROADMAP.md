# ROADMAP — Lộ trình xây dựng SocialMedia

> Mỗi bước = một task giao cho Claude Code (hoặc tự làm). Nghi thức chung cho MỌI bước:
> đọc CLAUDE.md + `.claude/rules/` liên quan → làm → chạy tiêu chí nghiệm thu →
> `make check` (phần đã có) → re-index GitNexus (`node .gitnexus/run.cjs analyze --index-only`)
> → commit. Bước nào lệch docs thì sửa docs trong cùng commit.

---

## Phase 0 — Nền móng (chưa có nghiệp vụ)

### 0.1. docker-compose.yml
- **Làm gì:** Tạo `docker-compose.yml` ở gốc: service `postgres:17` (volume, port 5432) + `redis:7` (port 6379).
- **Cách làm:** Claude Code tự quyết chi tiết (password dev, tên db). Chạy `docker compose up -d`.
- **Đầu ra:** DBeaver kết nối được Postgres; `redis-cli ping` trả PONG.

### 0.2. Solution + skeleton
- **Làm gì:** `SocialMedia.sln`, project `socialmedia_api/src/SocialMedia.Api`, project test `SocialMedia.Api.Tests`, cây `Modules/`, `Common/`, `Infra/`, `.editorconfig`.
- **Cách làm:** Bám đúng api.md mục 1. Cài package: EF Core + Npgsql, FluentValidation, Swashbuckle, JwtBearer, StackExchange.Redis.
- **Đầu ra:** `make build-api`, `make format-api`, `make test-api` (test rỗng) pass; đường dẫn khớp Makefile.

### 0.3. Program.cs — quy ước toàn cục
- **Làm gì:** Route prefix `/api` + versioning v1 · JWT + fallback policy `RequireAuthenticatedUser` · Swagger chỉ bật Development · `UnmappedMemberHandling = Disallow` + `JsonStringEnumConverter` · exception middleware → ProblemDetails · `AddValidatorsFromAssembly` · CORS `credentials: true` cho origin web.
- **Cách làm:** Hành vi theo ARCHITECTURE.md mục 3 + api.md mục 3/4/6; cách hiện thực Claude Code tự chọn. JWT secret dev trong `appsettings.Development.json`, không commit secret thật.
- **Đầu ra:** App chạy `make dev-api`; gọi route bất kỳ chưa đăng nhập → 401 ProblemDetails; `/docs` mở được ở dev.

### 0.4. Infra/Database — AppDbContext
- **Làm gì:** `AppDbContext` + `ApplyConfigurationsFromAssembly`; override `SaveChangesAsync` gán `CreatedAt/UpdatedAt`; convention global query filter `DeletedAt == null` áp tự động.
- **Cách làm:** Theo api.md mục 2. Chưa có entity nghiệp vụ, chưa migration.
- **Đầu ra:** Build pass; unit test chứng minh timestamps tự gán + query filter hoạt động (dùng entity giả trong test).

### 0.5. Modules/Health
- **Làm gì:** `GET /api/health` (version-neutral, `[AllowAnonymous]`), check kết nối Postgres + Redis.
- **Cách làm:** Module mẫu đầu tiên theo pattern `AddXModule()`.
- **Đầu ra:** `curl /api/health` → 200 khi docker up, lỗi rõ ràng khi tắt Postgres. **Chốt Phase 0.**

### 0.6. Khớp docs
- **Làm gì:** Sửa Makefile nếu đường dẫn lệch; sửa CLAUDE.md mục "Trạng thái hiện tại" + đường dẫn `.claude/rules/`.
- **Đầu ra:** `make check` (phần API) pass từ gốc repo; docs không nói dối.

---

## Phase 1 — Auth + Users (lát cắt chốt pattern)

> Đây là lát quan trọng nhất: mọi module sau copy pattern từ đây. Bạn review kỹ nhất ở phase này.
> **Điều kiện trước khi giao:** ERD đã xuất ra `docs/erd.md` (mermaid) từ tài liệu PTTK — không để Claude Code tự bịa schema.

### 1.1. Entity User + RefreshToken + migration đầu tiên
- **Làm gì:** `Modules/Auth/Entities`: `User` (bảng `au_users`), `RefreshToken` (`au_refresh_tokens`). Migration `Init`.
- **Cách làm:** Field theo `docs/erd.md`; quy ước theo api.md mục 2 (Guid v7, timestamptz, DeletedAt, index). `make migrate-api` rồi review SQL.
- **Đầu ra:** Bảng hiện trong DBeaver đúng tên/kiểu; SQL migration đã được đọc và duyệt.

### 1.2. Register / Login / Refresh / Logout / Me
- **Làm gì:** `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`. Access token trả body, refresh token là cookie httpOnly.
- **Cách làm:** Hash mật khẩu (Claude Code chọn thuật toán hiện hành, không tự chế); refresh rotation: dùng 1 lần, cấp mới, thu hồi cũ. DTO + validator theo api.md mục 3.
- **Đầu ra:** Trên Swagger: register → login → gọi `/auth/me` bằng token → refresh → logout xong refresh cũ bị từ chối. Test service cho các nhánh chính.

### 1.3. Modules/Users — profile
- **Làm gì:** Entity `Profile` (`usr_profiles`) tách khỏi `User`; `GET /users/{id}`, `PATCH /users/me`, `PUT /users/me/avatar` (tạm lưu local, Phase 3 mới nối storage thật).
- **Cách làm:** PATCH dùng `Optional<T>` phân biệt "không gửi"/"gửi null"; avatar kiểm magic bytes + ≤10MB ngay từ giờ.
- **Đầu ra:** Sửa được tên/bio/avatar qua Swagger; gửi field lạ → 400.

### 1.4. Cập nhật trạng thái
- **Làm gì:** CLAUDE.md "Trạng thái hiện tại" → "đã có Auth + Users, đang dựng dần các module còn lại".
- **Đầu ra:** Docs đúng thực tế. **Chốt Phase 1.**

---

## Phase 2 — Quan hệ: Friendships + Follows

### 2.1. Friendships
- **Làm gì:** Entity `FriendRequest`, `Friendship` (`frd_`) + migration. Endpoint: gửi/thu hồi lời mời, accept/decline, unfriend, danh sách bạn, danh sách lời mời.
- **Cách làm:** Accept = transaction (đóng request + tạo friendship). Chặn: tự kết bạn với mình, gửi trùng, gửi khi đã là bạn. Export `AreFriendsAsync(a, b)` cho module khác inject.
- **Đầu ra:** Kịch bản Swagger 2 tài khoản: A mời → B accept → cả hai thấy nhau trong danh sách bạn → unfriend → hết. Các nhánh chặn trả 400/409 đúng.

### 2.2. Follows
- **Làm gì:** Entity `Follow` (`flw_`) + migration. Follow/unfollow, danh sách following/followers, đếm.
- **Cách làm:** Unique index `(FollowerId, FolloweeId)`; đếm bằng query `GroupBy`, không lưu counter.
- **Đầu ra:** Follow/unfollow chạy trên Swagger; follow trùng → 409.

---

## Phase 3 — Posts + upload ảnh

### 3.1. Infra/Storage
- **Làm gì:** `StorageService` — adapter object storage (MinIO chạy thêm trong docker-compose cho local; prod trỏ S3-compatible qua config).
- **Cách làm:** Interface `IStorageService` (upload, delete, build URL); module nghiệp vụ không cầm SDK trực tiếp (api.md mục 7).
- **Đầu ra:** Test tích hợp: upload file lên MinIO local, lấy URL mở được. Chuyển avatar 1.3 sang storage thật.

### 3.2. Posts
- **Làm gì:** Entity `Post`, `PostImage` (`pst_`) + migration. CRUD bài viết (tạo kèm ≤10 ảnh ≤10MB, sửa nội dung, xoá mềm), quyền riêng tư (public / friends-only), xem bài theo user.
- **Cách làm:** Kiểm ảnh bằng magic bytes ở service; friends-only kiểm qua `AreFriendsAsync` — không query bảng `frd_`. Danh sách bài của một user dùng cursor paging.
- **Đầu ra:** Swagger: đăng bài kèm ảnh → URL ảnh mở được; tài khoản lạ xem bài friends-only → 403; ảnh thứ 11 hoặc 11MB → 400.

### 3.3. Newsfeed
- **Làm gì:** `GET /feed` — bài của bạn bè + người mình follow, cursor `(CreatedAt, Id)`, cache trang đầu bằng Redis.
- **Cách làm:** Fan-out-on-read (query lúc đọc) — KHÔNG fan-out-on-write ở quy mô đồ án. Invalidate cache khi có bài mới của người liên quan (đơn giản: TTL ngắn 30–60s là đủ).
- **Đầu ra:** Feed trả đúng bài theo quan hệ, phân trang cursor chạy; đo được cache hit qua log.

---

## Phase 4 — Comments + Reactions

### 4.1. Comments
- **Làm gì:** Entity `Comment` (`cmt_`, `ParentId` tự tham chiếu) + migration. Tạo/sửa/xoá mềm comment, reply, danh sách theo bài (cursor), danh sách reply theo comment.
- **Cách làm:** `AssertDepth`: đọc độ sâu parent phía server, reply cấp 4 → 400. Comment vào bài friends-only phải qua kiểm quan hệ.
- **Đầu ra:** Swagger: comment → reply → reply của reply → reply cấp 4 bị 400. Xoá mềm comment cha vẫn giữ cây con hiển thị "[đã xoá]" (quyết định nghiệp vụ — xác nhận với ERD/PTTK trước khi code).

### 4.2. Reactions
- **Làm gì:** Entity `Reaction` (`rct_`) + migration: `TargetType` (POST/COMMENT) + `TargetId`, unique `(UserId, TargetType, TargetId)`. React/un-react/đổi loại, đếm theo loại.
- **Cách làm:** `TargetType` enum ở `Common/Enums` (api.md mục 2); service kiểm target tồn tại (không có FK đa hình); đếm reaction cho 20 bài feed = 1 query GroupBy.
- **Đầu ra:** React bài + comment chạy; react trùng → thay loại chứ không nhân đôi; response feed/comment đã nhúng số reaction.

---

## Phase 5 — Realtime: Conversations + Notifications

### 5.1. Hạ tầng SignalR
- **Làm gì:** `AddSignalR().AddStackExchangeRedis(...)` (backplane), auth qua JWT `access_token` query khi handshake, map hub `/hubs/chat`, `/hubs/notifications`.
- **Đầu ra:** Client test (script nhỏ hoặc Postman WebSocket) kết nối hub bằng token hợp lệ; token sai → từ chối.

### 5.2. Conversations
- **Làm gì:** Entity `Conversation`, `ConversationMember`, `Message` (`cnv_`) + migration. REST: tạo/lấy hội thoại 1-1, lịch sử tin nhắn (cursor). Hub: gửi tin, nhận realtime, join group theo hội thoại.
- **Cách làm:** Gửi tin = ghi DB trước rồi broadcast; hub method kiểm participant y như service. Chỉ làm 1-1, group chat để ngoài phạm vi (ghi rõ vào ARCHITECTURE.md nợ kỹ thuật nếu ERD có vẽ).
- **Đầu ra:** 2 client nối hub, A gửi B nhận realtime; refresh trang đọc lại đủ lịch sử qua REST; người ngoài hội thoại join group → bị chặn.

### 5.3. Notifications
- **Làm gì:** Entity `Notification` (`ntf_`, `TargetType/TargetId`) + migration. Sinh notification khi: được mời kết bạn, được accept, có reaction/comment vào bài mình. Danh sách + đánh dấu đã đọc. Push realtime qua hub.
- **Cách làm:** Fan-out qua Redis Stream + `NotificationFanoutWorker` (BackgroundService trong module — api.md mục 8): job chỉ mang id, consumer đọc lại DB; bản ghi mất → log warning + ack.
- **Đầu ra:** A react bài của B → B nhận notification realtime + thấy trong danh sách; tắt worker thì job nằm chờ, bật lại xử lý nốt (chứng minh durable).

> **Chốt backend.** Rà Swagger toàn bộ; cập nhật ARCHITECTURE.md mục nợ kỹ thuật; CLAUDE.md → "backend xong, bắt đầu frontend".

---

## Phase 6 — Frontend nền móng

### 6.1. Scaffold socialmedia_web
- **Làm gì:** `create-next-app` (App Router, TS, Tailwind), cây `features/`, `components/ui/`, `lib/` theo web.md mục 1.
- **Đầu ra:** `make dev-web`, `make typecheck-web`, `make lint-web` pass; `make check` giờ chạy ĐỦ cả web.

### 6.2. lib/axios + auth
- **Làm gì:** `lib/axios.ts` (baseURL `/api/v1`, Bearer, token bridge, single-flight refresh 401), `AuthProvider`, slice `features/auth` (login/register screen, guard route).
- **Cách làm:** Theo web.md mục 2; refresh dựa cookie httpOnly + `credentials: true`.
- **Đầu ra:** Đăng nhập trên UI, F5 vẫn giữ phiên (refresh tự chạy), token hết hạn tự gia hạn không văng.

### 6.3. UI kit tối thiểu
- **Làm gì:** Design token trong `tailwind.config` + primitive: `Button`, `Input`, `Modal`, `Toast`, `Avatar`, `Skeleton`, `Spinner`, `ConfirmDialog`, `DropdownMenu`, `ErrorPanel`.
- **Cách làm:** web.md mục 4; chốt xong thì điền tên token thật vào web.md (docs mọc theo code).
- **Đầu ra:** Trang demo (route dev) hiển thị đủ primitive; không hex thô trong className.

---

## Phase 7 — Frontend theo lát tính năng

> Mỗi bước một slice, thứ tự khớp backend. Mỗi slice: `api/ + queryKeys + hooks + components + errors + index` (web.md mục 1), danh sách đủ 4 trạng thái (mục 5).

### 7.1. `profile` — xem/sửa trang cá nhân, đổi avatar (preview + chặn sớm >10MB).
- **Đầu ra:** Sửa profile trên UI khớp DB; ảnh sai giới hạn bị chặn trước khi gửi.

### 7.2. `friends` — tìm user, gửi/nhận lời mời, danh sách bạn. Optimistic cho accept/decline.
- **Đầu ra:** Kịch bản 2 trình duyệt: mời → accept → thấy nhau; rollback đúng khi server từ chối.

### 7.3. `post` + `feed` — composer đăng bài kèm `ImageGrid`/`Lightbox`, newsfeed `useInfiniteQuery` + sentinel IntersectionObserver.
- **Đầu ra:** Đăng bài có ảnh hiện ngay trên feed; cuộn vô hạn mượt; skeleton/empty/error đủ.

### 7.4. `comments` + `reactions` — cây comment 3 cấp, react optimistic (snapshot + rollback).
- **Đầu ra:** Like đổi icon tức thì, mạng lỗi thì hoàn tác; reply đúng cấp.

### 7.5. `chat` — `SignalRProvider` + `useChatHub` (một connection cả app), tin nhắn ghi vào cache react-query, reconnect xong invalidate hội thoại đang mở.
- **Đầu ra:** 2 trình duyệt chat realtime; tắt mạng 10s bật lại không mất tin (đã bù qua invalidate).

### 7.6. `notifications` — chuông + badge số chưa đọc, dropdown danh sách, realtime qua `useNotificationsHub`.
- **Đầu ra:** A tương tác → chuông B nhảy số ngay; đánh dấu đã đọc đồng bộ.

> **Chốt frontend.** Đi lại toàn bộ user journey: đăng ký → kết bạn → đăng bài → tương tác → chat → nhận thông báo.

---

## Phase 8 — Deploy production (Dokploy)

### 8.1. Dockerfile x2
- **Làm gì:** Multi-stage cho API (sdk → runtime, chạy non-root) và web (Next.js standalone output).
- **Đầu ra:** `docker build` cả hai chạy local được bằng image prod.

### 8.2. Hạ tầng prod trên VPS
- **Làm gì:** Qua Dokploy: Postgres + Redis + MinIO (hoặc S3 ngoài), secret qua biến môi trường, backup tự động cho Postgres.
- **Đầu ra:** Các service chạy; connection string prod không nằm trong repo; đã test restore 1 bản backup.

### 8.3. Deploy API + web
- **Làm gì:** 2 app Dokploy trỏ repo; migration chạy trong bước deploy (`dotnet ef database update`), KHÔNG auto-migrate lúc app start; healthcheck trỏ `/api/health`; domain + HTTPS; CORS chỉ mở đúng origin web; Swagger tắt (đã theo môi trường).
- **Đầu ra:** Truy cập bằng domain thật, HTTPS; `/docs` không mở được trên prod; đi lại user journey trên prod thành công.

### 8.4. Vòng lặp sau deploy
- **Làm gì:** Quy trình lặp: sửa code → `make check` → merge → Dokploy build → verify prod. Ghi nợ kỹ thuật phát sinh vào ARCHITECTURE.md mục 7.
- **Đầu ra:** Một chu trình thay đổi nhỏ (sửa 1 chuỗi UI) đi hết vòng < 15 phút. **Sản phẩm hoàn chỉnh.**

---

## Nguyên tắc xuyên suốt

1. **Không sang bước sau khi bước trước chưa đạt đầu ra** — đầu ra là tiêu chí nghiệm thu, đưa nguyên văn vào prompt cho Claude Code.
2. **Schema lấy từ `docs/erd.md`**, quy ước từ `.claude/rules/` — Claude Code chỉ tự quyết chi tiết hiện thực, không tự quyết kiến trúc/schema.
3. **Docs sống cùng code**: lệch là sửa trong cùng commit; CLAUDE.md "Trạng thái hiện tại" cập nhật cuối mỗi phase.
4. Phase 2–5 có thể đảo chút thứ tự nội bộ, nhưng **Storage trước Posts**, **Posts trước Comments/Reactions**, **SignalR trước Chat/Notifications** là phụ thuộc cứng.
