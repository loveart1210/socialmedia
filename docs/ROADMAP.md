# ROADMAP — Lộ trình xây dựng SocialMedia

> Mỗi bước = một task giao cho Claude Code (hoặc tự làm).
>
> - **Nghiệp vụ** lấy từ `docs/SPEC.md`: schema mục 3, BR-* mục 2, phân quyền + TC-A* mục 4,
>   acceptance AC mục 5, phi chức năng mục 6, enum mục 7. Đưa **nguyên văn** AC/BR/TC liên quan
>   vào prompt làm tiêu chí nghiệm thu.
> - **Khuôn truyền nhận dữ liệu** lấy từ `docs/API-FLOW.md`: mọi endpoint đi đúng 7 bước
>   Request DTO → Middleware → Controller → Service → Entity → Response DTO → UI.
> - **Luật code** lấy từ `CLAUDE.md` và skill `socialmedia_convention`.
>
> Nghi thức chung mỗi task: đọc SPEC phần liên quan → làm theo khuôn 7 lớp dưới đây → chạy tiêu chí
> nghiệm thu → `make check` → re-index GitNexus (`node .gitnexus/run.cjs analyze --index-only`) →
> commit. Lệch docs thì sửa docs **trong cùng commit**.

---

## Khuôn API-FLOW — áp cho mọi task có endpoint

`docs/API-FLOW.md` mô tả 7 bước tổng quát; bảng dưới là **cách 7 bước đó hiện ra trong repo này**.
Task backend dừng ở bước 6 (verify trên Swagger `/docs`), bước 1 và 7 do Phase 7–8 hoàn tất.

| Bước API-FLOW | Chỗ ở trong code | Luật bất biến của dự án |
|---|---|---|
| 1. Khởi tạo yêu cầu (FE) | `socialmedia_web/features/<slice>/api` | Bearer token ở header, body là JSON của Request DTO. Phase 7–8 mới làm |
| 2. Xác thực cổng vào | `Program.cs` + `Common` | JWT HS256, fallback policy **default deny**; quyền kiểm bằng `[HasPermission("...")]` đọc cache Redis, **không** `[Authorize(Roles=…)]`, **không** nhét permission vào JWT; lỗi ra ProblemDetails qua handler chung; `X-Request-Id`; rate limit nhóm `auth` / `content` |
| 3. Tiếp nhận & kiểm cấu trúc | `Modules/<X>/<X>Controller` | Route `/api/v{version}/<resource>`; controller chỉ binding + validate + chọn status code, **không chứa business rule** |
| 4. Xử lý nghiệp vụ | `Modules/<X>/<X>Service` | BR-* kiểm ở đây; `userId` lấy từ claim `sub`, không tin client; không query chéo bảng module khác — inject service của module sở hữu; transaction theo SPEC 3.5 |
| 5. Ánh xạ dữ liệu | Service → record response | **Không bao giờ trả entity ra ngoài**; lọc `password_hash`, `token_hash`, PII; `avatar_key`/`storage_key` dựng thành pre-signed URL rồi mới trả |
| 6. Đóng gói & phản hồi | Controller | 200 / 201 + Location / 202 / 204 đúng ngữ nghĩa; lỗi 400/401/403/409/423 đều là ProblemDetails |
| 7. Cập nhật giao diện | `socialmedia_web/features/<slice>` | 4 trạng thái loading / empty / error / success; map mã lỗi HTTP sang thông báo. Phase 7–8 |

**Request DTO** khai đúng những gì client *được phép gửi* (`UnmappedMemberHandling = Disallow` →
field lạ trả 400, không bỏ qua). **Response DTO** khai đúng những gì client *được phép nhìn thấy*.
**Entity** ánh xạ 1-1 bảng trong `docs/SPEC.md` mục 3 — snake_case, không prefix domain.

### Định nghĩa hoàn thành (DoD) — mỗi task có endpoint phải tick đủ

1. **Endpoint** đúng `/api/v1/<resource>` + verb đúng ngữ nghĩa; mặc định cần Bearer, public thì
   `[AllowAnonymous]` **cố ý** và ghi rõ vì sao.
2. **Middleware**: gọi không token → 401 ProblemDetails; thiếu permission → 403; endpoint nhạy cảm
   gắn đúng rate limit policy.
3. **Controller**: không có nhánh nghiệp vụ nào; không tự dựng response lỗi.
4. **Request DTO**: field lạ → 400; ràng buộc định dạng/độ dài nằm ở FluentValidation, ràng buộc
   nghiệp vụ (BR-*) **không** nằm ở đây.
5. **Service**: BR-* liên quan có test; ownership/membership kiểm ở đây (chống IDOR — SPEC mục 6);
   transaction đúng SPEC 3.5.
6. **Entity + migration**: tên bảng/cột/constraint khớp SPEC mục 3; `make migrate-api` chạy thử
   trên DB local + đọc SQL sinh ra trước khi commit.
