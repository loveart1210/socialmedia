# Luật code — `socialmedia_api` (ASP.NET Core .NET 10 + EF Core / PostgreSQL)

## 1. Bố cục một module

Tổ chức theo **feature folder**, không theo tầng kỹ thuật (không có thư mục
`Controllers/`, `Services/` chung toàn app):

```
src/SocialMedia.Api/
├─ Modules/
│  └─ <Module>/                      # Posts, Comments, Reactions, …
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
├─ Infra/                           # DbContext, Redis, Storage, Queue
└─ Program.cs
```

Thêm module mới = thêm 1 dòng `builder.Services.AddXModule()` vào `Program.cs`.

### Đặt code ở tầng nào

| Tầng | Chứa gì | Ví dụ |
|---|---|---|
| `Modules/<X>` | Nghiệp vụ của **riêng một domain** | `Posts`, `Comments` |
| `Common/*` | Hạ tầng **kỹ thuật**, không biết nghiệp vụ | `Pagination`, `Enums`, `Exceptions`, filter/middleware |
| `Infra/*` | Adapter tới hệ thống ngoài tiến trình | `AppDbContext`, `Redis`, `Storage` (object storage), `Queue` |

**Mặc định KHÔNG tạo tầng nghiệp vụ dùng chung** (`Shared/`, `Core/`). Luật
nghiệp vụ ở đúng module sở hữu nó; module khác cần thì inject service của
module đó (đã đăng ký DI trong `AddXModule`). Ví dụ: `Comments` cần biết bài
viết tồn tại → inject `PostsService`, không tự query bảng `posts`.

Chiều phụ thuộc **một chiều**: `Modules → Common/Infra`. `Common/*` không được
import `Modules/**`, không chạm `AppDbContext`, không ném exception HTTP.

Ngoại lệ duy nhất kiểu "đa hình dùng chung": enum `TargetType`
(POST/COMMENT) cho `Reactions` và `Notifications` — nằm ở `Common/Enums`,
xem mục 2.

## 2. Entity & database

