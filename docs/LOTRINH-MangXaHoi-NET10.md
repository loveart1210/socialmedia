# Lộ trình xây dựng mạng xã hội — từ số 0 tới production

> **Stack chốt cứng**: Backend ASP.NET Core (.NET 10) · CSDL PostgreSQL 16 (+ DBeaver để xem) ·
> Hợp đồng API mô tả bằng OpenAPI, xem/thử bằng Swagger UI · AuthN JWT · AuthZ RBAC ·
> Redis · Frontend tùy chọn · Deploy bằng Dokploy.
>
> **Tài liệu này trả lời đúng một câu hỏi**: *"Bây giờ tôi phải làm gì tiếp theo, và bắt đầu
> từ file nào?"*
>
> Khuôn lộ trình lấy từ `route.md` (rút ra từ repo Edumate); nội dung nghiệp vụ, ERD, ma trận
> RBAC, BR/FR/NFR lấy từ `BaoCao_Nhom5_v4.docx`. Không có ước lượng thời gian — **thứ tự**
> mới là thứ không được đổi.

---

## Cách dùng tài liệu này

Mỗi bước có cấu trúc cố định. Khi vào việc:

1. Xác định đang ở bước nào bằng [bảng tra nhanh](#tôi-đang-ở-bước-nào).
2. Kiểm tra **Điều kiện vào** — chưa thỏa thì quay lại bước trước. **Không nhảy cóc.**
3. Bắt đầu đúng ở dòng **`▶ Bắt đầu từ`** — đó là hành động cụ thể đầu tiên.
4. Làm theo **Thứ tự thực hiện**, không đảo.
5. Đọc **Không làm gì ở bước này** trước khi "làm thêm cho tiện".
6. Chỉ coi là xong khi **Chốt kiểm tra** chạy được thật.

---

## Nguyên tắc sắp thứ tự

**Xây từ thứ đắt-để-đổi-sau tới thứ rẻ-để-đổi-sau.**

```
ĐẮT NHẤT ──────────────────────────────────────────────────────► RẺ NHẤT

① Ranh giới   ② Mô hình    ③ Hợp đồng   ④ Enum/hằng  ⑤ Design   ⑥ Compo-  ⑦ Chuỗi
   module        dữ liệu       API         dùng chung    token      nent      hiển thị

đổi = viết    đổi = EF     đổi = sửa   đổi = lỗi     đổi = sửa  đổi tự  đổi 1
lại hệ thống  migration    mọi client  biên dịch     cơ học     do      dòng
              + mất data               (compiler bắt)
```

Hệ quả: **luật và ranh giới trước code** · **schema trước endpoint** · **endpoint trước giao
diện**. Đảo thứ tự là tự chuốc việc làm lại.

---

## Tôi đang ở bước nào?

Trả lời lần lượt, dừng ở câu **"Chưa"** đầu tiên — đó là bước phải làm.

| # | Câu hỏi | Chưa → làm bước |
|---|---|---|
| 1 | Đã có `ARCHITECTURE.md` nói rõ **module nào sở hữu bảng nào** và sơ đồ chiều gọi chưa? | **B0** |
| 2 | `make help` / `make doctor` chạy được, `dotnet build` xanh chưa? | **B1** |
| 3 | Xóa một biến cấu hình bắt buộc thì app **chết lúc boot** chưa? | **B2** |
| 4 | `GET /health/live` trả 200, log báo kết nối PostgreSQL thành công, DBeaver mở được DB chưa? | **B3** |
| 5 | Đăng nhập lấy được JWT; endpoint không `[AllowAnonymous]` trả 401; Swagger UI mở được chưa? | **B4** |
| 6 | (Nếu làm frontend) Đăng nhập được **trên trình duyệt**, hết hạn token thì tự refresh chưa? | **B5** |
| 7 | Đã có **một** module chạy thông từ bảng PostgreSQL lên tới client chưa? | **B6** |
| 8 | Đã phủ hết entity trong ERD (mục 5.2 báo cáo) chưa? | **B7** |
| 9 | Có chỗ nào **thật sự** trùng lặp giữa ≥2 module chưa xử lý không? | **B8** |
| 10 | Push code lên nhánh chính là Dokploy tự build + deploy chưa? | **B9** |

---

## CSDL và API nằm ở đâu — bảng tra

Hai câu hỏi hay bị hiểu sai nhất. **Cả hai đều không phải "một bước"**.

### PostgreSQL

| Việc | Bước | Nơi đặt |
|---|---|---|
| Chọn quy ước đặt tên · schema theo module · ERD | **B0** | **Tài liệu** (`docs/`), không phải code |
| Chuỗi kết nối + validate fail-fast | **B2** | `Configuration/DatabaseOptions.cs` · `.env.example` |
| **Mở kết nối — không có entity nào** | **B3** | `Infrastructure/Persistence/AppDbContext.cs` |
| Helper transaction | **B3** | `Infrastructure/Persistence/UnitOfWork.cs` |
| **Entity + `IEntityTypeConfiguration` + index của một module** | **B4** (Identity), **B6**, lặp ở **B7** | `Modules/<X>/Domain/` + `Modules/<X>/Persistence/Configurations/` |
| Migration đầu tiên | **B4** | `Infrastructure/Persistence/Migrations/` |
| Seed reference data (roles, reason_code) | **B4** (khai) → **B9** (chạy tự động) | `Infrastructure/Persistence/Seed/` |
| Đọc/soi dữ liệu bằng **DBeaver** | **B3** (dev) → **B9** (prod qua SSH tunnel) | Công cụ, không phải code |
| Tối ưu index theo slow query thật | **B9** | `EXPLAIN ANALYZE` trong DBeaver |

> **Không có bước "dựng toàn bộ CSDL".** Đây là chủ ý. `DbSet` và `IEntityTypeConfiguration`
> được đăng ký **phân tán** ở module sở hữu, nên đọc code là biết ngay bảng nào thuộc ai.
> Một thư mục `Entities/` tập trung xóa mất thông tin đó và biến mọi thay đổi schema thành
> một file bị tranh chấp giữa 3 người.
>
> **Nhưng vẫn phải chốt ERD ở B0.** Quan hệ giữa entity là thứ đắt nhất để sửa. Phân biệt rõ:
> *thiết kế mô hình dữ liệu sớm* (nên) ≠ *gom file entity vào một chỗ* (không nên).

### API

| Việc | Bước | Nơi đặt |
|---|---|---|
| Quyết định hợp đồng: prefix · versioning · auth model · shape phân trang · shape lỗi | **B0** | **Tài liệu** |
| **Khung**: prefix, versioning, JSON options, CORS, cookie, rate limit, OpenAPI + Swagger UI | **B4** | `Program.cs` |
| Chính sách **mặc định đóng** (`FallbackPolicy`) + `[AllowAnonymous]` | **B4** | `Program.cs` · `Shared/Authorization/` |
| Endpoint xác thực | **B4** | `Modules/Identity/` |
| DTO phân trang cursor dùng chung | **B4** | `Shared/Contracts/` |
| **Endpoint nghiệp vụ** | **B6**, lặp ở **B7** | `Modules/<X>/Api/<X>Controller.cs` |
| Adapter gọi dịch vụ ngoài (R2, SMTP) | **B8** | `Integrations/` |

---

# GIAI ĐOẠN A — Nền

*Kết thúc giai đoạn A: chưa có tính năng nghiệp vụ nào, nhưng mọi quy ước đã sống và kiểm
chứng được. Đây là phần đắt nhất để làm lại — làm chậm và chắc.*

---

## B0 · Luật và ranh giới

**▶ Bắt đầu từ**: tạo file `docs/ARCHITECTURE.md`, viết **mục 1 — bảng liệt kê subproject**
trước mọi thứ khác.

**Điều kiện vào**: không có. Bước đầu tiên tuyệt đối.

**Không làm gì ở bước này**: chưa `dotnet new`, chưa tạo thư mục code, chưa cài NuGet nào.
Ngứa tay muốn code = dấu hiệu ranh giới chưa đủ rõ.

### Thứ tự thực hiện

**1. `ARCHITECTURE.md` mục 1 — bảng subproject**

| Thư mục | Vai trò | Stack | Cổng dev |
|---|---|---|---|
| `src/SocialNet.Api` | Toàn bộ nghiệp vụ + SignalR Hub | ASP.NET Core 10, EF Core | 5080 |
| `src/SocialNet.Web` | Giao diện (tùy chọn) | Next.js hoặc bỏ | 3000 |
| `tests/SocialNet.Tests` | Unit + integration + ArchUnit | xUnit, Testcontainers | — |
| `deploy/` | Dockerfile, compose, cấu hình Dokploy | Docker | — |
| `docs/` | ERD, ADR, tài liệu PTTK | Markdown | — |

*Vì sao trước tiên*: mọi quyết định sau đều phải trả lời được "cái này thuộc subproject nào".

**2. `ARCHITECTURE.md` mục 2 — sơ đồ chiều gọi + luật bất biến**

Vẽ `flowchart LR` (mermaid) cho **mọi** mũi tên: Browser → Traefik → Api → PostgreSQL/Redis/R2/SMTP.
Rồi viết **luật bất biến**, mỗi luật một dòng khẳng định. Tối thiểu:

- **Chỉ `SocialNet.Api` được chạm PostgreSQL.** Web không có chuỗi kết nối, không bao giờ.
- **Client không gọi thẳng Redis / Object Storage** (trừ PUT ảnh bằng pre-signed URL — đây là
  ngoại lệ duy nhất, ghi rõ lý do).
- **Module chỉ đọc/ghi bảng của chính nó.** Cần dữ liệu module khác → gọi interface được
  `export` hẹp, không truy vấn thẳng bảng người ta.
- **Mọi request có `X-Correlation-Id`**; thiếu thì middleware tự sinh; truyền xuyên
  request → Hub → background worker.

**3. Mô hình dữ liệu / ERD** → `docs/erd.md` (+ ảnh draw.io), **không** vào `src/`.

Báo cáo đã có sẵn 15 entity (mục 5.2). Việc còn lại ở B0 là **chốt quy ước đặt tên** — đổi sau
là rename toàn bộ DB:

| Quy ước | Chốt |
|---|---|
| Tên bảng | `snake_case`, số nhiều: `users`, `posts`, `media_attachments` |
| Phân nhóm | **PostgreSQL schema theo module**: `identity`, `social`, `content`, `messaging`, `moderation`, `notification` (thay cho prefix `au_`/`cls_` kiểu Mongo) |
| Khóa chính | `uuid` v7 (`id`) — tăng dần theo thời gian, tốt cho sắp feed; `audit_logs` dùng `bigserial` |
| Cột thời gian | `created_at`, `updated_at`, `deleted_at` (`timestamptz`, luôn UTC) |
| Xóa mềm | Mặc định có `deleted_at`; ngoại lệ phải ghi lý do (`audit_logs` append-only) |
| Naming convention EF | `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()` — viết C# PascalCase, DB ra snake_case tự động |

Ghi luôn vào ERD: `friendships` PK cặp `(user_min, user_max)` + `CHECK user_min < user_max`;
`reactions` PK `(user_id, target_type, target_id)`; `messages` UQ `(conversation_id, seq)` và
UQ `(conversation_id, client_msg_id)`.

**4. Bốn quyết định hợp đồng API** — viết thẳng vào `ARCHITECTURE.md`:

| Quyết định | Chốt | Hệ quả kéo theo |
|---|---|---|
| Prefix + versioning | `/api/v1/...`; `/health/*` không version | Dùng `Asp.Versioning.Mvc` với `UrlSegmentApiVersionReader` |
| Auth model | **Access token JWT 15 phút trả trong body (client giữ trong memory) + refresh token trong cookie `HttpOnly`** | CORS phải `AllowCredentials`; cookie `SameSite=Lax` nếu web và api **cùng site**, `SameSite=None; Secure` nếu khác subdomain; route guard chạy **client-side** vì access token không nằm trong cookie |
| Shape phân trang | **Cursor**: `{ items: [], nextCursor: string?, limit: int }` — không dùng `page/offset` cho feed và tin nhắn | `limit` mặc định 20, tối đa 50 (FR-009) |
| Shape lỗi | **RFC 7807 Problem Details**: `{ type, title, status, detail, errors, traceId }` | Bật `AddProblemDetails()`; cấm trả 500 trắng |

> **Bẫy quan trọng**: nếu chọn đặt refresh token trong cookie thì **web và api phải cùng
> domain gốc** (`app.example.com` + `api.example.com` với cookie `Domain=.example.com`), nếu
> không trình duyệt sẽ không gửi cookie kèm. Chốt tên miền **ngay bây giờ**, ghi vào
> `ARCHITECTURE.md`, vì nó ràng buộc cả cấu hình Dokploy ở B9.

**5. `docs/CODE-RULES.md`** — luật áp dụng cho mọi subproject:

- Ngôn ngữ code tiếng Anh, comment tiếng Việt; comment giải thích **tại sao**, không mô tả lại code.
- Soft delete mặc định.
- Chiều phụ thuộc một chiều: `Api → Modules → Shared`; Shared **không bao giờ** import Modules.
- Không `DateTime.Now` — chỉ `DateTimeOffset.UtcNow` qua `IClock` (test được).
- Lệnh phải chạy xanh trước khi báo xong: `make check`.

**6. `docs/dotnet-api.md`** — viết **khung** thôi (mục lục + luật đã chắc chắn). Phần "cây thư
mục mẫu" **để trống**, điền ở cuối B6 bằng cây **thật**.
*Vì sao hoãn*: viết cây thư mục khi chưa làm module nào là đoán mò, và sẽ phải sửa.

**7. `CLAUDE.md` / `README.md`** — **chỉ là bảng định tuyến**: "đang làm gì → đọc file nào".
Không chứa luật (luật nằm ở `ARCHITECTURE.md` và `CODE-RULES.md`).

### Chốt kiểm tra

- [ ] Đưa `ARCHITECTURE.md` cho người ngoài nhóm, họ trả lời được: *"Tính năng nhắn tin nằm ở
      module nào?"* · *"Web có được query thẳng DB không?"* · *"Thêm endpoint thì URL trông thế nào?"*
- [ ] ERD có đủ 15 entity của báo cáo, mọi bảng đã gán schema và quy ước tên.
- [ ] Đã chốt tên miền production cho web và api (kể cả khi chưa mua).
- [ ] Trong repo **không có file `.cs` nào**.

---

## B1 · Bộ xương solution

**▶ Bắt đầu từ**: `dotnet new sln -n SocialNet` ở thư mục gốc, rồi tạo `Makefile` với **đúng
một target: `help`**.

**Điều kiện vào**: B0 đã chốt.

**Không làm gì ở bước này**:
- **Không tạo thư mục rỗng "để dành"** kèm `.gitkeep`. Thư mục chỉ sinh ra khi có file thật đầu tiên.
- Không tạo project `Domain`/`Application`/`Infrastructure` tách riêng theo Clean Architecture 4
  tầng. Đội 3 người / ~25 ngày: **một project API + thư mục module** là đúng mức (ADR-001:
  modular monolith). Ranh giới ép bằng test, không ép bằng số lượng `.csproj`.
- Không viết code nghiệp vụ.

### Thứ tự thực hiện

**1. Khởi tạo solution và project**

```bash
dotnet new sln -n SocialNet
dotnet new webapi -o src/SocialNet.Api --use-controllers -f net10.0
dotnet new xunit -o tests/SocialNet.Tests -f net10.0
dotnet sln add src/SocialNet.Api tests/SocialNet.Tests
dotnet new gitignore
dotnet new editorconfig
```

**2. `Directory.Build.props` ở gốc** — bật nghiêm ngặt **ngay từ đầu** (bật sau là sửa hàng trăm chỗ):

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Cảnh báo = lỗi: chặn nullable warning tích tụ thành 400 cái không ai đọc -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

**3. `Directory.Packages.props`** — central package management, khóa version một chỗ. Ba người
cài NuGet lệch version là nguồn lỗi khó tìm.

**4. `deploy/docker-compose.dev.yml`** — hạ tầng dev, **chưa có app**:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment: { POSTGRES_DB: socialnet, POSTGRES_USER: dev, POSTGRES_PASSWORD: dev }
    ports: ["5432:5432"]          # mở ra host để DBeaver kết nối
    volumes: ["pgdata:/var/lib/postgresql/data"]
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
  mailpit:                         # bắt email xác minh ở dev, không gửi thật
    image: axllent/mailpit
    ports: ["8025:8025", "1025:1025"]
volumes: { pgdata: }
```

**5. `Makefile` vòng một**: `help`, `doctor`, `setup-env`, `db-up`, `db-down`.

Quy tắc bắt buộc:
- `help` tự mô tả mọi target, có phân nhóm.
- `doctor` phân biệt công cụ **bắt buộc** (❌ nếu thiếu: `dotnet`, `docker`) và **tùy chọn**
  (⚠️ nếu thiếu: `node`, `dotnet-ef`, `k6`) — người chỉ làm backend không bị chặn vì chưa cài Node.
- **Makefile không giữ số cổng của service nào.** Cổng thuộc về `.env`. Ghi ở Makefile là tạo
  nguồn sự thật thứ hai, và nguồn thứ hai sớm muộn sẽ lệch.

**6. `Makefile` vòng hai**: `dev`, `build`, `format`, `test`, `migrate`, và `check` gộp lại.

```makefile
check: format-check build test    ## Chạy trước mọi lần báo "xong"
format-check:
	dotnet format --verify-no-changes
```

**7. CI — chỉ stage `test`.** Chưa build image, chưa deploy.
GitHub Actions: `dotnet restore` → `dotnet build -warnaserror` → `dotnet test`.
Chạy trên mọi PR và nhánh `develop`.

**8. Công cụ ép luật — cài ngay, đừng để sau**

- **ArchUnitNET** trong `tests/SocialNet.Tests/ArchitectureTests.cs` — biến 3 luật ranh giới ở
  B0 thành test đỏ:
  ```
  - Namespace Shared.* KHÔNG được phụ thuộc Modules.*
  - Modules.X KHÔNG được phụ thuộc Modules.Y (trừ qua namespace Contracts)
  - Class kết thúc bằng "Controller" KHÔNG được phụ thuộc DbContext trực tiếp
  ```
- **`BannedApiAnalyzers`** — cấm `DateTime.Now`, `Configuration["..."]` ngoài `Configuration/`.
- `.editorconfig` + `dotnet format` trong `make check`.

*Vì sao bây giờ*: luật chỉ nằm trong văn bản thì sẽ bị vi phạm. Đến tuần thứ ba, dưới áp lực
deadline, không ai đọc `ARCHITECTURE.md` nữa — chỉ có test đỏ mới chặn được.

### Chốt kiểm tra

- [ ] `make help` in ra danh sách lệnh có phân nhóm.
- [ ] `make doctor` báo đúng công cụ thiếu/đủ.
- [ ] `make db-up` → `docker ps` thấy postgres + redis + mailpit chạy.
- [ ] `make check` xanh (chưa có gì để kiểm, nhưng lệnh phải chạy).
- [ ] CI xanh trên commit đầu tiên.
- [ ] `git status` không có thư mục nào chỉ chứa `.gitkeep`.

---

## B2 · Cấu hình fail-fast

**▶ Bắt đầu từ**: mở `src/SocialNet.Api/.env.example` và **liệt kê hết biến môi trường trước**,
kèm comment tiếng Việt giải thích từng biến. Chưa viết code.

**Điều kiện vào**: B1 đã chốt.

**Không làm gì ở bước này**: chưa kết nối DB, chưa viết module nghiệp vụ.

*Vì sao `.env.example` đi trước*: nó là hợp đồng dành cho **người**. Class Options chỉ là bản
dịch của nó sang máy. Viết ngược lại thì `.env.example` luôn lạc hậu — và người thứ ba trong
nhóm sẽ mất nửa ngày để chạy được dự án.

### Thứ tự thực hiện

**1. `.env.example`** — nhóm biến bằng comment. Mỗi biến "có bẫy" phải có comment nêu bẫy:

```bash
# --- APP ---
ASPNETCORE_ENVIRONMENT=Development
# Cổng HTTP trong container. KHÔNG hardcode 8080 ở Dockerfile — Dokploy đọc biến này.
ASPNETCORE_HTTP_PORTS=8080

# --- DATABASE ---
# Dùng "localhost" (KHÔNG dùng "127.0.0.1") cho khớp host với web — lệch host thì cookie
# refresh_token (SameSite=Lax) sẽ KHÔNG được trình duyệt gửi kèm.
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=socialnet;Username=dev;Password=dev

# --- REDIS ---
# Bắt buộc có AbortConnect=false: Redis chết thì app vẫn phải boot (NFR-REL-01, degrade).
ConnectionStrings__Redis=localhost:6379,abortConnect=false

# --- JWT ---
# Tối thiểu 32 byte cho HS256. Đổi khóa = thu hồi mọi phiên đang mở (chủ ý).
Jwt__Secret=
Jwt__Issuer=socialnet
Jwt__Audience=socialnet-web
Jwt__AccessTokenMinutes=15      # NFR-SEC-03: cửa sổ tấn công 15 phút
Jwt__RefreshTokenDays=7

# --- CORS ---
# Danh sách phân tách bằng dấu phẩy. Production THIẾU biến này phải cảnh báo, không im lặng.
Cors__Origins=http://localhost:3000

# --- STORAGE (Cloudflare R2 / S3) ---
Storage__Endpoint=
Storage__Bucket=socialnet-media
Storage__PresignMinutes=10      # UC-04: URL ký số hạn 10 phút
```

**2. `Configuration/JwtOptions.cs` (và các Options khác)** — khớp **1:1** với `.env.example`:

```csharp
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]          // HS256 dưới 32 byte là yếu — chặn ngay lúc boot
    public string Secret { get; init; } = default!;

    [Required] public string Issuer { get; init; } = default!;
    [Required] public string Audience { get; init; } = default!;

    [Range(5, 60)]                     // >60 phút thì refresh rotation mất ý nghĩa
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 30)]
    public int RefreshTokenDays { get; init; } = 7;
}
```

Với mỗi biến, quyết định `[Required]` hay có `default` và **ghi lý do ngay tại chỗ**. Chọn
`default` cẩn thận: một giá trị mặc định sai âm thầm còn tệ hơn không có mặc định.

**3. `Configuration/ServiceCollectionExtensions.cs`** — đăng ký kèm **validate lúc boot**:

```csharp
services.AddOptions<JwtOptions>()
        .Bind(config.GetSection(JwtOptions.Section))
        .ValidateDataAnnotations()
        .ValidateOnStart();          // ← đây là toàn bộ điểm của bước B2