7. **Response DTO**: là record, không lộ dữ liệu nhạy cảm, status code đúng.
8. **Nghiệm thu**: AC/TC nêu ở mục "Đầu ra" pass (test tự động từ 1.3 trở đi), `make check` xanh,
   re-index GitNexus, commit `<type>(<scope>): <summary>`.

---

## Phase 0 — Nền móng ✅ (đã xong, giữ để tham chiếu)

Phase này dựng bước 2 và 6 của API-FLOW cho toàn hệ thống, chưa có nghiệp vụ nào.

### 0.1. docker-compose.yml ✅
- **Làm gì:** `postgres:16.15-alpine` (volume, 5432) + `redis:7` (6379) + **MailHog** (SMTP dev, UI 8025) + **MinIO** (9000 + console 9001).
- **Bẫy đã gặp:** máy nào đã cài sẵn PostgreSQL — **kể cả trong WSL** — phải tắt hẳn (`sudo systemctl disable --now postgresql`). Windows không thấy socket của WSL trong netstat nên container vẫn bind 5432 thành công, sau đó `localhost:5432` từ Windows và từ trong WSL trỏ về **hai DB khác nhau** mà không báo lỗi gì.
- **Đầu ra:** DBeaver kết nối được Postgres; `redis-cli ping` → PONG; MailHog UI và MinIO console mở được.

### 0.2. Solution + skeleton ✅
- **Làm gì:** `socialmedia_api/SocialMedia.sln` (trong `socialmedia_api/`, không ở gốc), `src/SocialMedia.Api`, `tests/SocialMedia.Api.Tests`, cây `Modules/` · `Common/` · `Infra/`, `.editorconfig`. Web không có `.sln` — `package.json` đóng vai trò đó, dựng ở Phase 7.
- **Đầu ra:** `make build-api`, `make format-api`, `make test-api` pass.

### 0.3. Program.cs — bước 2 và 6 của API-FLOW ✅
- **Làm gì:** prefix `/api` + URI versioning (`Asp.Versioning.Mvc`, route `api/v{version:apiVersion}/[controller]`, `/api/health` `[ApiVersionNeutral]`), Swashbuckle sinh document theo từng version · JWT HS256 TTL 15p (claims `sub`/`role`/`jti`) + fallback policy default deny · Swagger UI `/docs` chỉ Development · `UnmappedMemberHandling = Disallow` + `JsonStringEnumConverter` · exception middleware → ProblemDetails · `AddValidatorsFromAssembly` · CORS `credentials: true` · `AddRateLimiter` policy `auth` + `content` (SPEC mục 6).
- **Đầu ra:** route bất kỳ chưa đăng nhập → 401 ProblemDetails; `/docs` mở được ở dev.

### 0.4. Infra/Database — AppDbContext ✅
- **Làm gì:** `AppDbContext` + `ApplyConfigurationsFromAssembly`; `SaveChangesAsync` tự gán `CreatedAt/UpdatedAt`; global query filter `DeletedAt == null` cho entity `ISoftDeletable`.
- **Nhắc lại luật:** entity có cột `status` (`posts`/`users`/`reports`) **không** áp query filter — lọc trong service, vì bài `hidden` tác giả vẫn phải thấy (BR-07).
- **Đầu ra:** unit test chứng minh timestamps tự gán + query filter hoạt động.

### 0.5. Modules/Health ✅
- **Làm gì:** `GET /api/health` (version-neutral, `[AllowAnonymous]`), check Postgres + Redis, 503 khi phụ thuộc hỏng.

### 0.5b. Test harness tích hợp ✅
- **Làm gì:** `WebApplicationFactory<Program>` + Postgres/Redis thật qua **Testcontainers** (DB riêng mỗi lần chạy) + helper đăng nhập/lấy token. Điều kiện tiên quyết của mọi bước sau: **từ 1.3 trở đi bước nào cũng phải có test tự động**.
- **Đầu ra:** test `/api/health` → 200 và route bảo vệ không token → 401 (**TC-A01**); `make test-api` xanh.

### 0.6. Khớp docs ✅ · 0.7. CI ✅
- GitHub Actions chạy `make check` trên mỗi push/PR — **TC-A01→A08 phải chạy trong CI**, không chạy tay.

---

## Phase 1 — Auth + Users (lát cắt chốt pattern)

> Lát quan trọng nhất: mọi module sau copy pattern 7 lớp từ đây — review kỹ nhất ở phase này.
> Schema: SPEC 3.2 (`users`) · 3.4/3.4a (`profiles`, `refresh_tokens`, `auth_tokens`, `roles`).