```csharp
public class Comment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }          // GIỮ khoá thô, navigation riêng
    public Post? Post { get; set; }
    public Guid? ParentId { get; set; }       // reply — tối đa 3 cấp, service kiểm
    public Comment? Parent { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }   // soft delete
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("cmt_comments");
        b.HasIndex(x => new { x.PostId, x.DeletedAt, x.CreatedAt });
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Quy ước:

- Tên bảng snake_case + prefix domain: `au_` (auth) · `usr_` (users) · `frd_` (friendships) · `flw_` (follows) · `pst_` (posts) · `cmt_` (comments) · `rct_` (reactions) · `cnv_` (conversations) · `ntf_` (notifications).
- Khoá chính `Guid` (uuid v7 — `Guid.CreateVersion7()`), khoá ngoại luôn có field thô `XxxId` + navigation cùng tên không hậu tố.
- `CreatedAt/UpdatedAt` là `timestamptz`, gán ở `SaveChangesAsync` override trong `AppDbContext` — entity không tự gán.
- Soft delete bằng `DeletedAt`; **global query filter** `HasQueryFilter(x => x.DeletedAt == null)` cho mọi entity có field này. Cần đọc cả bản ghi đã xoá thì `IgnoreQueryFilters()` có chủ đích.
- Mỗi truy vấn nóng phải có index tương ứng, khai ngay trong `Configuration`.
- Trạng thái suy được từ dữ liệu khác thì **không lưu** (số reaction/comment đếm bằng query hoặc cache Redis, không lưu counter trên `Post` ở giai đoạn này — hai nguồn sự thật thì cái thứ hai sẽ lệch).
- Migration: `dotnet ef migrations add <Ten>` — review SQL sinh ra trước khi commit. **Không** auto-migrate lúc app khởi động ở prod (chạy trong pipeline deploy, xem ARCHITECTURE.md mục 6).

### Tham chiếu đa hình (`Reaction`, `Notification`)

Khi một field trỏ tới "một trong nhiều loại bản ghi", **không** khai chuỗi
`"POST" | "COMMENT"` tại chỗ. Dùng enum `TargetType` ở `Common/Enums` — nơi
duy nhất liệt kê các loại bị trỏ tới:

- `Reaction` có `TargetType TargetType` + `Guid TargetId`, unique index
  `(UserId, TargetType, TargetId)` — một user một reaction/target.
- Giá trị chuỗi (serialize bằng `JsonStringEnumConverter`) **đi thẳng ra API**:
  đổi tên giá trị = đổi contract, thêm giá trị mới thì tương thích ngược.
- Không có FK vật lý cho `TargetId` (Postgres không FK đa hình) → service tạo
  reaction phải tự kiểm target tồn tại; xoá mềm post/comment thì reaction đi
  theo bằng nghiệp vụ, không dựa cascade.

## 3. DTO & mapper

Trong `<X>Response.cs` tách bạch:

1. **Record response** (`CommentResponse`) — chỉ để trả về + sinh OpenAPI. Không
   trả entity ra ngoài, bao giờ.
2. **Mapper thuần** (`static CommentResponse ToResponse(this Comment c, bool canEdit)`)
   — không I/O, không quyết định phân quyền; nhận cờ từ service.

DTO request:

- `record` + validator FluentValidation khai cùng file, đăng ký tự động qua
  `AddValidatorsFromAssembly`.
- Chuỗi người dùng nhập: `.Transform(v => v.Trim())` trong validator.
- Bật `JsonSerializerOptions.UnmappedMemberHandling = Disallow` → field không
  khai trong DTO bị **từ chối 400**, không phải bỏ qua (khớp ARCHITECTURE.md mục 3).
- Cho phép `null` có chủ đích (gỡ liên kết): dùng kiểu `Optional<T>` của
  `Common` để phân biệt "không gửi" và "gửi null" trong PATCH.

## 4. Phân quyền

- AuthN: JWT Bearer, fallback policy `RequireAuthenticatedUser` → **mọi route
  mặc định cần token**; mở public bằng `[AllowAnonymous]`.
- AuthZ: RBAC (`User`, `Admin`) bằng `[Authorize(Roles = ...)]` cho route quản trị.
- Quyền trên **bản ghi cụ thể** (sửa/xoá bài của mình, xem tin nhắn của hội
  thoại mình tham gia) kiểm **trong service**: `AssertOwner()`,
  `AssertParticipant()` — không viết policy handler cho từng resource.
- Quyền theo **quan hệ** (xem bài friends-only, nhắn tin) kiểm qua service của
  module quan hệ: `FriendshipsService.AreFriendsAsync(a, b)` — `Posts` không
  tự query bảng `frd_`.
- Không tin `userId` từ client: luôn lấy từ claim `sub` qua extension
  `User.GetUserId()`.
- SignalR hub: cùng JWT (query `access_token` khi handshake), hub method kiểm
  quyền y như service (vào group hội thoại phải là participant).

## 5. Service

- Guard clause + hàm private nhỏ (`LoadOr404`, `AssertDepth`, `BuildFeed`)
  thay vì hàm dài lồng nhiều tầng.
- Ghi nhiều bảng liên quan → transaction:
  `await using var tx = await _db.Database.BeginTransactionAsync()` (kết bạn:
  cập nhật request + tạo friendship + tạo notification là một giao dịch).
- Không đọc `Environment.GetEnvironmentVariable`; bind options qua
  `IOptions<XOptions>` với `ValidateOnStart`.
- Danh sách có phân trang → **cursor-based** (`CursorPage<T>` của `Common`,
  cursor = `(CreatedAt, Id)`) cho feed/comment/message — offset paging chỉ cho
  danh sách quản trị. Danh sách ngắn theo cha (ảnh của bài) trả mảng thuần.
- Đếm theo lô bằng một query `GroupBy`, không đếm trong vòng lặp
  (reaction count của 20 bài trong feed = 1 query).
- Reply cấp 4 bị **từ chối ở service** (`AssertDepth`): đọc độ sâu của parent,
  không tin client.

## 6. Controller

- Chỉ có: attribute route, `[ProducesResponseType]`/Swagger annotation, lấy
  `User.GetUserId()`/`[FromRoute]`/`[FromBody]`, `return await _service.X(...)`.
- Mã trạng thái: tạo → 201 `CreatedAtAction`, xoá mềm → 204 trả `void`, hành
  động (accept/decline/react) → 200.
- Upload ảnh: `[RequestSizeLimit]` + kiểm ở service (≤ 10MB/ảnh, ≤ 10 ảnh/bài,
  content-type whitelist) — **kiểm bằng magic bytes**, không tin extension.
- Lỗi trả về theo **ProblemDetails** thống nhất qua exception middleware ở
  `Common` — controller/service không tự dựng response lỗi.

## 7. Gọi dịch vụ ngoài

Module nghiệp vụ **không bao giờ** cầm `HttpClient`/SDK gọi thẳng ra ngoài.
Mỗi dịch vụ ngoài có đúng một adapter ở `Infra/*`:

| Dịch vụ | Adapter | Dùng ở |
|---|---|---|
| Object storage (ảnh bài viết, avatar) | `StorageService` | `Posts`, `Users` |
| Redis (cache, presence, SignalR backplane) | `CacheService` + `AddStackExchangeRedis` | feed cache, online status |

Adapter chịu trách nhiệm dịch lỗi mạng thành exception có nghĩa
(`BadGateway`, `GatewayTimeout`) + gắn `X-Request-Id` vào log.

## 8. Việc chạy nền

Dùng khi công việc **mất hàng giây trở lên hoặc fan-out nhiều bản ghi** — giữ
nó trong request nghĩa là người đăng bài phải chờ hệ thống báo cho 500 follower
(mẫu: fan-out notification tách khỏi `CreatePost()`).

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

## 9. Kiểm tra

```bash
make build-api          # dotnet build -warnaserror
make test-api           # dotnet test
make format-api         # dotnet format --verify-no-changes
```

`make check` = `build-api + format-api + test-api + typecheck-web + lint-web`.
Migration mới phải kèm chạy thử `dotnet ef database update` trên DB local
trước khi commit.