```

`ValidateOnStart()` là thứ biến "cấu hình sai" từ lỗi runtime lúc 2h sáng thành lỗi lúc khởi
động, có tên biến rõ ràng.

**4. `Program.cs` tối thiểu** — chỉ `builder.Build()` + `app.Run()`, đủ để boot và kiểm chứng.
Hoàn thiện ở B4.

**5. Secrets ở dev**: `dotnet user-secrets set "Jwt:Secret" "..."` — **không** commit `.env`.
Ở production: biến môi trường do Dokploy quản lý (B9).

### Chốt kiểm tra

- [ ] App boot lên được.
- [ ] **Xóa `Jwt__Secret` khỏi cấu hình → app chết ngay lúc boot**, in ra tên biến thiếu.
      Đây là chốt quan trọng nhất của bước này.
- [ ] `grep -rn "Configuration\[" src/` chỉ trả về `Configuration/` (tối đa một chỗ ở
      `Program.cs` đọc `ASPNETCORE_ENVIRONMENT`, có comment giải thích).
- [ ] Mọi biến trong `.env.example` đều có trong một class Options, và ngược lại.

---

## B3 · Hạ tầng — CSDL lần 1 (chỉ kết nối)

**▶ Bắt đầu từ**: tạo `Infrastructure/Persistence/AppDbContext.cs`, bên trong **chỉ có** phần
cấu hình kết nối. Không có `DbSet` nào.

**Điều kiện vào**: B2 đã chốt (chuỗi kết nối phải đi qua Options, không đọc `Configuration[]`).

**Không làm gì ở bước này**:
- **Không tạo entity nào.** Kể cả `User`.
- **Không tạo thư mục `Entities/` hay `Models/` tập trung.** Entity thuộc về module sở hữu, và
  module đầu tiên phải tới B4 mới có.
- **Chưa chạy `dotnet ef migrations add`.** Chưa có entity thì migration rỗng, vô nghĩa.
- Không tạo `Infrastructure/Mailer/`, `Infrastructure/Storage/` khi chưa dùng tới.

### Thứ tự thực hiện

**1. `Infrastructure/Persistence/AppDbContext.cs`**

```csharp
/// <summary>
/// DbContext dùng chung cho toàn bộ modular monolith.
///
/// LUẬT: file này KHÔNG chứa DbSet nào. Mỗi module tự khai DbSet của mình
/// bằng partial class trong Modules/&lt;X&gt;/Persistence/, và cấu hình bảng bằng
/// IEntityTypeConfiguration được nạp qua ApplyConfigurationsFromAssembly.
/// Gom entity về đây sẽ xóa mất thông tin "bảng nào thuộc module nào" và biến
/// file này thành chỗ 3 người cùng sửa, cùng conflict.
/// </summary>
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
        => b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