### 1.1. Entity + migration đầu tiên (bước 5 của API-FLOW)
- **Entity:** `User` (đủ cột SPEC 3.2: `status` 5 giá trị, `failed_login_count`, `locked_until`), `Profile` (1-1, đủ cột SPEC 3.4a gồm `avatar_key`), `RefreshToken` (lưu **băm**), `AuthToken` (SPEC 3.4a — dùng chung `email_verify`/`password_reset` qua cột `purpose`), `Role` + `Permission` + `RolePermission`.
- **Migration `Init`:** tên bảng không prefix; `CREATE EXTENSION citext` cho email; seed **idempotent** `roles` (3 dòng `is_system`), `permissions` (đủ danh sách SPEC mục 7), `role_permissions` (ma trận seed SPEC mục 4); admin đầu tiên tạo từ biến môi trường (SPEC 3.7).
- **Đầu ra:** bảng đúng tên/kiểu/constraint trong DBeaver; `roles` 3 dòng, `permissions` đủ, `role_permissions` khớp ma trận seed. Chưa có endpoint nào ở bước này.

### 1.2. Infra/Mail + đăng ký & xác minh email
- **Endpoint:** `POST /api/v1/auth/register` `[AllowAnonymous]` (rate limit `auth`) → 201 · `POST /api/v1/auth/verify-email` `[AllowAnonymous]` → 200 · `POST /api/v1/auth/resend-verification` `[AllowAnonymous]` → 202.
- **Request DTO:** `RegisterRequest(Email, Username, Password, DisplayName)` — validator: email hợp lệ, username `[a-z0-9_.]` ≤ 30, mật khẩu 8–72 ký tự có chữ và số (SPEC mục 6). `VerifyEmailRequest(Token)`.
- **Service:** `AuthService` tạo `User` (status `pending`) + `Profile` **cùng transaction** (SPEC 3.5), sinh `AuthToken` purpose `email_verify` TTL 24h lưu **băm**, gọi `MailService` (`Infra/Mail`, MailKit → MailHog ở dev). Verify: token đúng + chưa dùng + chưa hết hạn → `status = active`, đặt `used_at`.
- **Response DTO:** `RegisterResponse(UserId, Email, Status)` — **không** trả token xác minh, không trả `password_hash`.
- **Nợ kỹ thuật cố ý:** gửi mail **đồng bộ** ở phase này, trả nợ ở 5.3 khi có `Infra/Queue` (xem mục "Nợ kỹ thuật").
- **Đầu ra:** đăng ký trên Swagger → mail hiện trong MailHog → verify → status `active`; đăng ký trùng email → 409; field lạ trong body → 400.

### 1.3. Login / Refresh / Logout + lockout
- **Endpoint:** `POST /auth/login` `[AllowAnonymous]` · `POST /auth/refresh` `[AllowAnonymous]` (đọc cookie) · `POST /auth/logout` · `GET /auth/me`. Cả nhóm gắn rate limit `auth`.
- **Request DTO:** `LoginRequest(Email, Password)`. Refresh token **không** nằm trong body — cookie httpOnly.
- **Service:** BCrypt cost 12; chưa xác minh → **403** (AC-04); sai mật khẩu → **401** thông báo trung tính không lộ email tồn tại + `failed_login_count++` (AC-02); sai 5 lần/15p → **423** khóa 15 phút (AC-03); refresh **xoay vòng**, phát hiện reuse → thu hồi cả chuỗi; logout thu hồi phiên hiện tại.
- **Response DTO:** `LoginResponse(AccessToken, ExpiresIn, User(...))` — refresh token đi bằng cookie, không nằm trong body JSON. `MeResponse` đọc từ claim `sub` chứ không nhận id từ client.
- **Đầu ra:** **US-002/AC-01→04** pass trên Swagger; **TC-A01, TC-A02** có test tự động trong CI.

### 1.4. Users — profile
- **Endpoint:** `GET /users/{id}` · `PATCH /users/me` · `PUT /users/me/avatar` (multipart).
- **Request DTO:** `UpdateProfileRequest` dùng `Optional<T>` để phân biệt "không gửi" với "gửi null" (các cột SPEC 3.4a). Avatar: kiểm **magic bytes** + ≤ 10MB ở service, không tin `Content-Type` client gửi.
- **Service:** `UsersService` — `userId` từ claim `sub`, không nhận từ body; ghi `profiles.avatar_key` (**key trong bucket**, không lưu URL). Phase này tạm trỏ thư mục local.
- **Response DTO:** `UserResponse` dựng **pre-signed URL từ `avatar_key`** ngay từ giờ, để 3.1 chỉ phải đổi cách dựng URL chứ không đổi contract.
- **Đầu ra:** sửa tên/bio/avatar qua Swagger; field lạ → 400; ảnh 11MB → 400.

