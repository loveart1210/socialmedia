# Luật code — `socialmedia_api` (ASP.NET Core .NET 10 + EF Core / PostgreSQL)

> Nghiệp vụ (schema từng cột, BR-*, ma trận quyền, acceptance): `docs/SPEC.md`.
> File này chỉ quy định CÁCH VIẾT; nội dung nghiệp vụ lấy từ SPEC, lệch thì SPEC thắng.

## 1. Bố cục một module

Tổ chức theo **feature folder**, không theo tầng kỹ thuật (không có thư mục
`Controllers/`, `Services/` chung toàn app):

```
src/SocialMedia.Api/
├─ Modules/
│  └─ <Module>/                      # Posts, Comments, Moderation, …
│     ├─ <Module>Controller.cs      # chỉ định tuyến + Swagger + gọi service
│     ├─ <Module>Service.cs         # toàn bộ nghiệp vụ + phân quyền
│     ├─ <Module>Module.cs          # extension AddXModule(IServiceCollection)
│     ├─ Dtos/
│     │  ├─ Create<X>Request.cs     # record + FluentValidation validator cùng file
│     │  ├─ Update<X>Request.cs
│     │  ├─ List<X>Query.cs
│     │  └─ <X>Response.cs          # record response + mapper thuần (static)
│     ├─ Entities/
│     │  └─ <X>.cs                  # entity + IEntityTypeConfiguration cùng file
│     └─ Hubs/                      # chỉ Conversations, Notifications có
├─ Common/                          # hạ tầng kỹ thuật, không biết nghiệp vụ
├─ Infra/                           # DbContext, Redis, Storage, Mail, Queue
└─ Program.cs
```

Danh sách module: xem ARCHITECTURE.md mục 4 (gồm cả `Moderation`, `Admin`).
Thêm module mới = thêm 1 dòng `builder.Services.AddXModule()` vào `Program.cs`.

### Đặt code ở tầng nào

| Tầng | Chứa gì | Ví dụ |
|---|---|---|
| `Modules/<X>` | Nghiệp vụ của **riêng một domain** | `Posts`, `Comments`, `Moderation` |
| `Common/*` | Hạ tầng **kỹ thuật**, không biết nghiệp vụ | `Pagination`, `Enums`, `Exceptions`, filter/middleware |
| `Infra/*` | Adapter tới hệ thống ngoài tiến trình | `AppDbContext`, `Redis`, `Storage`, `Mail`, `Queue` |

**Mặc định KHÔNG tạo tầng nghiệp vụ dùng chung** (`Shared/`, `Core/`). Luật
nghiệp vụ ở đúng module sở hữu nó; module khác cần thì inject service của
module đó (đã đăng ký DI trong `AddXModule`). Ví dụ: `Comments` cần biết bài
viết tồn tại → inject `PostsService`, không tự query bảng `posts`.

Chiều phụ thuộc **một chiều**: `Modules → Common/Infra`. `Common/*` không được
import `Modules/**`, không chạm `AppDbContext`, không ném exception HTTP.

Ngoại lệ duy nhất kiểu "đa hình dùng chung": enum `TargetType`
(POST/COMMENT) cho `Reactions`, `Notifications`, `Moderation` (report trỏ
nội dung) — nằm ở `Common/Enums`, xem mục 2.

## 2. Entity & database