Đăng ký:

```csharp
services.AddDbContext<AppDbContext>((sp, opt) =>
{
    var cs = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Postgres;
    opt.UseNpgsql(cs, npg => npg.EnableRetryOnFailure(3))
       .UseSnakeCaseNamingConvention();   // C# PascalCase → PostgreSQL snake_case
});
```

**2. `Infrastructure/Persistence/UnitOfWork.cs`** — helper transaction.
Nhiều invariant ở báo cáo là "cùng transaction": tạo `User` + `Profile` (5.3), ghi `message` +
tăng `seq` + cập nhật `last_message` (5.3), cập nhật `report` + ghi `audit_log` (UC-19). Có
helper từ đầu thì service không tự chế mỗi nơi một kiểu:

```csharp
Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
```

Dùng `IExecutionStrategy` của Npgsql để transaction hoạt động cùng `EnableRetryOnFailure` —
thiếu chỗ này sẽ ăn `InvalidOperationException` khi retry.

**3. `Infrastructure/Cache/RedisCacheService.cs`** — **thiết kế suy biến an toàn**:

```csharp
/// Redis chết thì app VẪN boot, VẪN phục vụ được — chỉ mất cache và ghi log warn.
/// (NFR-REL-01; UC-08 luồng E2: "Redis chết → đọc thẳng DB")
/// Vì thế: abortConnect=false, mọi lỗi Redis bị nuốt ở lớp này, KHÔNG ném lên service.
```

- `ConnectionMultiplexer` đăng ký `Singleton` qua `Lazy<>`.
- `GetOrSetAsync<T>(key, ttl, factory)` — lỗi Redis → gọi thẳng `factory`.
- Quy ước key ghi ngay trong file: `feed:{userId}:p1` (TTL 30s) · `friends:{userId}` (TTL 60s)
  · `presence:{userId}` — khớp mục 5.6 báo cáo.

**4. `Infrastructure/Observability/`** — Serilog JSON + middleware `X-Correlation-Id`.
Làm ở đây, không hoãn: sau này lỗi ở B6/B7 sẽ khó lần nếu log không có correlation id.

**5. `Modules/Health/HealthController.cs`** — `GET /health/live`, `[AllowAnonymous]`, **không
version**, **không chạm dependency ngoài**. Để dùng được ngay cả khi DB/Redis chưa cấu hình.
`/health/ready` (có chạm DB + Redis) là việc khác — làm ở B9 khi Traefik cần nó.

**6. Kết nối DBeaver và kiểm chứng**

1. DBeaver → New Connection → PostgreSQL.
2. Host `localhost`, Port `5432`, Database `socialnet`, User `dev`.
3. Test Connection → Finish.
4. Mở `Database Navigator` → `socialnet` → `Schemas`. Lúc này chỉ có `public` — **đúng như kỳ
   vọng**, vì chưa có entity nào. Nếu thấy bảng lạ nghĩa là ai đó đã tạo entity sớm hơn lộ trình.

### Chốt kiểm tra

- [ ] `make dev` → app boot → log Serilog báo mở kết nối PostgreSQL thành công.
- [ ] `GET /health/live` trả 200.
- [ ] Tắt PostgreSQL → app báo lỗi **rõ ràng** (không phải stack trace mơ hồ 40 dòng).
- [ ] Tắt Redis → app **vẫn boot** và `/health/live` vẫn 200.
- [ ] DBeaver kết nối được, thấy database `socialnet`.
- [ ] `find src -name "*Entity.cs" -o -name "*Configuration.cs"` không trả về gì.

---

## B4 · Khung API + xác thực — API lần 1

**▶ Bắt đầu từ**: mở `Program.cs` và viết đủ **10 khối theo đúng thứ tự** dưới đây. Mỗi khối
kèm comment nêu *tại sao*.

**Điều kiện vào**: B3 đã chốt.

**Không làm gì ở bước này**: chưa làm module nghiệp vụ nào. `Identity` là **ngoại lệ duy nhất**,
và phải xong ở đây vì mọi module sau đều dựa vào `CurrentUser` và ma trận RBAC.

### Thứ tự thực hiện

#### Phần 1 — Khung `Program.cs`

| # | Khối | Ghi chú |
|---|---|---|
| 1 | Serilog theo `ASPNETCORE_ENVIRONMENT` | prod: JSON gọn; dev: console đầy đủ |
| 2 | Options + `ValidateOnStart` (từ B2) | |
| 3 | `AddDbContext` + Redis + HealthChecks (từ B3) | |
| 4 | `AddProblemDetails()` + `UseExceptionHandler` | RFC 7807, cấm 500 trắng |
| 5 | API versioning `/api/v1` | `UrlSegmentApiVersionReader`; `/health/*` khai `[ApiVersionNeutral]` |
| 6 | `AddControllers()` + JSON options | **xem cảnh báo bên dưới** |
| 7 | CORS: nhiều origin + `AllowCredentials()` | prod thiếu `Cors__Origins` phải log cảnh báo |
| 8 | Rate limiting | 100 req/phút/user; **10 req/phút cho `/auth/*`** (chống dò mật khẩu, FR-003) |
| 9 | AuthN JWT + AuthZ **mặc định đóng** | xem Phần 2 |
| 10 | `AddOpenApi()` + Swagger UI **chỉ ở dev** | prod tắt để không lộ contract |

> **Quyết định lan tỏa nhất toàn dự án** nằm ở khối 6:
>
> ```csharp
> builder.Services.ConfigureHttpJsonOptions(o =>
>     o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
> ```
>
> Field không khai trong DTO sẽ **bị từ chối 400**, không phải bị bỏ qua âm thầm. Nó biến DTO
> thành hợp đồng cứng. **Bật ngay bây giờ** — bật sau khi đã có 10 module là vỡ hàng loạt
> client đang gửi field thừa.
>
> Kèm theo: `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` (mặc định) và ghi vào
> `dotnet-api.md` rằng **DTO là `record` với `init`**, không phải class có setter công khai.

**Thứ tự middleware** (sai thứ tự = lỗi khó tìm nhất bước này):

```
UseSerilogRequestLogging → UseExceptionHandler → CorrelationIdMiddleware
→ UseCors → UseRateLimiter → UseAuthentication → UseAuthorization → MapControllers
```

`UseCors` **phải trước** `UseAuthentication`, nếu không response 401 sẽ thiếu header CORS và
trình duyệt báo lỗi CORS thay vì báo 401 — cực kỳ mất thời gian để lần.

#### Phần 1b — Swagger UI trên .NET 10