### 1.5. Quên / đặt lại mật khẩu (UC-21)
- **Endpoint:** `POST /auth/forgot-password` `[AllowAnonymous]` → **202** · `POST /auth/reset-password` `[AllowAnonymous]` → 200. Rate limit `auth`.
- **Service:** tái dùng nguyên `MailService` + bảng `auth_tokens` của 1.2, purpose `password_reset` TTL **30 phút**. Reset = đổi `password_hash` + đặt `used_at` + **thu hồi mọi refresh token**, cùng transaction (SPEC 3.5).
- **Response DTO:** phản hồi cho email **không tồn tại** giống hệt email tồn tại (AC-01) — chỗ này là quyết định bảo mật, đừng "sửa cho thân thiện".
- **Đầu ra:** **US-021/AC-01→04** pass; token dùng lần hai → 400; refresh token cũ sau khi đổi mật khẩu → 401. Cập nhật CLAUDE.md "Trạng thái hiện tại". **Chốt Phase 1.**

---

## Phase 2 — Quan hệ: Friendships + Follows + tìm kiếm

### 2.1. Friendships
- **Endpoint:** `POST /friendships` (gửi lời mời) · `DELETE /friendships/{userId}` (thu hồi / unfriend) · `PATCH /friendships/{userId}` (accept/decline) · `GET /friendships` · `GET /friendships/requests`.
- **Entity:** `Friendship` — **composite PK `(user_min, user_max)` + CK `user_min < user_max`** (BR-03), trạng thái Pending/Accepted + migration.
- **Service:** Pending→Accepted là **1 UPDATE** (SPEC 3.5). Chặn: tự kết bạn → 400, gửi trùng → 409, C chấp nhận lời mời của B → 403. Export `AreFriendsAsync(a, b)` + **cache Redis TTL 60s** (SPEC 3.6) — đây là hàm mọi module khác gọi thay vì tự query bảng `friendships`.
- **Đầu ra:** **US-010/AC-01→04** pass trên Swagger với 2 tài khoản — **trừ vế "được thông báo" của AC-01**: Notifications tới 5.3 mới có, nghiệm thu lại ở đó.

### 2.2. Follows
- **Endpoint:** `POST /follows/{userId}` · `DELETE /follows/{userId}` · `GET /users/{id}/followers` · `GET /users/{id}/following`.
- **Entity:** `Follow` — PK `(follower, followee)` + CK `follower <> followee` + migration.
- **Đầu ra:** follow/unfollow chạy; follow trùng → 409; tự follow → 400.

### 2.3. Tìm kiếm người dùng (UC-16)
- **Endpoint:** `GET /users/search?q=&cursor=` — khớp tiền tố không dấu trên `display_name`.
- **Cách làm:** **GIN pg_trgm + unaccent** (SPEC 3.6), index viết SQL thô trong migration (EF không tả được). **Bẫy:** `unaccent()` không IMMUTABLE nên **không index trực tiếp được** — migration phải `CREATE EXTENSION pg_trgm, unaccent`, tạo wrapper `IMMUTABLE` bọc `unaccent`, rồi mới index trên biểu thức đó.
- **Đầu ra:** tìm "nguyen" ra "Nguyễn Văn A"; explain plan dùng index (dán vào PR).

---

## Phase 3 — Posts + storage + feed

### 3.1. Infra/Storage
- **Làm gì:** `StorageService` — **một adapter `AWSSDK.S3` cho cả hai môi trường**: local trỏ MinIO (0.1), prod trỏ **Cloudflare R2**. Phục vụ ảnh qua **pre-signed URL** (SPEC mục 1).
- **Cách làm:** MinIO là *server* S3-compatible, không phải SDK riêng — khác nhau đúng `ServiceUrl` + credential, cả hai `ForcePathStyle = true`, R2 `region = auto`. Bind bằng `IOptions<StorageOptions>` + `ValidateOnStart`. **Bẫy:** URL ký từ MinIO trong docker mang host nội bộ → cấu hình public endpoint riêng để link mở được từ trình duyệt.
- **Đầu ra:** test tích hợp upload → pre-signed URL mở được; chuyển `avatar_key` của 1.4 sang storage thật — **chỉ đổi bước 5 (mapping), không đổi schema và không đổi Response DTO**.