```csharp
public class Comment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }          // GIỮ khoá thô, navigation riêng
    public Post? Post { get; set; }
    public Guid? ParentId { get; set; }       // reply — tối đa 3 cấp (BR-08), service kiểm
    public Comment? Parent { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }   // soft delete — giữ nhánh trả lời
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments");
        b.HasIndex(x => new { x.PostId, x.CreatedAt });
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Quy ước:

- **Tên bảng/cột theo đúng SPEC.md mục 3** — snake_case, KHÔNG prefix
  (`users`, `posts`, `comments`, `friendships`, `media_attachments`…). Cột,
  kiểu, CHECK/UNIQUE lấy nguyên từ bảng 3.2–3.4 của SPEC, không tự chế thêm/bớt.
- Khoá chính: mặc định `Guid` uuid v7 (`Guid.CreateVersion7()`). **Ngoại lệ
  composite PK theo SPEC**: `friendships (user_min, user_max)`,
  `follows (follower, followee)`, `reactions (user, target_type, target_id)`,
  `profiles (user_id)`. `audit_logs` dùng bigserial.
- Khoá ngoại luôn có field thô `XxxId` + navigation cùng tên không hậu tố.
- `CreatedAt/UpdatedAt` là `timestamptz`, gán ở `SaveChangesAsync` override
  trong `AppDbContext` — entity không tự gán.
- **Hai cơ chế "xoá", chọn theo SPEC**:
  - Entity SPEC định nghĩa cột `status` (`posts`: published/hidden/deleted;
    `users`; `reports`) → dùng `status`, KHÔNG thêm `DeletedAt`. Không đặt
    global query filter cho `posts` — bài `hidden` tác giả vẫn thấy kèm lý do
    (BR-07), tức là điều kiện hiển thị phụ thuộc người xem → lọc trong service.
  - Entity không có `status` (`comments`…) → `DeletedAt` + global query filter
    `HasQueryFilter(x => x.DeletedAt == null)`; đọc cả bản ghi đã xoá thì
    `IgnoreQueryFilters()` có chủ đích. Comment xoá vẫn phải trả về node
    "Bình luận đã bị xóa" để giữ nhánh (BR-08) → truy vấn cây comment là chỗ
    dùng `IgnoreQueryFilters()` hợp lệ.
- Mỗi truy vấn nóng phải có index tương ứng (danh sách bắt buộc: SPEC.md 3.6 —
  gồm partial index và GIN pg_trgm), khai ngay trong `Configuration`; index
  đặc thù Postgres mà EF không tả được thì viết SQL trong migration.
- **Extension Postgres** bật bằng migration: `citext` (email), `pg_trgm` +
  `unaccent` (tìm user — UC-16). `unaccent()` **không IMMUTABLE** nên không
  index trực tiếp được: tạo một wrapper `IMMUTABLE` bọc nó rồi mới index trên
  biểu thức đó. Cùng chỗ này khai trigger chặn UPDATE/DELETE trên
  `audit_logs` (`BEFORE UPDATE OR DELETE … RAISE EXCEPTION`) — **không** dùng
  REVOKE, vì app chạy bằng role owner nên REVOKE không chặn được gì.
- **Không lưu trạng thái suy được — trừ ngoại lệ SPEC chỉ định**:
  `posts.comment_count` + `posts.reaction_counts` (jsonb) là bộ đếm phi chuẩn
  hóa có chủ đích. Điều kiện đi kèm: cập nhật **cùng transaction** với bản ghi
  thật (SPEC 3.5) + job đêm đối soát (SPEC 3.7). Ngoài hai cột này, cấm thêm
  counter/trạng thái dẫn xuất mới.
- Seed idempotent trong migration: `roles` (1=User, 2=Moderator, 3=Admin,
  `is_system = true`), `permissions` (toàn bộ danh sách SPEC.md mục 7 — nguồn
  là enum trong code), `role_permissions` (ma trận SPEC mục 4 là **giá trị
  khởi tạo**, Admin sửa được sau đó), `reason_code`; admin đầu tiên qua biến
  môi trường (SPEC 3.7).
- Migration: `dotnet ef migrations add <Ten>` — review SQL sinh ra trước khi
  commit; backward-compatible 1 phiên bản (expand–contract). **Không**
  auto-migrate lúc app khởi động ở prod (chạy trong pipeline deploy).

### Tham chiếu đa hình (`Reaction`, `Notification`, `Report`)

Khi một field trỏ tới "một trong nhiều loại bản ghi", **không** khai chuỗi
`"POST" | "COMMENT"` tại chỗ. Dùng enum `TargetType` ở `Common/Enums` — nơi
duy nhất liệt kê các loại bị trỏ tới:

- `Reaction`: PK `(UserId, TargetType, TargetId)` — một user một cảm xúc/target,
  thả loại khác là **thay thế**, không nhân đôi (BR-05).
- Giá trị chuỗi (serialize bằng `JsonStringEnumConverter`) **đi thẳng ra API**:
  đổi tên giá trị = đổi contract, thêm giá trị mới thì tương thích ngược.
- Không có FK vật lý cho `TargetId` (Postgres không FK đa hình) → service tạo
  reaction/report phải tự kiểm target tồn tại; gỡ/ẩn post-comment thì bản ghi
  trỏ tới đi theo bằng nghiệp vụ, không dựa cascade.

## 3. DTO & mapper

Trong `<X>Response.cs` tách bạch:

1. **Record response** (`CommentResponse`) — chỉ để trả về + sinh OpenAPI.
   Không trả entity ra ngoài, bao giờ. `password_hash`, `failed_login_count`,
   `locked_until` (data class Secret/Internal trong SPEC 3.2) **không bao giờ**
   xuất hiện trong bất kỳ response nào.
2. **Mapper thuần** (`static CommentResponse ToResponse(this Comment c, bool canEdit)`)
   — không I/O, không quyết định phân quyền; nhận cờ từ service.

DTO request:

- `record` + validator FluentValidation khai cùng file, đăng ký tự động qua
  `AddValidatorsFromAssembly`.
- Giới hạn validate lấy từ SPEC (content ≤ 5.000 ký tự, display_name 1–50,
  username `[a-z0-9_.]` ≤ 30, reason_code thuộc danh sách chuẩn…), không bịa số.
- Chuỗi người dùng nhập: `.Transform(v => v.Trim())` trong validator.
- Bật `JsonSerializerOptions.UnmappedMemberHandling = Disallow` → field không
  khai trong DTO bị **từ chối 400**, không phải bỏ qua.
- Cho phép `null` có chủ đích (gỡ liên kết): dùng kiểu `Optional<T>` của
  `Common` để phân biệt "không gửi" và "gửi null" trong PATCH.

## 4. Phân quyền

Nguyên tắc: **default deny, RBAC động** theo SPEC.md mục 4. Tám test
TC-A01→A08 (SPEC mục 4) là hợp đồng tối thiểu, chạy trong CI.

- AuthN: JWT Bearer (HS256, TTL 15 phút, claims `sub`/`role`/`jti`), fallback
  policy `RequireAuthenticatedUser` → **mọi route mặc định cần token**; mở
  public bằng `[AllowAnonymous]` (đăng ký, đăng nhập, quên mật khẩu, xem nội
  dung public — cột Guest trong ma trận).
- AuthZ: kiểm bằng **permission code**, không bằng tên role —
  `[HasPermission("report.resolve")]` + `IAuthorizationPolicyProvider` động +
  `AuthorizationHandler<PermissionRequirement>` ở `Common`. **Không dùng
  `[Authorize(Roles = ...)]`** ở bất kỳ đâu: role là dữ liệu sửa được lúc chạy,
  gắn tên role vào code là quay lại hard-code.
- Danh sách permission code **cố định trong code** (enum, SPEC mục 7) vì mỗi mã
  phải có chỗ kiểm; role và `role_permissions` thì **động** qua màn Admin.
  Một user giữ đúng **một** role.
- Nguồn quyền lúc kiểm: đọc tập permission của user từ **cache Redis**
  (TTL 300s, key theo user), fallback DB. **Không nhét permission vào JWT** —
  token phình và mọi thay đổi quyền phải chờ hết TTL 15 phút. Đổi role của user
  hoặc đổi `role_permissions` → **xoá cache ngay** sau khi transaction commit,
  không chờ TTL hết hạn (TC-A08 kiểm đúng điều này).
- Claim `role` trong JWT chỉ để frontend render menu — server không bao giờ
  quyết định quyền dựa trên nó.
- Hai chốt an toàn bắt buộc: không gỡ `role.assign` khỏi role cuối cùng còn giữ
  nó, không xoá role `is_system`, và không cho user tự đổi role của chính mình.
- Quyền trên **bản ghi cụ thể** (`✔(own)` trong ma trận — sửa/xoá bài của mình,
  đọc hội thoại mình tham gia) kiểm **trong service**: `AssertOwner()`,
  `AssertParticipant()` — không viết policy handler cho từng resource. Đây là
  tuyến chống IDOR (rủi ro Critical — TC-A03/A04).
- Quyền theo **quan hệ** (`✔(bạn)` — xem bài `friends`, nhắn tin BR-06/BR-09)
  kiểm qua service của module quan hệ: `FriendshipsService.AreFriendsAsync(a, b)`
  (có cache Redis TTL 60s) — `Posts` không tự query bảng `friendships`.
- Auth: mật khẩu **BCrypt cost 12**; refresh token lưu **băm**, xoay vòng, phát
  hiện reuse → thu hồi cả chuỗi; lockout sai 5 lần/15 phút → 423; chưa xác minh
  email → 403 (SPEC US-002).
- Không tin `userId` từ client: luôn lấy từ claim `sub` qua extension
  `User.GetUserId()`.
- SignalR hub: cùng JWT (query `access_token` khi handshake), hub method kiểm
  quyền y như service (vào group hội thoại phải là participant).
- Mọi thao tác Moderator/Admin ghi `audit_logs` (append-only) **cùng
  transaction** với thao tác (SPEC 3.5) — quên audit là bug phân quyền.

## 5. Service

- Guard clause + hàm private nhỏ (`LoadOr404`, `AssertDepth`, `BuildFeed`)
  thay vì hàm dài lồng nhiều tầng.
- Ranh giới transaction lấy từ SPEC.md 3.5, không tự quyết: tạo User + Profile;
  bài + media + bộ đếm; Pending→Accepted; ghi tin + tăng `seq` + last_message;
  kết luận report + audit_log — mỗi cụm là **một** transaction
  (`await using var tx = await _db.Database.BeginTransactionAsync()`).
- Không đọc `Environment.GetEnvironmentVariable`; bind options qua
  `IOptions<XOptions>` với `ValidateOnStart`.
- Danh sách có phân trang → **cursor-based** (`CursorPage<T>` của `Common`):
  feed/comment cursor `(CreatedAt, Id)`; **tin nhắn cursor theo `seq`**, không
  OFFSET (SPEC 3.6). Offset paging chỉ cho danh sách quản trị. Danh sách ngắn
  theo cha (ảnh của bài) trả mảng thuần.
- Gửi tin idempotent theo `client_msg_id`: trùng `(conversation_id,
  client_msg_id)` → trả bản ghi cũ, không tạo mới (SPEC US-015/AC-03).
- Đọc theo lô bằng một query `GroupBy`, không query trong vòng lặp; số
  reaction/comment của feed đọc từ counter trên `posts` (ngoại lệ mục 2),
  không đếm lại mỗi request.
- Reply cấp 4 bị **từ chối ở service** (`AssertDepth`): đọc độ sâu của parent,
  không tin client (BR-08).

## 6. Controller

- Chỉ có: attribute route, `[ProducesResponseType]`/Swagger annotation, lấy
  `User.GetUserId()`/`[FromRoute]`/`[FromBody]`, `return await _service.X(...)`.
- Mã trạng thái: tạo → 201 `CreatedAtAction`; xoá → 204 trả `void`; hành động
  (accept/decline/react/resolve) → 200; trùng/đã xử lý → 409; **tài khoản bị
  khóa → 423** (SPEC US-002/AC-03).
- Upload ảnh: `[RequestSizeLimit]` + kiểm ở service (≤ 10MB/ảnh, ≤ 10 ảnh/bài
  — BR-01, content-type whitelist) — **kiểm bằng magic bytes**, không tin
  extension; server **re-encode ảnh** trước khi đẩy storage (SPEC mục 6).
- Rate limit (`AddRateLimiter`) cho nhóm auth và đăng bài/bình luận (SPEC mục 6).
- Lỗi trả về theo **ProblemDetails** thống nhất qua exception middleware ở
  `Common` — controller/service không tự dựng response lỗi.

## 7. Gọi dịch vụ ngoài

Module nghiệp vụ **không bao giờ** cầm `HttpClient`/SDK gọi thẳng ra ngoài.
Mỗi dịch vụ ngoài có đúng một adapter ở `Infra/*`:

| Dịch vụ | Adapter | Dùng ở |
|---|---|---|
| Object storage (ảnh — pre-signed URL) | `StorageService` — **`AWSSDK.S3`**, `ForcePathStyle = true`; local MinIO, prod Cloudflare R2 (`region = auto`) | `Posts`, `Users` |
| Email (xác minh, đặt lại mật khẩu) | `MailService` (`Infra/Mail`) | `Auth` |
| Redis (cache, presence, SignalR backplane) | `CacheService` + `AddStackExchangeRedis` | feed cache, quan hệ bạn bè, online status |

Adapter chịu trách nhiệm dịch lỗi mạng thành exception có nghĩa
(`BadGateway`, `GatewayTimeout`) + gắn `X-Request-Id` vào log. Gửi email đi
qua hàng đợi (mục 8), không chặn request đăng ký.

## 8. Việc chạy nền

Dùng khi công việc **mất hàng giây trở lên, gọi mạng ra ngoài, hoặc fan-out
nhiều bản ghi** — giữ nó trong request nghĩa là người đăng bài phải chờ hệ
thống báo cho 500 follower (mẫu: fan-out notification tách khỏi `CreatePost()`,
gửi email xác minh tách khỏi `Register()`).

- Hàng đợi chạy trên **Redis Stream** qua `Infra/Queue`; tên stream + kiểu
  payload khai ở `Infra/Queue/QueueConstants.cs` — **nguồn duy nhất** cho cả
  bên đẩy lẫn bên xử lý.
- **Job chỉ mang id**, không mang dữ liệu. Consumer đọc lại từ PostgreSQL khi
  tới lượt — dữ liệu trong queue vài phút là dữ liệu có thể đã đổi.
- Consumer là `BackgroundService` thuộc **module sở hữu nghiệp vụ**
  (`NotificationFanoutWorker` nằm trong `Modules/Notifications`), không nằm ở
  `Infra`.
- Bản ghi không còn (bị xoá giữa chừng) → **log warning rồi ack**, đừng ném
  lỗi để retry vô ích.
- Notification gộp theo `UQ(recipient_id, group_key)` — worker upsert theo
  group_key, không chèn trùng (SPEC 3.4).
- **Job định kỳ** (đối soát bộ đếm, dọn media mồ côi, xóa cứng 90 ngày, ẩn
  danh PII 30 ngày — SPEC 3.7) cũng là `BackgroundService` của module sở hữu,
  lịch chạy khai trong options; hiện là nợ kỹ thuật (ARCHITECTURE.md mục 7).

## 9. Kiểm tra

```bash
make build-api          # dotnet build -warnaserror
make test-api           # dotnet test
make format-api         # dotnet format --verify-no-changes
```

`make check` = `build-api + format-api + test-api` (+ `typecheck-web + lint-web`
khi `socialmedia_web` đã tồn tại — trước đó tự bỏ qua).

- Test tích hợp chạy trên **Postgres thật qua Testcontainers** +
  `WebApplicationFactory<Program>` (harness dựng ở ROADMAP 0.5b), **không** dùng
  DB dev và **không** dùng InMemory provider — global query filter, CHECK
  constraint và index partial chỉ đúng trên Postgres.
- Test tối thiểu cho phần phân quyền: **TC-A01→A08** (SPEC mục 4), chạy trong
  CI (ROADMAP 0.7); tiêu chí nghiệm thu tính năng lấy từ acceptance criteria
  SPEC mục 5.
- Migration mới phải kèm chạy thử `make migrate-api` trên DB local trước khi
  commit.