Từ .NET 9, template `webapi` **không còn kèm Swashbuckle**; .NET 10 sinh tài liệu OpenAPI 3.1
bằng package first-party `Microsoft.AspNetCore.OpenApi`. Người ta vẫn thường gọi chung là
"Swagger" nhưng phải cấu hình thêm một bước để có **giao diện** thử API:

```csharp
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // sinh /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SocialNet API v1"));
}
```

- Package cho document: `Microsoft.AspNetCore.OpenApi`
- Package cho giao diện: `Swashbuckle.AspNetCore.SwaggerUI` (**chỉ phần UI**, không phải
  Swashbuckle đầy đủ). Nếu muốn UI hiện đại hơn, `Scalar.AspNetCore` +
  `app.MapScalarApiReference()` là lựa chọn thay thế — cùng đọc `/openapi/v1.json`.
- `BearerSecuritySchemeTransformer` là thứ làm hiện nút **Authorize** trong Swagger UI. Thiếu
  nó thì mọi endpoint cần token đều trả 401 khi bấm "Try it out" — và cả nhóm sẽ tưởng backend hỏng.
- Trong `launchSettings.json`, đặt `"launchUrl": "swagger"` để `make dev` mở thẳng UI.

#### Phần 2 — `Shared/` tối thiểu

**1. `Shared/Security/CurrentUser.cs`** — kiểu người dùng đã xác thực.
*Vì sao trước mọi thứ khác*: JWT handler gắn nó vào `HttpContext.User`; mọi controller sau đọc
nó; mọi authorization handler đọc nó. Định nghĩa trước thì ba nơi kia không phải đoán.
**Không chứa trường nhạy cảm** (không `PasswordHash`, không email nếu không cần).

```csharp
public sealed record CurrentUser(Guid Id, string Username, string Role);
// + extension: HttpContext.GetCurrentUser() đọc từ claim sub / role
```

**2. `Shared/Security/Permissions.cs`** — hằng chuỗi cho **ma trận RBAC mục 6.7.2** của báo cáo:

```csharp
public static class Permissions
{
    public const string PostCreate   = "post.create";
    public const string PostHide     = "post.hide";       // Moderator+
    public const string ReportResolve= "report.resolve";  // Moderator+
    public const string UserLock     = "user.lock";       // Admin
    public const string RoleAssign   = "role.assign";     // Admin
    public const string AuditRead    = "audit.read";      // Admin
    // ... đủ 14 dòng của bảng 6.7.2
}

public static class RolePermissions   // nguồn sự thật duy nhất cho ánh xạ Role → Permission
{
    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<...>
    {
        ["User"]      = [PostCreate, CommentCreate, ReactionSet, FriendRequest, MessageSend, ReportCreate],
        ["Moderator"] = [.. Map["User"], PostHide, ReportResolve],
        ["Admin"]     = [.. Map["Moderator"], UserLock, RoleAssign, AuditRead],
    };
}
```

**3. `Shared/Security/AuthorizationExtensions.cs`** — đăng ký policy **bằng vòng lặp**, không
viết tay từng cái (thêm permission mà quên đăng ký policy là lỗi 500 lúc chạy):

```csharp
foreach (var perm in Permissions.All)
    options.AddPolicy(perm, p => p.RequireClaim("permission", perm));
```

**4. Mặc định đóng** — dòng quan trọng nhất của toàn bộ phần bảo mật:

```csharp
options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
// → MỌI endpoint cần token. Mở ra bằng [AllowAnonymous], từng cái một, có chủ ý.
// Chiều ngược lại (mặc định mở, nhớ gắn [Authorize]) nghĩa là quên một attribute
// là hở một endpoint — và không có gì báo cho bạn biết.
```

**5. Tầng 3 — quyền trên tài nguyên (chống IDOR)**

Báo cáo xếp IDOR là rủi ro **Critical** (6.7.4) — RBAC một mình không đủ. Dựng khuôn ngay bây
giờ để mọi module sau chỉ việc copy:

```csharp
// Shared/Authorization/ResourceOperations.cs
public static class Operations
{
    public static readonly OperationAuthorizationRequirement Update = new() { Name = "Update" };
    public static readonly OperationAuthorizationRequirement Delete = new() { Name = "Delete" };
    public static readonly OperationAuthorizationRequirement Read   = new() { Name = "Read"   };
}
// Mỗi module viết AuthorizationHandler<OperationAuthorizationRequirement, TResource> của mình.
// Service gọi: await _authz.AuthorizeAsync(User, post, Operations.Update);
```

Ghi vào `dotnet-api.md`: **mọi endpoint có `{id}` trỏ tới tài nguyên của người dùng đều phải
đi qua tầng 3.** Không có ngoại lệ.

**6. `Shared/Contracts/CursorPage.cs`** — shape phân trang đã chốt ở B0:

```csharp
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, int Limit);
public sealed record CursorQuery { public string? Cursor { get; init; } [Range(1,50)] public int Limit { get; init; } = 20; }
```

Kèm helper mã hóa/giải mã cursor (base64 của `created_at|id`) — **một chỗ duy nhất**, vì
`feed`, `comments`, `messages`, `notifications` đều dùng.

#### Phần 3 — Module `Identity`

**7. `Modules/Identity/`** — làm theo **đúng thứ tự này**, và ghi nhớ nó, vì nó lặp lại ở mọi
module sau:

```
Domain/User.cs, Role.cs, RefreshToken.cs
→ Persistence/Configurations/UserConfiguration.cs (+ Role, RefreshToken)
→ Persistence/IdentityDbSets.cs (partial AppDbContext)
→ migration đầu tiên
→ Contracts/ (DTO)  → Validators/  → Services/  → Api/AuthController.cs
→ IdentityModuleExtensions.cs (DI)
```

`User` là entity **đầu tiên** — nó thiết lập khuôn cho mọi entity sau: schema `identity`, cột
`created_at/updated_at/deleted_at`, index khai ngay trong `IEntityTypeConfiguration`, **kèm
comment nêu truy vấn nào dùng index đó**.

Bám đúng bảng `users` ở mục 5.5 báo cáo: `email citext UNIQUE` (cần
`b.HasPostgresExtension("citext")`), `username varchar(30)`, `password_hash varchar(72)`,
`role_id smallint FK RESTRICT`, `status` + `CHECK`, `failed_login_count`, `locked_until`.

Chạy migration đầu tiên:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialIdentity -p src/SocialNet.Api
dotnet ef database update -p src/SocialNet.Api
```

Rồi **mở DBeaver kiểm tra ngay**: schema `identity` có `users`, `roles`, `refresh_tokens`;
`users.email` là `citext` và có unique index. Đây là lần đầu tiên bạn nhìn thấy quy ước B0
thành hình trong DB thật — sai gì thì sửa **bây giờ**, lúc mới có 3 bảng.

**8. Nội dung nghiệp vụ bắt buộc của Identity** (UC-01, UC-02; FR-001..003):

| Việc | Chi tiết |
|---|---|
| Băm mật khẩu | `BCrypt.Net-Next`, work factor **12** (NFR-SEC-01) |
| Đăng nhập sai | `failed_login_count++`; đủ 5 lần → `locked_until = now + 15'` → trả **423** |
| Thông báo lỗi | Sai email và sai mật khẩu trả **cùng một** message (không lộ email nào tồn tại) |
| Access token | JWT HS256, TTL 15', claim `sub`/`role`/`permission[]`/`jti`/`iat`/`exp` |
| Refresh token | Sinh ngẫu nhiên 32 byte, **lưu dạng băm** trong DB, cookie `HttpOnly` |
| Refresh rotation | Mỗi lần refresh phát token mới + thu hồi token cũ; **phát hiện reuse → thu hồi cả chuỗi** (NFR-SEC-03) |
| Logout / đổi mật khẩu | Thu hồi toàn bộ refresh token của user |
| Seed | 3 role (User/Moderator/Admin) bằng `HasData` idempotent; admin đầu tiên tạo từ biến môi trường |

**9. Đăng ký module vào `Program.cs` — đúng một dòng**: `builder.Services.AddIdentityModule();`

### Chốt kiểm tra

- [ ] `POST /api/v1/auth/register` → 201; email trùng → 409.
- [ ] `POST /api/v1/auth/login` → 200 kèm access token; refresh token nằm trong cookie `HttpOnly`.
- [ ] Sai mật khẩu 5 lần → lần thứ 6 trả **423**, kể cả khi nhập đúng.
- [ ] Gọi endpoint **không** `[AllowAnonymous]` mà không kèm token → **401**.
- [ ] Gửi body có field lạ → **400** (không phải bị bỏ qua âm thầm).
- [ ] `POST /auth/refresh` với token đã dùng một lần → **401** và cả chuỗi bị thu hồi.
- [ ] Swagger UI mở được ở dev tại `/swagger`, bấm **Authorize**, dán token, gọi được endpoint
      cần quyền; ở `Production` **không** mở được.
- [ ] `GET /health/live` vẫn 200 mà không cần token.
- [ ] DBeaver: `SELECT * FROM identity.users` thấy `password_hash` bắt đầu bằng `$2a$12$`.

---

## B5 · Bộ xương web *(chỉ khi làm frontend)*

**▶ Bắt đầu từ**: tạo `lib/api.ts` — instance HTTP + **token bridge** + **single-flight
refresh**. Chưa tạo màn hình nào.

**Điều kiện vào**: B4 đã chốt (phải có endpoint đăng nhập thật để kiểm chứng).

> **Nếu nhóm quyết định KHÔNG làm frontend** (đề bài cho phép "frontend tùy chọn"): bỏ qua B5,
> nhưng **phải thay bằng một client kiểm chứng được**, nếu không B6 sẽ mất chốt "chạy thông từ
> DB lên tới client":
>
> 1. Xuất `openapi.json` từ `/openapi/v1.json` và commit vào `docs/`.
> 2. Import vào Postman → tạo collection theo môi trường (dev/staging/prod) như mục 6.6 báo cáo.
> 3. Viết script `tests/http/*.http` hoặc Postman test cho **mỗi** endpoint: happy path + 401 +
>    403 + 400.
> 4. Đưa 7 ca âm bản TC-A01..TC-A07 (mục 6.7.5) thành integration test chạy trong CI.
>
> Sau đó nhảy thẳng tới **B6**, bỏ các mục 12–21 phần frontend.

### Thứ tự thực hiện (Next.js)

*Vì sao `api.ts` đi đầu*: hình dạng token bridge quyết định cách `AuthContext` được viết, và
`AuthContext` quyết định thứ tự provider trong `layout.tsx`. Làm ngược thì phải sửa cả ba.