### 3.2. Posts + MediaAttachment
- **Endpoint:** `POST /posts` (multipart) → 201 + Location · `PATCH /posts/{id}` · `DELETE /posts/{id}` · `GET /posts/{id}` · `GET /users/{id}/posts?cursor=`. Nhóm ghi gắn rate limit `content`.
- **Request DTO:** `CreatePostRequest(Content?, Privacy, Files[])` — `privacy` enum 3 mức, `Content` ≤ 5000. **BR-01 (có chữ HOẶC ≥ 1 ảnh, ≤ 10 ảnh) kiểm ở service**, không chỉ ở validator.
- **Entity:** `Post` đúng SPEC 3.3 (`content` CK ≤ 5000, `privacy`, `status` published/hidden/deleted, `comment_count`, `reaction_counts` jsonb default `{}`), `MediaAttachment` (CK size ≤ 10485760, `position` 0–9) + migration.
- **Service:** tạo bài + media **atomically**; ảnh qua magic bytes → **re-encode bằng SixLabors.ImageSharp** (vứt metadata và payload nhét kèm — SPEC mục 6) → đẩy storage. Xoá = `status = deleted`. Quyền xem đánh giá **tại thời điểm đọc** theo BR-02 qua `AreFriendsAsync`; bài `hidden` chỉ tác giả thấy kèm lý do (BR-07). **Không** đặt global query filter — lọc `status`/`privacy` trong service.
- **Response DTO:** `PostResponse` trả `mediaUrls` là pre-signed URL dựng từ `storage_key`, đọc `reaction_counts` từ counter — **không** trả `storage_key` thô, không GroupBy mỗi request.
- **Đầu ra:** **US-004/AC-01→04** pass — riêng vế "xuất hiện trên **feed** bạn bè" của AC-01 nghiệm thu ở 3.3, bước này chỉ cần bài hiện trong danh sách bài của tác giả. User lạ xem bài `friends` → 403; **TC-A03** (PATCH bài người khác → 403) có test.

### 3.3. Newsfeed
- **Endpoint:** `GET /feed?cursor=` — bài `published` của bạn bè + người follow, cursor `(created_at, id)`.
- **Cách làm:** fan-out-on-read; index partial `WHERE status='published'` (SPEC 3.6); cache Redis 30s trang đầu. Chấp nhận trễ ≤ 5s (SPEC 3.5).
- **Đầu ra:** **US-008/AC-01→03** pass (bài `friends` của người lạ và bài `hidden` KHÔNG xuất hiện); log cho thấy cache hit.

### 3.3b. Đo tải feed (US-008/AC-04)
- **Làm gì:** script **k6** bắn `GET /feed` với **100–200 VU** trên seed ≥ 2.000 user / 20.000 bài; báo cáo p95.
- **Vì sao:** giá trị của bước này là **phát hiện thiếu index sớm**, không phải con số đẹp. Ghi rõ cấu hình máy đo kèm kết quả.
- **Đầu ra:** p95 ≤ 500ms ở mức VU đã chốt; trượt thì có explain plan chỉ ra truy vấn chậm + index bù vào, sửa xong đo lại.

---

## Phase 4 — Comments + Reactions

### 4.1. Comments
- **Endpoint:** `POST /posts/{postId}/comments` · `PATCH /comments/{id}` · `DELETE /comments/{id}` · `GET /posts/{postId}/comments?cursor=` · `GET /comments/{id}/replies?cursor=`.
- **Entity:** `Comment` (`content` CK ≤ 2000, `parent_id` nullable, `deleted_at`) + migration.
- **Service:** `AssertDepth` server-side — cấp 4 → 400 (BR-08). Xoá giữ nhánh: node trả về "Bình luận đã bị xóa" (query cây dùng `IgnoreQueryFilters()` **có chủ đích**). `comment_count` trên `posts` cập nhật **cùng transaction** (SPEC 3.5). Bình luận vào bài phải qua kiểm quyền xem bài (BR-02) — gọi `PostsService`, không tự query bảng `posts`.
- **Đầu ra:** reply 3 cấp OK, cấp 4 → 400; xoá comment cha vẫn thấy reply con; `comment_count` khớp thực tế.

### 4.2. Reactions
- **Endpoint:** `PUT /reactions` (đặt/đổi loại) · `DELETE /reactions` — target là `(targetType, targetId)`.
- **Entity:** `Reaction` — **PK `(user_id, target_type, target_id)`** + migration.
- **Service:** thả loại khác = **thay thế** (BR-05); `reaction_counts` jsonb cập nhật cùng transaction. Kiểm target tồn tại trong service (không FK đa hình) qua service của module sở hữu.
- **Enum:** `ReactionType` **6 giá trị** `like/love/haha/wow/sad/angry` và `TargetType` khai ở `Common/Enums` (SPEC mục 7) — khoá của `reaction_counts` chính là tên các giá trị này.
- **Đầu ra:** react bài + comment chạy; đổi loại không nhân đôi; counter khớp sau chuỗi react/un-react.

---

## Phase 5 — Realtime: Conversations + Notifications

> Realtime đi lệch khuôn 7 bước ở chỗ transport (WebSocket thay HTTP), nhưng **bước 2, 4, 5 giữ
> nguyên**: handshake vẫn xác thực JWT, nghiệp vụ vẫn nằm ở service, payload đẩy xuống client vẫn
> là Response DTO chứ không phải entity.