1. **`lib/api.ts`** — ba thứ trong một file:
   - **Token bridge**: object `bridge` với 3 hàm mặc định vô hại
     (`getAccessToken`/`setAccessToken`/`onAuthFailure`) + `registerAuthBridge()`.
     *Vì sao*: phá vòng import `store → api → axios → store`. File này **không bao giờ** import React.
   - **Request interceptor**: gắn `Authorization: Bearer`.
   - **Response interceptor — single-flight refresh 401**: một biến `refreshPromise` dùng chung,
     nên N request 401 đồng thời chỉ gọi **đúng một** lần `/auth/refresh`. Cờ `_retry` chống
     retry lặp. Endpoint `/auth/*` bị loại khỏi vòng retry. Lời gọi refresh dùng `fetch` trần,
     không dùng instance `api`, để không tái nhập interceptor.
   - `withCredentials: true` — bắt buộc, nếu không cookie refresh không được gửi.
2. **`lib/queryClient.ts`** — `staleTime` vài chục giây · `refetchOnWindowFocus: false` ·
   `retry: 1` nhưng **bỏ retry ngay với 401/403/404** · mutation `retry: 0`.
3. **`lib/queryCacheBridge.ts`** — xóa cache **từ ngoài** `QueryClientProvider`, để logout
   không rò dữ liệu người dùng trước sang phiên sau.
4. **`tailwind.config.ts`** — design token **ngữ nghĩa** (`ink`, `bg`, `surface`, `line`,
   `muted`, `brand`, `danger`), không phải tên màu. Luật kèm theo: **cấm mã hex thô trong component**.
5. **`components/ui/` — chỉ 5–7 primitive**: `Button`, `Input`, `Modal`, `Spinner`, `Skeleton`,
   `Toast`. Không dựng sẵn 20 cái.
6. **`features/auth/`**: `types/` → `api/` → `context/AuthContext.tsx` → `AuthGuard.tsx` →
   form → `LoginScreen` → `index.ts`.
   `AuthContext` có 3 chi tiết bắt buộc: `tokenRef` song song với state · cờ `bootstrappedRef`
   chống double-mount của Strict Mode làm xoay cookie hai lần · đăng ký bridge và bootstrap
   session trong **cùng một effect**.
7. **`app/layout.tsx`** — provider stack, **thứ tự có ý nghĩa**:
   `ChunkErrorRecovery → AuthProvider → Providers(QueryClient + Toast)`.
   `AuthGuard` chạy **client-side**, không phải middleware — vì access token nằm trong memory,
   edge không đọc được. Ghi lý do này vào docblock.

### Chốt kiểm tra

- [ ] Đăng nhập **trên trình duyệt thật** → vào được `/feed` (dù trống).
- [ ] Chờ access token hết hạn → request tiếp theo **tự refresh** và thành công, **không** đá về `/login`.
- [ ] Mở 3 tab, cùng lúc gây 401 → tab Network chỉ thấy **một** lời gọi `/auth/refresh`.
- [ ] Logout → cache bị xóa sạch (đăng nhập tài khoản khác không thấy dữ liệu cũ).
- [ ] Không component nào chứa mã màu hex.

---

# GIAI ĐOẠN B — Tính năng

*Từ đây công việc lặp theo một khuôn. Lát cắt đầu tiên tốn nhất; các lát sau ngày càng rẻ.*

---

## B6 · Lát cắt dọc số 1 — `Post`

**Đây là bước quan trọng nhất trong toàn bộ lộ trình.** Nó không chỉ tạo ra tính năng đầu tiên —
nó **kiểm chứng toàn bộ quy ước** đã đặt ra ở B0.

**▶ Bắt đầu từ**: mở ERD ở B0, đếm entity nào có nhiều khóa ngoại trỏ **đến** nó nhất. Sau
`User` (đã làm ở B4) thì đó là **`Post`** — `comments`, `reactions`, `media_attachments`,
`reports` đều trỏ tới. Bắt đầu bằng file `Modules/Content/Domain/PostPrivacy.cs`.

**Điều kiện vào**: B4 đã chốt (và B5 nếu làm frontend). Phải gọi được API có token trước khi
làm bước này.

**Không làm gì ở bước này**:
- **Không làm hai module cùng lúc.** Một lát, làm hết, chạy được, rồi mới sang lát hai.
- **Không làm hết backend rồi mới quay sang frontend/Postman.**
- Chưa làm cache Redis cho feed, chưa background worker, chưa SignalR — để tới B8.
- Chưa làm `comments` và `reactions` — chúng là lát cắt riêng ở B7.

> ### Vì sao phải làm DỌC, không làm NGANG
>
> Cám dỗ tự nhiên là "làm hết API đã, client tính sau". Đừng.
>
> Lát cắt dọc đầu tiên là thứ **duy nhất** chứng minh được các quy ước có dùng được không: DTO
> tách 3 lớp có tiện không · cursor pagination có đủ không · authorization handler tầng 3 có
> gọn không · `UnmappedMemberHandling.Disallow` có gây phiền không.
>
> Nếu một quy ước sai, bạn muốn biết ở module **thứ nhất** — không phải sau khi đã viết tám
> module theo quy ước sai đó.

### Thứ tự thực hiện — 21 mục, không đảo

#### Backend

| # | File | Nội dung tối thiểu | Vì sao ở vị trí này |
|---|---|---|---|
| 1 | `Modules/Content/Domain/PostPrivacy.cs`, `PostStatus.cs` | Enum lưu **dưới dạng chuỗi** (`HasConversion<string>()`), khớp `CHECK` ở DB | Cả entity, DTO **và** client đều dùng. Không có nó thì ba nơi tự chế union rồi lệch nhau |
| 2 | `Modules/Content/Domain/Post.cs` | `Id` (uuid v7) · `AuthorId` · `Content` · `Privacy` · `Status` · `CommentCount` · `ReactionCounts` (jsonb) · `CreatedAt/UpdatedAt/DeletedAt`. Hàm khởi tạo **ép BR-01 ngay trong domain** (≤5000 ký tự **hoặc** ≥1 ảnh) | Entity là hợp đồng đắt nhất. Sai ở đây = migration |
| 3 | `Modules/Content/Domain/MediaAttachment.cs` | `CHECK size_bytes ≤ 10485760`; tối đa 10 ảnh/bài ép ở domain | |
| 4 | `Persistence/Configurations/PostConfiguration.cs` | Schema `content` · `ToTable("posts")` · `HasCheckConstraint` cho `privacy`/`status`/độ dài · **index khai ngay tại đây kèm comment nêu truy vấn nào dùng nó** | |
| | | `HasIndex(p => new { p.AuthorId, p.CreatedAt }).IsDescending(false, true).HasFilter("status = 'published' AND deleted_at IS NULL")` — **index chính của News Feed** (mục 5.6) | |
| 5 | `Persistence/ContentDbSets.cs` | `partial class AppDbContext { public DbSet<Post> Posts => Set<Post>(); }` | Giữ luật "module sở hữu DbSet của mình" |
| 6 | migration `AddContent` | `dotnet ef migrations add AddContent` → **đọc file migration sinh ra trước khi apply** | Migration sai phát hiện lúc review rẻ hơn lúc production |
| 7 | `Contracts/CreatePostRequest.cs` | `record` với `init`; mọi field đều có trong hợp đồng — field không khai = **400** | |
| 8 | `Contracts/UpdatePostRequest.cs` | Mọi field optional; phân biệt "không gửi" và "gửi null" bằng `JsonElement?` hoặc `Optional<T>` nếu cần | |
| 9 | `Contracts/PostResponse.cs` | **3 lớp**: `record PostResponse` (hợp đồng ra ngoài) · `PostView` (projection nội bộ) · `ToResponse()` mapper **thuần** — không I/O, không quyết định phân quyền, chỉ **nhận cờ** từ service | |
| 10 | `Validators/CreatePostRequestValidator.cs` | FluentValidation: BR-01 (≤5000 ký tự hoặc ≥1 ảnh, ≤10 ảnh) · `privacy` thuộc enum | Validate cú pháp ở đây; validate nghiệp vụ cần DB thì ở service |
| 11 | `Authorization/PostAuthorizationHandler.cs` | `Update`/`Delete` → chỉ owner (hoặc Admin) · `Read` → **BR-02**: public/friends/private, đánh giá **tại thời điểm đọc** | Đây là tầng 3 chống IDOR. Bỏ mục này = lỗ hổng Critical |
| 12 | `Services/PostService.cs` | Guard clause + hàm private nhỏ (`LoadOr404`, `AssertCanEdit`, `BuildDetail`). Phân quyền gọi qua `IAuthorizationService`. **Không đọc `Configuration[]`**. Tạo post + media trong **một transaction** | |
| 13 | `Api/PostsController.cs` | Chỉ: attribute route · `[Authorize(Policy = Permissions.PostCreate)]` · `[ProducesResponseType]` cho **mọi** mã trả về (đây là thứ Swagger đọc) · gọi service | Tạo → **201** + `Location` · xóa mềm → **204** · sửa → **200** |
| 14 | `ContentModuleExtensions.cs` | `AddContentModule()`: đăng ký service, handler, validator | |
| 15 | `Program.cs` | **Thêm đúng một dòng**. Nếu phải sửa nhiều hơn một dòng, ranh giới module đang sai | |

**Quy ước Swagger cho mỗi endpoint** (ghi vào `dotnet-api.md` ngay lần đầu):

```csharp
/// <summary>Tạo bài viết mới.</summary>
/// <remarks>BR-01: phải có nội dung ≤5000 ký tự hoặc ít nhất 1 ảnh; tối đa 10 ảnh.</remarks>
[HttpPost]
[Authorize(Policy = Permissions.PostCreate)]
[ProducesResponseType<PostResponse>(StatusCodes.Status201Created)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
```

Bật `<GenerateDocumentationFile>true</GenerateDocumentationFile>` trong `.csproj` để XML
comment lên được Swagger UI. Không có nó, phần `<summary>` bị bỏ qua âm thầm.

#### Client (frontend hoặc Postman)

| # | File | Nội dung tối thiểu |
|---|---|---|
| 16 | `features/posts/types/index.ts` | **Mirror DTO backend**, comment ghi rõ mirror cái gì. Trạng thái **suy được** (ví dụ "bài của tôi") tính ở client, **không** lưu DB |
| 17 | `features/posts/api/postsApi.ts` | Object phẳng, mỗi method một endpoint, **docblock ghi đúng route**. Không chứa react-query |
| 18 | `features/posts/api/queryKeys.ts` | Factory **phân cấp**: `all` → `list(params)` → `detail(id)`. Thêm tham số lọc mới phải thêm vào cả hàm serialize, nếu không cache va nhau |
| 19 | `features/posts/hooks/` | `usePosts` · `usePostDetail` · `usePostMutations`. `onSuccess` phải invalidate **đúng** key bị ảnh hưởng, kể cả key của feature khác |
| 20 | `features/posts/components/` | Screen + modal + row. **Đủ 4 trạng thái, đúng thứ tự**: `pending → error → empty → list` |
| 21 | `app/(app)/posts/page.tsx` | **Chỉ mount screen**, không logic |