### 5.1. Hạ tầng SignalR + Infra/Queue
- **Làm gì:** `AddSignalR().AddStackExchangeRedis(...)`, auth JWT qua `access_token` khi handshake, map `/hubs/chat` và `/hubs/notifications`. Dựng `Infra/Queue` (Redis Stream + `QueueConstants`).
- **Đầu ra:** client test nối hub bằng token hợp lệ; token sai → từ chối.

### 5.2. Conversations (ENT-06/07, BR-06/09, US-015)
- **Endpoint:** REST `POST /conversations` · `GET /conversations` · `GET /conversations/{id}/messages?cursor=` (**cursor theo `seq`, không OFFSET** — SPEC 3.6). Hub: gửi/nhận realtime, cập nhật Delivered/Seen.
- **Entity:** `Conversation` (**UQ(user_a, user_b) + CK a < b** — 1 hội thoại/cặp), `Message` (**`seq` UQ trong hội thoại; `client_msg_id` UQ idempotency**; trạng thái Sent/Delivered/Seen) + migration.
- **Service:** ghi tin + tăng `seq` + `last_message` **atomically** (SPEC 3.5). Chỉ bạn bè tạo hội thoại/nhắn tin; **unfriend → hội thoại chỉ đọc** (BR-09). Gửi trùng `client_msg_id` → trả bản cũ, không tạo tin mới. Membership kiểm ở service (BR-06).
- **Đầu ra:** **US-015/AC-01→04** pass (kể cả B offline nhận lại khi online, idempotency, 403 khi hết bạn); **TC-A04, TC-A07** có test.

### 5.3. Notifications
- **Endpoint:** `GET /notifications?cursor=` · `PATCH /notifications/{id}/read` · `PATCH /notifications/read-all` · badge chưa đọc. Push qua hub.
- **Entity:** `Notification` (**UQ(recipient_id, group_key)** — gộp; partial index `WHERE is_read = false`) + migration.
- **Service:** sinh khi có lời mời kết bạn, được accept, reaction/comment vào bài mình (đủ `NotificationType` ở SPEC mục 7). Fan-out qua Redis Stream + `NotificationFanoutWorker` — **job chỉ mang id, consumer đọc lại từ DB**. Upsert theo `group_key`, gộp thì tăng `actor_count` và đẩy `updated_at`.
- **Trả nợ 1.2:** chuyển gửi email sang queue.
- **Đầu ra:** A react bài B → B nhận realtime + badge; upsert theo `group_key` không chèn trùng; tắt worker → job chờ, bật lại xử lý nốt; email đăng ký vẫn tới MailHog (giờ qua queue). Nghiệm thu lại vế "được thông báo" của **US-010/AC-01**.

---

## Phase 6 — Moderation + Admin (UC-18/19/20, ENT-12/13)

### 6.1. Reports (UC-18)
- **Endpoint:** `POST /reports` (`report.create`) → 201.
- **Request DTO:** `CreateReportRequest(TargetType, TargetId, ReasonCode, Detail?)` — `reason_code` ngoài danh sách spam/harassment/nudity/violence/other → 400.
- **Entity:** `Report` (trạng thái open/resolved/dismissed **một chiều**) + migration.
- **Đầu ra:** tạo report trên Swagger; reason_code sai → 400.

### 6.2. Kiểm duyệt + AuditLog (UC-19)
- **Endpoint:** `GET /reports` (`report.read`) · `PATCH /reports/{id}` (`report.resolve`).
- **Entity:** `AuditLog` (cột theo SPEC 3.4a, **append-only**).
- **Service:** "ẩn nội dung" → post/comment `hidden` + report `Resolved`; "bỏ qua" → `Dismissed` — **kết luận + audit_log cùng transaction** (SPEC 3.5); xử lý lại report đã xử lý → 409.
- **Cách làm:** append-only cài bằng **trigger `BEFORE UPDATE OR DELETE … RAISE EXCEPTION`** trong migration, **không** dùng REVOKE: app chạy bằng role owner nên REVOKE không chặn được gì mà bạn lại tưởng đã xong.
- **Đầu ra:** **US-019/AC-01→04** pass; **TC-A06** có test; chạy `UPDATE audit_logs …` trong DBeaver bị từ chối.

### 6.3. Admin — người dùng & vai trò (UC-20)
- **Endpoint:** `PATCH /admin/users/{id}/lock` (`user.lock`) · `.../unlock` (`user.unlock`) · `PATCH /admin/users/{id}/role` (`role.assign`) · `GET /admin/audit-logs` (`audit.read`).
- **Service:** mọi thao tác ghi `audit_logs`; chặn user tự đổi role của chính mình (SPEC mục 4); user bị lock → `status = suspended`, không đăng nhập được.
- **Đầu ra:** **TC-A05** có test; audit ghi đủ ai / hành động / đối tượng / payload trước-sau.

### 6.4. RBAC động — hạ tầng phân quyền (SPEC mục 4)
- **Làm gì:** `[HasPermission("...")]` + `IAuthorizationPolicyProvider` động + `AuthorizationHandler<PermissionRequirement>` ở `Common` (đây chính là bước 2 của API-FLOW cho phần Authorization); đọc tập permission của user từ **cache Redis** TTL 300s, fallback DB. API quản lý role: tạo/sửa/xoá role, gán permission cho role (`role.manage`).
- **Cách làm:** **không** nhét permission vào JWT (token phình + đổi quyền phải chờ hết TTL 15 phút). Đổi role của user hoặc đổi `role_permissions` → **xoá cache ngay sau khi commit**, không chờ TTL. Hai chốt an toàn bắt buộc (SPEC mục 4): không gỡ `role.assign` khỏi role cuối cùng còn giữ nó; không xoá role `is_system`.
- **Đầu ra:** **TC-A08** có test — Admin gỡ permission khỏi role thì user thuộc role đó bị 403 **ngay**, không cần đăng nhập lại. Toàn bộ route quản trị/kiểm duyệt đã chuyển sang `[HasPermission]`, **không còn `[Authorize(Roles = …)]` nào trong codebase**.

### 6.5. Job định kỳ
- **Làm gì:** `BackgroundService` định kỳ: đối soát `comment_count`/`reaction_counts` với bản ghi thật; dọn `media_attachments` mồ côi; dọn `auth_tokens` hết hạn; dọn avatar không còn ai trỏ tới.
- **Đầu ra:** làm lệch counter bằng tay trong DB → job chạy → counter đúng lại; log job đọc được.

> **Chốt backend.** Rà Swagger toàn bộ theo DoD ở đầu file; **TC-A01→A08** đều có test trong CI;
> cập nhật CLAUDE.md "Trạng thái hiện tại".

---

## Phase 7 — Frontend nền móng (bước 1 và 7 của API-FLOW)

### 7.1. Scaffold socialmedia_web
- **Làm gì:** `create-next-app` (App Router, TS, Tailwind), cây `features/`, `components/ui/`, `lib/`.
- **Đầu ra:** `make dev-web`, `make typecheck-web`, `make lint-web` pass; `make check` chạy ĐỦ cả web.

### 7.2. lib/axios + auth — hiện thực bước 1 và 7
- **Làm gì:** `lib/axios.ts` (baseURL `/api/v1`, gắn `Authorization: Bearer`, token bridge, single-flight refresh, interceptor đọc ProblemDetails) — đây là chỗ **duy nhất** biết cách nói chuyện với API. `AuthProvider`, slice `auth`: đăng ký (+ màn "kiểm tra email"), đăng nhập (xử lý **401 / 403 chưa xác minh / 423 bị khóa** — US-002), **quên + đặt lại mật khẩu** (US-021), guard route.
- **Đầu ra:** đăng ký → verify qua MailHog → đăng nhập trên UI; quên mật khẩu → mail → đặt lại → đăng nhập bằng mật khẩu mới; F5 giữ phiên; access token hết hạn tự refresh.

### 7.3. UI kit tối thiểu
- **Làm gì:** design token + primitive: `Button`, `Input`, `Modal`, `Toast`, `Avatar`, `Skeleton`, `Spinner`, `ConfirmDialog`, `DropdownMenu`, `ErrorPanel`, `ImageGrid`, `Lightbox`, `Badge`.
- **Đầu ra:** trang demo đủ primitive; không hex thô (chỉ dùng token).

---

## Phase 8 — Frontend theo lát tính năng

> Mỗi slice có cấu trúc `api/ + queryKeys + hooks + components + errors + index`; mọi danh sách đủ
> 4 trạng thái loading / empty / error / success. Hành vi đối chiếu AC tương ứng trong SPEC mục 5.
> `api/` của slice khai **Request DTO / Response DTO đúng bằng contract backend** — lệch là sửa
> một phía, không "chữa" bằng cách map lung tung ở component.

### 8.1. `profile` — xem/sửa trang cá nhân, avatar (preview + chặn sớm > 10MB, server vẫn kiểm lại).
### 8.2. `friends` — tìm user (UC-16), lời mời, danh sách bạn; optimistic accept/decline.
### 8.3. `post` + `feed` — composer (chữ HOẶC ảnh — BR-01; chọn privacy **3 mức**), `useInfiniteQuery` + sentinel; bài `hidden` của mình hiển thị kèm lý do (BR-07).
### 8.4. `comments` + `reactions` — cây 3 cấp, node "Bình luận đã bị xóa", react optimistic + rollback khi API trả lỗi.
### 8.5. `chat` — `SignalRProvider` + `useChatHub`; trạng thái Sent/Delivered/Seen; gửi kèm `clientMsgId` (retry an toàn); hội thoại hết bạn hiển thị **chỉ đọc** (BR-09); reconnect → invalidate hội thoại đang mở.
### 8.6. `notifications` — chuông + badge chưa đọc (realtime), danh sách gộp theo `group_key`, đánh dấu đã đọc.
### 8.7. `moderation` — màn Moderator: hàng đợi report, xử lý ẩn/bỏ qua; màn Admin: khoá user, gán role, **quản lý role + tick permission cho role** (RBAC động 6.4). Route guard đọc claim `role` **chỉ để ẩn/hiện menu** — quyền thật do server quyết ở bước 2, FE không tự suy.