*(Nếu không làm frontend: thay 16–21 bằng Postman collection + `.http` file + integration test
đủ 4 ca: 201, 400 BR-01, 401, 403 IDOR.)*

#### Việc cuối cùng của B6 — đừng bỏ qua

**22. Quay lại hoàn thiện `docs/dotnet-api.md`**: điền phần "cây thư mục mẫu" bằng **cây thật
vừa dựng**, và viết ra mọi quyết định đã chốt trong lúc làm (khuôn DTO 3 lớp, chỗ đặt
authorization handler, quy ước index, quy ước attribute Swagger).

*Vì sao bây giờ mới viết*: bây giờ nó là **mô tả bằng chứng**, không phải suy đoán.

**23. Mở DBeaver, chạy `EXPLAIN ANALYZE`** trên truy vấn danh sách bài viết theo tác giả.
Xác nhận là **Index Scan**, không phải Seq Scan. Nếu là Seq Scan, sửa index **ngay bây giờ**,
lúc bảng còn rỗng — không phải sau khi có 1,8 triệu bài.

### Chốt kiểm tra

- [ ] Một người dùng thật **tạo → xem danh sách → xem chi tiết → sửa → xóa** được qua client,
      không cần mở dev tools.
- [ ] Bài có `privacy=private` của A **không** hiện với B (kiểm bằng 2 tài khoản).
- [ ] A gọi `PATCH /api/v1/posts/{id-của-B}` → **403**, không phải 404 hay 200 (TC-A03).
- [ ] Gửi bài không nội dung và không ảnh → **400** kèm Problem Details có `errors` theo field.
- [ ] Xóa là **soft delete** — DBeaver thấy bản ghi vẫn còn với `deleted_at` khác null, nhưng
      không xuất hiện ở mọi luồng đọc.
- [ ] Thêm một giá trị vào enum `PostPrivacy` → **lỗi biên dịch** ở chỗ map nhãn (không phải
      chuỗi trống lúc chạy).
- [ ] Swagger UI hiển thị đủ mô tả, ví dụ request và tất cả mã lỗi của endpoint mới.
- [ ] `make check` xanh.
- [ ] `dotnet-api.md` đã có cây thư mục thật.

---

## B7 · Lặp lát cắt 2..n

**▶ Bắt đầu từ**: vẽ **đồ thị phụ thuộc module** từ ERD, rồi chọn module tiếp theo theo quy tắc:
**làm module bị nhiều module khác phụ thuộc trước.**

**Điều kiện vào**: B6 đã chốt, và `dotnet-api.md` đã cập nhật bằng cây thư mục thật.

**Không làm gì ở bước này**: chưa trừu tượng hóa. Thấy trùng lặp lần thứ nhất → **ghi lại vào
bảng nợ kỹ thuật**, đừng gom vội. Tiêu chí gom nằm ở B8.

### Đồ thị phụ thuộc và thứ tự làm

Từ mục 5.1 báo cáo (mũi tên = "phụ thuộc vào"):

```
Identity ──► Profile
         └─► SocialGraph ──┬──► Content ──┬──► Comment ──► Reaction
                           │              └──► Feed
                           └─► Messaging ──► Notification
                                                 ▲
                              Moderation ────────┘
```

**Thứ tự làm** — duyệt từ trái sang:

| # | Lát cắt | UC / FR | Điểm khó nhất — đọc trước khi bắt đầu |
|---|---|---|---|
| 1 | `Post` ✔ (B6) | UC-04/05 | — |
| 2 | `Profile` | UC-03, FR-013 | Quan hệ 1–1 với `users`; tạo cùng transaction với đăng ký (invariant 5.3) |
| 3 | `SocialGraph` — friendships | UC-10/11/12, BR-03 | PK cặp `(user_min, user_max)` + `CHECK user_min < user_max`: **luôn sắp xếp 2 id trước khi ghi**. Pending→Accepted là **một** `UPDATE` |
| 4 | `SocialGraph` — follows | UC-13, FR-012 | Một chiều, không cần phê duyệt; `CHECK follower <> followee` |
| 5 | `Comment` | UC-06, BR-08 | Tối đa **3 cấp**: kiểm ở tầng ứng dụng (đọc `depth` của parent). Xóa → giữ nhánh, hiện "Bình luận đã bị xóa" |
| 6 | `Reaction` | UC-07, BR-05 | PK `(user, target_type, target_id)` → `PUT` **idempotent**; đổi loại = `UPDATE`, không phải thêm dòng. Cập nhật `reaction_counts` **cùng transaction** |
| 7 | **`Feed`** | UC-08, FR-009, NFR-PERF-01 | **Lát khó nhất.** Fan-out-on-read (ADR-004): lấy danh sách bạn+following → truy vấn `posts` với index ở B6 → lọc BR-02/BR-07 → cursor theo `(created_at, id)`. **Chưa cache ở bước này** — đo trước đã |
| 8 | `Messaging` + SignalR | UC-15, BR-06/BR-09, NFR-PERF-03 | `conversations` UQ cặp; `messages` UQ `(conv, seq)` và UQ `(conv, client_msg_id)` để **khử trùng khi client retry**. Ghi tin + tăng `seq` + cập nhật `last_message` trong **một** transaction. Hub xác thực JWT qua query string (`access_token`) vì WebSocket không gửi header được |
| 9 | `Notification` | UC-17, FR-018 | UQ `(recipient, group_key)` để **gộp**; partial index `WHERE is_read = false` cho badge |
| 10 | `Search` | UC-16, FR-017 | `CREATE EXTENSION unaccent, pg_trgm`; GIN index trên `unaccent(display_name)`; yêu cầu `q` ≥ 2 ký tự |
| 11 | `Moderation` + `Admin` | UC-18/19/20, FR-019/020 | Cập nhật `report` + ghi `audit_log` **cùng transaction**. `audit_logs` append-only: **revoke UPDATE/DELETE ở role DB**, không chỉ tin vào code |

### Ba kỹ thuật giữ đồ thị không rối

Ghi vào `dotnet-api.md` và áp dụng **từ lát cắt thứ hai trở đi**:

**1. Đọc chéo module qua interface hẹp, không truy vấn thẳng bảng người ta.**
`Content` cần biết A và B có phải bạn bè không (để lọc BR-02):

```csharp
// Modules/SocialGraph/Contracts/IFriendshipReader.cs
// Chỉ ĐỌC, chỉ để kiểm tra quan hệ — module khác KHÔNG được ghi friendships qua đây.
public interface IFriendshipReader
{
    Task<bool> AreFriendsAsync(Guid a, Guid b, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetFriendIdsAsync(Guid userId, CancellationToken ct);
}
```

`Content` chỉ biết interface này, không biết `Friendship`. Cắt được vòng phụ thuộc mà không
cần trick nào — và ArchUnitNET test ở B1 sẽ đỏ nếu ai đó lười truy vấn thẳng.

**2. `internal` mặc định, `public` phải có lý do ghi tại chỗ.**

```csharp
// Public để Content lọc quyền riêng tư (BR-02) — không mở thêm bề mặt nào khác.
public interface IFriendshipReader { ... }
internal sealed class FriendshipService { ... }   // phần còn lại đóng
```

**3. Gọi service của module chủ, đừng dựng nhánh thứ hai.**
`Messaging` cần tạo notification → gọi `INotificationCreator.CreateAsync()`, để đi qua đúng
luật gộp (FR-018) của `Notification`. Tự viết nhánh tạo thứ hai thì sớm muộn hai nhánh lệch nhau.

### Khi nào dừng lại sửa luật

Nếu ở lát cắt **thứ 2 hoặc thứ 3** thấy một quy ước bất tiện → **dừng, sửa luật, sửa lại các
lát đã làm.** Ở lát 3 việc này rẻ; ở lát 8 thì đắt gấp nhiều lần.

Nếu tới lát thứ 5 vẫn thấy bất tiện mà chưa sửa, khả năng cao đã quá muộn — ghi vào **bảng nợ
kỹ thuật** trong `ARCHITECTURE.md` thay vì giả vờ không có.

### Chốt kiểm tra (cho mỗi lát)

- [ ] Lát mới đi qua **cùng 23 mục** của B6, không bỏ mục nào.
- [ ] `Program.cs` chỉ thêm **một** dòng.
- [ ] Không module nào truy vấn `DbSet` của module khác (ArchUnitNET xanh).
- [ ] Ca âm bản của lát này (403 chéo tài nguyên) đã có test tự động.
- [ ] `make check` xanh.

### Chốt kiểm tra (kết thúc B7)

- [ ] Mọi entity ENT-01..ENT-13 trong ERD đều có module sở hữu.
- [ ] Đủ 7 ca **TC-A01..TC-A07** (mục 6.7.5) chạy tự động trong CI và xanh.
- [ ] Mọi endpoint trong bảng 6.6 báo cáo đều tồn tại và hiện đúng trong Swagger UI.
- [ ] Mọi màn hình trong bản thiết kế mở được và có dữ liệu thật.

---

# GIAI ĐOẠN C — Trưởng thành

---

## B8 · Tối ưu và trừu tượng hóa — chỉ khi có bằng chứng

**▶ Bắt đầu từ**: **không bắt đầu gì cả.** Mở bảng dưới, kiểm tra xem tín hiệu nào đã xuất hiện
chưa. Chưa có tín hiệu → quay lại B7 hoặc sang B9.

**Điều kiện vào**: đã có **ít nhất 2 module** hoàn chỉnh; với phần cache thì phải có **số đo thật**.

> ### Sai lầm phổ biến nhất của cả giai đoạn
>
> Dựng `Shared/`, `Core/`, `Common/` **cho nghiệp vụ** ngay từ đầu.
>
> Một thư mục "dùng chung" **không có tiêu chí nào để từ chối thứ gì cả**, nên nó luôn phình
> thành ngăn kéo tạp. Còn khi luật nghiệp vụ nằm trong module sở hữu nó, chiều phụ thuộc hiện
> rõ trong khai báo interface — đọc là thấy ai phụ thuộc ai.
>
> (`Shared/` ở B4 là ngoại lệ có kiểm soát: nó chỉ chứa **hạ tầng** — CurrentUser, Permissions,
> CursorPage, ProblemDetails. Không có nghiệp vụ nào ở đó, và ArchUnit test canh chừng.)