> **Chốt frontend.** Đi lại toàn bộ user journey: đăng ký → verify → kết bạn → đăng bài → tương tác
> → chat → thông báo → báo cáo → kiểm duyệt.

---

## Phase 9 — Deploy production (Dokploy)

### 9.1. Dockerfile x2
- **Làm gì:** multi-stage cho API (sdk → runtime, non-root) và web (Next.js standalone).
- **Đầu ra:** `docker build` cả hai chạy local bằng image prod.

### 9.2. Hạ tầng prod trên VPS
- **Làm gì:** qua Dokploy: Postgres (**16.15**, khớp local) + Redis + **SMTP thật** (thay MailHog); object storage trỏ **Cloudflare R2** (không chạy MinIO ở prod); secret qua biến môi trường (kể cả tài khoản admin đầu tiên); backup Postgres hằng ngày.
- **Đầu ra:** service chạy; secret không nằm trong repo; đã test restore 1 bản backup; upload ảnh trên prod vào đúng bucket R2 và pre-signed URL mở được.

### 9.3. Deploy API + web
- **Làm gì:** 2 app Dokploy; migration chạy trong bước deploy, **KHÔNG** auto-migrate lúc start; healthcheck `/api/health`; domain + HTTPS (TLS 1.2+ — SPEC mục 6); CORS đúng origin; Swagger tắt trên prod; rate limit bật.
- **Đầu ra:** domain thật + HTTPS; `/docs` không mở được; email xác minh gửi tới hộp thư thật; user journey chạy trên prod.

### 9.4. Vòng lặp sau deploy
- **Làm gì:** sửa code → `make check` → merge → Dokploy build → verify prod. Nợ mới ghi vào mục "Nợ kỹ thuật" dưới đây.
- **Đầu ra:** một thay đổi nhỏ đi hết vòng < 15 phút. **Sản phẩm hoàn chỉnh.**

---

## Nợ kỹ thuật (mỗi món phải có bước trả, hoặc ghi rõ là chấp nhận)

| Nợ | Nhận ở bước | Trả ở bước | Trạng thái |
|---|---|---|---|
| Gửi email đồng bộ trong request (chưa có queue) | 1.2 | 5.3 | đã có kế hoạch trả |
| `avatar_key` trỏ thư mục local, chưa lên object storage | 1.4 | 3.1 | đã có kế hoạch trả |
| Xoá cứng post/comment sau 90 ngày | SPEC 3.7 | 6.5 nếu còn thời gian | **chấp nhận nợ** nếu trượt |
| Ẩn danh PII tài khoản deactivated sau 30 ngày | SPEC 3.7 | 6.5 nếu còn thời gian | **chấp nhận nợ** nếu trượt |

---

## Nguyên tắc xuyên suốt

1. **Không sang bước sau khi bước trước chưa đạt đầu ra** — đầu ra trỏ thẳng mã AC/BR/TC trong
   `docs/SPEC.md`, copy nguyên văn vào prompt.
2. **Mọi endpoint đi đủ 7 bước của `docs/API-FLOW.md`** — không bỏ qua lớp nào để "cho nhanh":
   không nhét nghiệp vụ vào controller, không truy vấn DB từ controller, không trả entity ra ngoài.
3. **Schema + business rule lấy từ SPEC.md, cách viết code lấy từ CLAUDE.md + skill
   `socialmedia_convention`** — Claude Code chỉ tự quyết chi tiết hiện thực.
4. **Docs sống cùng code**: lệch là sửa cùng commit; CLAUDE.md "Trạng thái hiện tại" cập nhật cuối
   mỗi phase; nợ mới ghi vào bảng "Nợ kỹ thuật" và phải có bước trả.
5. **Phụ thuộc cứng:** test harness (0.5b) + CI (0.7) trước mọi bước đòi test · Storage (3.1) trước
   Posts (3.2) · Posts trước Comments/Reactions · SignalR + Queue (5.1) trước Chat/Notifications ·
   Moderation sau khi có Posts/Comments · **RBAC động (6.4) trước màn Admin/Moderator ở FE (8.7)** ·
   FE sau khi backend chốt.
6. **Hạ tầng local chỉ đến từ `docker compose`** — máy nào cài sẵn Postgres/Redis (kể cả trong WSL)
   phải tắt hẳn service đó, xem 0.1.