### Bảng tín hiệu — điều kiện và điểm bắt đầu

| Việc | Chỉ làm khi | ▶ Bắt đầu từ |
|---|---|---|
| **Cache Redis cho Feed** | Đã đo `EXPLAIN ANALYZE` + k6 và **p95 vượt 500ms** (NFR-PERF-01). Không cache khi chưa đo | `Modules/Content/Feed/FeedCacheKeys.cs` — khai key + TTL ở **một chỗ**: `feed:{userId}:p1` TTL 30s, `friends:{userId}` TTL 60s. Ghi comment: *hủy kết bạn có độ trễ ≤60s — chấp nhận được, xem 5.6* |
| **SignalR Redis backplane** | Chạy **≥2 instance API**. Một instance thì không cần | `builder.Services.AddSignalR().AddStackExchangeRedis(cs)` + chuyển presence từ memory sang Redis |
| **Background worker** | Có việc mất **hàng giây** hoặc gọi mạng ngoài đang nằm trong request: gửi email xác minh, dọn media mồ côi, đối soát bộ đếm | `Infrastructure/Jobs/JobNames.cs` — tên job + kiểu payload, **nguồn duy nhất** cho cả bên đẩy lẫn bên xử lý. **Job chỉ mang id**, processor đọc lại từ DB |
| **`Integrations/<x>`** | Thật sự phải gọi ra ngoài tiến trình qua mạng (R2, SMTP) | `Integrations/Storage/IObjectStorage.cs` — khai hợp đồng **trước**: giữ tên trường gốc của họ ở lớp wire, phơi ra ngoài bằng kiểu của mình. Kèm Polly retry + timeout 5s |
| **Job dọn dữ liệu (purge)** | Đã có dữ liệu soft-delete tích tụ (post/comment > 90 ngày, tài khoản deactivated > 30 ngày) | `Shared/Maintenance/IPurgeable.cs` — interface `{ string PurgeName; Task<int> PurgeAsync(DateTimeOffset before, int batch, CancellationToken ct); }`. Mỗi service tự `implements`; `PurgeHostedService` chỉ lặp qua danh sách. Thêm một entity vào hệ dọn = `implements` + **một dòng** đăng ký |
| **Sinh type từ OpenAPI** | Có **≥2 client** (web + mobile), hoặc số type viết tay vượt ~8 | Xuất `openapi.json` trong CI → `openapi-typescript` sinh `types/api.d.ts` |
| **Nâng component lên `components/ui/`** | **Ngưỡng-3**: copy lần 2 thì ghi nợ vào `ARCHITECTURE.md`; **lần 3 thì bắt buộc** nâng và sửa cả hai chỗ cũ | Tạo file trong `components/ui/`, rồi xóa **cả hai** bản sao cũ trong cùng một commit |

### Thứ tự đo trước khi tối ưu Feed

Đây là phần dễ làm sai nhất — đừng cache trước khi đo:

1. Seed 10k user, 100k post, 500k friendship bằng script (`make db-seed`).
2. Mở DBeaver, chạy `EXPLAIN (ANALYZE, BUFFERS)` cho truy vấn feed. Đọc: có **Index Scan** không?
   Có `Rows Removed by Filter` lớn không?
3. Sửa index / viết lại truy vấn trước. **Index rẻ hơn cache**, và không gây dữ liệu cũ.
4. Chạy k6 1.000 CCU. Nếu p95 vẫn > 500ms → **lúc này** mới thêm cache Redis 30s cho trang đầu.
5. Ghi số đo trước/sau vào `docs/perf.md`. Báo cáo NFR-PERF-01 cần con số này.

### Chốt kiểm tra

- [ ] Mọi thứ trong `Shared/` là hạ tầng, không có nghiệp vụ nào.
- [ ] ArchUnitNET xác nhận không controller nào cầm `HttpClient` hay `DbContext` trực tiếp.
- [ ] Job trong hàng đợi chỉ chứa id, không chứa dữ liệu nghiệp vụ.
- [ ] Mỗi quyết định cache có **số đo trước/sau** trong `docs/perf.md`.
- [ ] Tắt Redis khi có cache → feed **vẫn trả dữ liệu** (đọc thẳng DB), chỉ chậm hơn.

---

## B9 · Vận hành — deploy bằng Dokploy

**▶ Bắt đầu từ**: viết `deploy/Dockerfile` cho API, dạng multi-stage.

**Điều kiện vào**: đã có ≥1 lát cắt hoàn chỉnh chạy được ở local.

> Có thể làm B9 **sớm hơn**, ngay sau B6, và nên thế: deploy lát cắt đầu tiên lên staging khi
> hệ thống còn nhỏ thì mọi lỗi hạ tầng dễ cô lập. Đừng để dồn deploy vào tuần cuối.

### Thứ tự thực hiện

**1. `deploy/Dockerfile` — API**

```dockerfile
# Stage build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/SocialNet.Api/SocialNet.Api.csproj", "src/SocialNet.Api/"]
RUN dotnet restore "src/SocialNet.Api/SocialNet.Api.csproj"    # tách restore để cache layer
COPY . .
RUN dotnet publish "src/SocialNet.Api/SocialNet.Api.csproj" -c Release -o /app

# Stage runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Ảnh dotnet đã có sẵn user non-root với UID $APP_UID — không chạy bằng root.
USER $APP_UID
# EXPOSE chỉ là tài liệu. Cổng thật đọc từ ASPNETCORE_HTTP_PORTS — đừng hiểu nhầm là chốt cứng.
EXPOSE 8080
ENTRYPOINT ["dotnet", "SocialNet.Api.dll"]
```

**2. `.dockerignore`** — bỏ `bin/`, `obj/`, `.git/`, `node_modules/`, `tests/`. Thiếu file này
thì mỗi lần build đẩy hàng trăm MB lên daemon và cache layer vô dụng.

**3. `deploy/Dockerfile` — frontend (nếu có).** Bẫy lớn: Next.js **inline biến môi trường vào
bundle lúc build**. Mọi `NEXT_PUBLIC_*` phải là `ARG` + `ENV` ở stage builder. Thiếu một cái =
biến rỗng trong bundle production, và **không có lỗi nào báo** — chỉ là API URL thành
`undefined` khi người dùng thật truy cập.

**4. `deploy/docker-compose.yml` cho Dokploy**

```yaml
services:
  api:
    build: { context: .., dockerfile: deploy/Dockerfile }
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8080
      ConnectionStrings__Postgres: ${POSTGRES_CONNECTION}
      ConnectionStrings__Redis: ${REDIS_CONNECTION}
      Jwt__Secret: ${JWT_SECRET}
      Cors__Origins: ${CORS_ORIGINS}
    expose: ["8080"]          # KHÔNG dùng ports: "80:80" — 80/443 thuộc về Traefik của Dokploy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 15s
      retries: 3
```

> **Ba bẫy Dokploy phải biết trước**:
> 1. **Không bind cổng 80/443.** Traefik của Dokploy giữ hai cổng đó. Dùng `expose` và cấu
>    hình domain trong tab Domains.
> 2. **Healthcheck phải pass.** Nếu healthcheck fail, Traefik **bỏ qua** service đó và không
>    tạo route — bạn sẽ thấy 404 mà không hiểu vì sao. Đây là lý do `/health/ready` phải
>    tồn tại và phải trả 200 nhanh.
> 3. **Đổi biến môi trường phải redeploy.** Container không tự nhận biến mới.

**5. Cài Dokploy trên VPS**

```bash
curl -sSL https://dokploy.com/install.sh | sh
```

Yêu cầu: Ubuntu/Debian có Docker, **mở cổng 80 và 443** trên firewall VPS *và* trên security
group của nhà cung cấp (thiếu bước này thì Let's Encrypt cấp chứng chỉ thất bại **âm thầm**).
Truy cập Dokploy UI ở `http://<ip>:3000`, tạo tài khoản admin đầu tiên ngay (trang đăng ký chỉ
mở cho người đầu tiên).

**6. Tạo hạ tầng trong Dokploy**

1. **Project** → `socialnet`.
2. **Database → PostgreSQL 16**: đặt tên `socialnet-db`, ghi lại user/password. Dokploy quản lý
   luôn backup cho service này.
3. **Database → Redis 7**: `socialnet-cache`.
4. Lấy **internal connection string** của cả hai (dạng `socialnet-db:5432`) — dùng tên service,
   không dùng IP.

**7. Tạo ứng dụng và deploy**

1. **Application** (hoặc **Compose** nếu deploy cả web + api cùng lúc) → Source: **GitHub**,
   chọn repo và nhánh `main`.
2. Build Type: **Dockerfile**, trỏ tới `deploy/Dockerfile`.
3. Tab **Environment** — dán toàn bộ biến từ `.env.example`, điền giá trị thật:
   ```
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__Postgres=Host=socialnet-db;Port=5432;Database=socialnet;Username=...;Password=...
   ConnectionStrings__Redis=socialnet-cache:6379,abortConnect=false
   Jwt__Secret=<32+ ký tự ngẫu nhiên, KHÁC với dev>
   Cors__Origins=https://app.example.com
   ```
4. Tab **Domains** → thêm `api.example.com`, Container Port `8080`, bật **Let's Encrypt**.
   Trước đó tạo bản ghi DNS A trỏ về IP VPS.
5. Bật **Auto Deploy** (webhook) → push lên `main` là tự build + deploy.
6. Bấm **Deploy**, xem log trực tiếp.

**8. Chiến lược migration khi deploy**

Chọn **một** và ghi vào `ARCHITECTURE.md`:

| Cách | Ưu | Nhược | Khuyến nghị |
|---|---|---|---|
| `db.Database.MigrateAsync()` lúc boot, có cờ `RunMigrationsOnStartup` | Đơn giản nhất | Hai instance khởi động cùng lúc sẽ tranh nhau | ✔ cho MVP 1 instance |
| Bundle: `dotnet ef migrations bundle` → chạy trước khi start | An toàn khi scale | Thêm một bước trong pipeline | Khi lên ≥2 instance |

Dù chọn cách nào, **migration phải backward-compatible một phiên bản** (expand–contract): thêm
cột nullable trước, ghi dữ liệu, rồi mới bỏ cột cũ ở lần deploy sau. Không bao giờ `DROP COLUMN`
trong cùng deploy với code còn dùng cột đó — đó là cách rollback biến thành mất dữ liệu.

**9. `/health/ready` — khác `/health/live` ở B3**

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(cs, name: "postgres", tags: ["ready"])
    .AddRedis(redisCs, name: "redis", failureStatus: HealthStatus.Degraded, tags: ["ready"]);
// Redis chỉ Degraded, KHÔNG Unhealthy — mất Redis không được làm Traefik gỡ instance khỏi LB.
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```

**10. Seed dữ liệu và tài khoản admin đầu tiên**

`Infrastructure/Persistence/Seed/` — roles và `reason_code` seed bằng `HasData` (idempotent);
admin đầu tiên tạo từ `Seed__AdminEmail` / `Seed__AdminPassword`, và **chỉ tạo khi bảng users
rỗng**. Ghi log rõ ràng khi tạo.

**11. Xem database production bằng DBeaver**

**Không mở cổng 5432 ra Internet.** Dùng SSH tunnel — DBeaver có sẵn:

1. Tab **Main**: Host `localhost`, Port `5432`, DB `socialnet`, user/password của Dokploy.
2. Tab **SSH**: bật *Use SSH Tunnel*, Host = IP VPS, user + private key.
3. Test Connection.

Nếu bắt buộc phải mở cổng (ví dụ chấm điểm cần), Dokploy cho phép pin cổng ngoài qua biến
`DB_EXTERNAL_PORT` — nếu không, Docker gán cổng ngẫu nhiên mới sau **mỗi lần** redeploy và kết
nối DBeaver sẽ hỏng liên tục.

**12. Backup và restore drill** (NFR-REL-02: RPO ≤ 15 phút, RTO ≤ 2 giờ)

Trong Dokploy: service PostgreSQL → tab **Backups** → cấu hình lịch + đích S3/R2.
**Rồi thực sự restore một lần** vào database tạm và ghi lại thời gian. Một bản backup chưa từng
được restore là một bản backup chưa biết có dùng được không — và báo cáo có mục yêu cầu bằng chứng này.

**13. Điều chỉnh index theo truy vấn chậm thật**
Bật `log_min_duration_statement = 500` trong PostgreSQL, chạy tải thật vài ngày, đọc log, thêm
index. **Không đoán trước.**

### Chốt kiểm tra

- [ ] Push lên `main` → Dokploy tự build và deploy, không cần thao tác tay.
- [ ] `https://api.example.com/health/ready` trả 200 với chứng chỉ Let's Encrypt hợp lệ.
- [ ] Image Docker chạy được với **chỉ** biến môi trường, không cần mount file cấu hình.
- [ ] Đổi `ASPNETCORE_HTTP_PORTS` → container nghe đúng cổng mới.
- [ ] Frontend production gọi đúng API URL (không phải `localhost`) — kiểm bằng tab Network.
- [ ] Đăng nhập được trên domain thật, cookie refresh token có cờ `Secure` + `HttpOnly`.
- [ ] `/swagger` **không** mở được ở production.
- [ ] Tắt container Redis trên VPS → web vẫn dùng được, chỉ chậm hơn.
- [ ] Đã restore backup thành công **ít nhất một lần** và ghi lại thời gian.

---

# Phụ lục A — Checklist một lát cắt dọc (copy dùng lại)

Dán vào issue/PR mỗi khi làm một module mới.

```
## Backend
- [ ]  1. Modules/<X>/Domain/<X>Enums.cs
- [ ]  2. Modules/<X>/Domain/<X>.cs              (invariant ép trong hàm khởi tạo)
- [ ]  3. Modules/<X>/Persistence/Configurations/<X>Configuration.cs
         (schema · ToTable · CHECK · index + comment nêu truy vấn dùng nó)
- [ ]  4. Modules/<X>/Persistence/<X>DbSets.cs   (partial AppDbContext)
- [ ]  5. dotnet ef migrations add Add<X>        (ĐỌC file sinh ra trước khi apply)
- [ ]  6. Contracts/Create<X>Request.cs
- [ ]  7. Contracts/Update<X>Request.cs
- [ ]  8. Contracts/<X>Response.cs               (3 lớp: Response · View · mapper thuần)
- [ ]  9. Validators/Create<X>RequestValidator.cs
- [ ] 10. Authorization/<X>AuthorizationHandler.cs   (TẦNG 3 — chống IDOR)
- [ ] 11. Services/<X>Service.cs                 (guard clause · transaction · không Configuration[])
- [ ] 12. Api/<X>Controller.cs                   (route + [Authorize] + [ProducesResponseType] đủ mã)
- [ ] 13. <X>ModuleExtensions.cs
- [ ] 14. Program.cs                             (+1 dòng, không hơn)

## Client
- [ ] 15. features/<x>/types/index.ts            (mirror DTO)
- [ ] 16. features/<x>/api/<x>Api.ts
- [ ] 17. features/<x>/api/queryKeys.ts          (factory phân cấp)
- [ ] 18. features/<x>/hooks/
- [ ] 19. features/<x>/components/               (đủ 4 trạng thái: pending→error→empty→list)
- [ ] 20. app/(app)/<x>/page.tsx                 (chỉ mount screen)
  (không làm frontend → thay 15–20 bằng Postman collection + integration test)

## Test
- [ ] 21. Happy path
- [ ] 22. 400 — vi phạm business rule
- [ ] 23. 401 — không token
- [ ] 24. 403 — truy cập chéo tài nguyên người khác (IDOR)

## Chốt
- [ ] Tạo/sửa/xóa được qua client thật
- [ ] Xóa là soft delete (kiểm bằng DBeaver)
- [ ] EXPLAIN ANALYZE là Index Scan
- [ ] Swagger UI hiển thị đủ mô tả và mọi mã trả về
- [ ] make check xanh
```

---

# Phụ lục B — Ánh xạ tài liệu PTTK → bước triển khai

| Mục trong `BaoCao_Nhom5_v4.docx` | Dùng ở bước |
|---|---|
| 1.3 Phạm vi MVP | B0 — quyết định module nào có, module nào không |
| 2.2 Danh mục Use Case | B7 — mỗi UC ≈ một lát cắt hoặc một endpoint |
| 2.4 Business Rules (BR-01..BR-09) | B6/B7 — **ép trong domain + CHECK constraint**, không chỉ ở validator |
| 4.1 FR-001..FR-020 | B4 (FR-001..003), B6/B7 (còn lại) |
| 4.4 NFR | B8 (PERF), B4 (SEC), B9 (REL, OBS) |
| 5.2 Domain object catalog | B0 — ERD; B6/B7 — entity |
| 5.5 Logical data model | B4/B6/B7 — `IEntityTypeConfiguration` |
| 5.6 Index và query | B6 — khai index; B8 — đo và chỉnh |
| 6.6 API contract | B0 — chốt hợp đồng; B4 — khung; B6/B7 — endpoint |
| 6.7.2 Ma trận Role–Permission | B4 — `Permissions.cs` + `RolePermissions.Map` |
| 6.7.5 TC-A01..TC-A07 | B4 (A01, A02, A05) và B6/B7 (A03, A04, A06, A07) — **phải là test tự động** |
| 6.8 Deployment | B9 — Dokploy |
| 6.10 ADR | B0 — chép vào `docs/adr/`, cập nhật khi quyết định đổi |

---

# Phụ lục C — Sáu sai lầm và luật chặn tương ứng

| Sai lầm | Hậu quả | Luật chặn | Chặn ở bước |
|---|---|---|---|
| Tạo thư mục rỗng "để dành" | Mời gọi đặt code sai chỗ | Thư mục chỉ sinh ra cùng file thật đầu tiên | **B1** |
| Làm hết backend rồi mới làm client | Quy ước sai chỉ lộ ra ở module thứ 8 | Lát cắt **dọc** | **B6** |
| Dựng `Shared/` nghiệp vụ từ đầu | Ngăn kéo tạp không có tiêu chí từ chối | `Shared/` chỉ chứa hạ tầng; ArchUnit canh | **B8** |
| Viết luật mà không có công cụ ép | Đến tuần 3 không ai đọc `ARCHITECTURE.md` nữa | ArchUnitNET + analyzer + `dotnet format` trong CI | **B1** |
| Chỉ RBAC, quên kiểm ownership | **IDOR — rủi ro Critical** trong chính báo cáo của nhóm | Mọi endpoint có `{id}` phải qua `AuthorizationHandler` tầng 3 | **B4** (khuôn), **B6/B7** (áp dụng) |
| Cache trước khi đo | Dữ liệu cũ + bug khó tái hiện, mà không nhanh hơn bao nhiêu | Có `EXPLAIN ANALYZE` và số k6 rồi mới cache | **B8** |

---

# Phụ lục D — Bốn thứ quyết định một lần, không đổi được rẻ

Chốt ở **B0** hoặc **B4**, ghi vào `ARCHITECTURE.md`. Đổi sau đều tốn.

1. **Ai được chạm PostgreSQL** — chỉ `SocialNet.Api`. Nới ra sau thì không bao giờ siết lại được.
2. **Auth model** — refresh token nằm ở đâu. Kéo theo dây chuyền: route guard chạy ở client hay
   edge · CORS `AllowCredentials` · cookie `SameSite` · web và api phải cùng domain gốc ·
   cấu hình domain trong Dokploy.
3. **`UnmappedMemberHandling.Disallow`** — bật ngay ở B4. Bật sau khi đã có 10 module là vỡ
   hàng loạt client đang gửi field thừa.
4. **Quy ước tên bảng và schema** (`content.posts`, `snake_case`, uuid v7) — đổi sau là rename
   toàn bộ DB và viết lại mọi migration.

---

# Phụ lục E — Gợi ý phân công cho đội 3 người

Lộ trình có thứ tự, nhưng trong một bước vẫn song song được. Nguyên tắc: **B0–B4 làm cùng nhau,
đừng chia**; chia việc từ B6 trở đi.

| Giai đoạn | Cách chia |
|---|---|
| B0–B2 | **Cả ba ngồi cùng.** Đây là lúc quyết định thứ đắt nhất — chia ra là ba người hiểu ba kiểu |
| B3–B4 | Một người dựng khung + Identity; hai người còn lại viết `docs/`, dựng CI, chuẩn bị Postman collection và test âm bản |
| B5 | Một người frontend (nếu làm) |
| B6 | **Cả ba cùng làm lát cắt `Post`** (pair/mob). Đây là lát định khuôn — ba người phải cùng thấy khuôn đó |
| B7 | Chia theo nhánh đồ thị: người A `SocialGraph`+`Feed` · người B `Comment`+`Reaction`+`Notification` · người C `Messaging`+`Moderation` |
| B8–B9 | Một người vận hành (Dokploy, backup, k6), hai người còn lại đóng nốt lát cắt |

Deploy staging (B9) **nên chạy sớm, ngay sau B6**, để không dồn rủi ro hạ tầng vào tuần cuối.

---

*Hết. Tài liệu nguồn: `route.md` (khuôn lộ trình) và `BaoCao_Nhom5_v4.docx` (nghiệp vụ, ERD,
RBAC, NFR).*
