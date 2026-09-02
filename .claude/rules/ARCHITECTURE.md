# SocialMedia — Kiến trúc hiện tại

> Nguồn sự thật cho agent về KIẾN TRÚC. Yêu cầu nghiệp vụ (use case, business rules BR-*,
> schema, phân quyền, acceptance criteria): `docs/SPEC.md` — kiến trúc mâu thuẫn với SPEC
> thì SPEC thắng. Cập nhật file này mỗi khi thêm/bỏ service, đổi ranh giới hoặc đổi hợp đồng.

## 1. Hai subproject

| Thư mục | Vai trò | Stack | Cổng | Quản lý gói |
|---|---|---|---|---|
| `socialmedia_api` | Backend chính, sở hữu toàn bộ dữ liệu | ASP.NET Core (.NET 10), EF Core / PostgreSQL, Redis | 5000 | NuGet (dotnet) |
| `socialmedia_web` | Web app (người dùng cuối) | Next.js 15 App Router, React 19, Tailwind, TanStack Query 5 | 3000 | pnpm |

Điều phối bằng `Makefile` ở gốc (`make dev-api`, `make dev-web`, `make check`…).

## 2. Ranh giới & chiều gọi

```mermaid
flowchart LR
  W[socialmedia_web] -->|REST /api/v1 + Bearer| A[socialmedia_api]
  W -->|WebSocket /hubs/*| A
  A --> DB[(PostgreSQL)]
  A --> R[(Redis — cache + presence + backplane SignalR + queue)]
  A --> S[(Object storage — ảnh, phục vụ qua pre-signed URL)]
  A -->|SMTP| MX[(Email service — xác minh email, đặt lại mật khẩu)]
```

Luật bất biến:

- **Chỉ `socialmedia_api` chạm PostgreSQL.** Web không bao giờ nói chuyện trực tiếp với DB.
- **Realtime (nhắn tin, thông báo) đi qua SignalR hub của API** — web không tự mở kết nối tới Redis hay dịch vụ nào khác.
- **Upload ảnh đi qua API**: validate (≤ 10MB/ảnh, ≤ 10 ảnh/bài — BR-01, kiểm magic bytes) +
  **re-encode phía server** rồi mới đẩy lên object storage; client đọc ảnh qua **pre-signed URL**.
  Web không upload thẳng lên storage.
- **Phân quyền default deny** theo ma trận SPEC.md mục 4; chống IDOR bằng kiểm
  ownership/membership ở tầng service (rủi ro Critical — TC-A03/A04 chạy trong CI).
- `X-Request-Id` do API sinh cho mỗi request → truy vết một thao tác xuyên log của API và hub.

## 3. Hợp đồng HTTP của `socialmedia_api`

- Prefix chung `/api`, URI versioning mặc định `v1` → mọi route là `/api/v1/<resource>`.
- `/api/health` là version-neutral (không có `/v1`).
- Swagger UI `/docs` **chỉ bật ở development**. Prod tắt để không lộ contract.
- Validate toàn cục bằng FluentValidation + model binding chặt: field không khai trong DTO sẽ bị **từ chối 400**, không phải bỏ qua.
- AuthN: JWT HS256, access TTL 15 phút, claims `sub`/`role`/`jti`. **Mọi route mặc định cần
  Bearer token** (fallback policy `RequireAuthenticatedUser`); mở public bằng `[AllowAnonymous]`.
- Refresh token: **cookie httpOnly** (`credentials: true` ở cả CORS lẫn axios), lưu **băm** trong DB,
  xoay vòng mỗi lần dùng; phát hiện reuse → thu hồi cả chuỗi phiên.
- Đăng nhập có **xác minh email** (chưa xác minh → 403) và **lockout** (sai 5 lần/15 phút → 423) — SPEC.md US-002.
- AuthZ: RBAC 3 vai trò **`User` / `Moderator` / `Admin`** + kiểm quyền theo resource trong service.
- SignalR hubs mount tại `/hubs/chat`, `/hubs/notifications`, xác thực bằng cùng JWT (query `access_token` khi handshake).

## 4. Module nghiệp vụ (src/Modules)

`Auth` · `Users` (profile + tìm kiếm) · `Friendships` (kết bạn) · `Follows` (theo dõi) · `Posts` ·
`Comments` (trả lời ≤ 3 cấp) · `Reactions` (thả cảm xúc) · `Conversations` (nhắn tin 1-1) ·
`Notifications` · `Moderation` (báo cáo + kiểm duyệt + audit log) · `Admin` (khóa tài khoản, gán vai trò)

Ngoài nghiệp vụ: `Health` (`/api/health`, version-neutral).

Sơ đồ phụ thuộc chính (entity theo SPEC.md mục 3):

```
Auth (User, RefreshToken, Role)
 └─ Users (Profile)
     ├─ Friendships (Friendship — cặp user_min < user_max)
     ├─ Follows (Follow)
     ├─ Posts (Post, MediaAttachment) ─┬─ Comments (Comment, parentId ≤ 3 cấp)
     │                                 └─ Reactions (Reaction — đa hình: post/comment)
     ├─ Conversations (Conversation, Message) ─> hub /hubs/chat
     ├─ Notifications (Notification) ─> hub /hubs/notifications
     └─ Moderation (Report, AuditLog) ─> Posts/Comments (post.hide — BR-07)
Admin ─> Users (lock/unlock), Auth (role.assign), Moderation (audit.read)
```

Quy ước dữ liệu (chi tiết cột/constraint: SPEC.md mục 3):

- Bài viết: `privacy` **3 mức** `public / friends / private` (BR-02, đánh giá tại thời điểm đọc)
  + `status` `published / hidden / deleted` (BR-07 — kiểm duyệt gỡ bài là chuyển `hidden`,
  tác giả vẫn thấy kèm lý do).
- `Comment.parentId` tự tham chiếu, **giới hạn độ sâu 3** (BR-08) — API từ chối reply cấp 4;
  xóa comment giữ nhánh trả lời, hiển thị "Bình luận đã bị xóa".
- `Reaction` đa hình (targetType + targetId), PK `(user, target_type, target_id)` — một user
  một cảm xúc/target, thả loại khác là thay thế (BR-05).
- `Friendship` identity là cặp `(user_min, user_max)` với CHECK `user_min < user_max` (BR-03);
  mỗi cặp tối đa 1 hội thoại (`UQ(user_a, user_b)`, CK `a < b`).
- Nhắn tin: chỉ bạn bè (BR-06/BR-09 — hủy kết bạn thì hội thoại **chỉ đọc**); `Message` có
  `seq` tăng đơn điệu trong hội thoại (UQ) + `client_msg_id` idempotency (UQ) + trạng thái
  Sent/Delivered/Seen.
- **Ngoại lệ counter có chủ đích**: `Post.comment_count` + `reaction_counts` (jsonb) là bộ đếm
  phi chuẩn hóa — cập nhật cùng transaction với bản ghi thật, job đêm đối soát (SPEC.md 3.5/3.7).
  Ngoài hai bộ đếm này, không lưu trạng thái suy được từ dữ liệu khác.
- Feed đọc từ PostgreSQL (fan-out-on-read), cache Redis 30s trang đầu; kiểm tra quan hệ bạn bè
  cache TTL 60s.
- Seed `roles`, `reason_code` bằng migration idempotent; admin đầu tiên qua biến môi trường.

## 5. Frontend (socialmedia_web)

- `app/` chỉ là routing + shell: mỗi `page.tsx` mount một *screen* nằm trong `features/`.
- Provider stack (`app/layout.tsx`): `AuthProvider` → `Providers` (QueryClient + Toast) → `SignalRProvider`.
- `lib/axios.ts` giữ **token bridge** và **single-flight refresh** khi gặp 401.
- Mỗi domain là một feature slice: `features/<name>/{api,components,hooks,lib,types,errors.ts,index.ts}`
  — `auth`, `profile`, `feed`, `post`, `comments`, `chat`, `friends`, `notifications`,
  `moderation` (màn hình Moderator/Admin).

## 6. Triển khai

- Đóng gói bằng Docker (mỗi subproject một `Dockerfile`), deploy lên VPS qua **Dokploy**.
- PostgreSQL xem/quản trị bằng DBeaver; API thử bằng Swagger/Postman.
- Migration EF Core chạy lúc deploy (`dotnet ef database update` trong pipeline), không
  auto-migrate lúc app khởi động ở prod; migration backward-compatible 1 phiên bản (expand–contract).

## 7. Nợ kỹ thuật đã biết

| Vấn đề | Chi tiết |
|---|---|
| Job định kỳ chưa xếp lịch xây | SPEC.md 3.7: dọn media mồ côi, xóa cứng sau 90 ngày, ẩn danh PII sau 30 ngày, đối soát bộ đếm. Chưa nằm trong ROADMAP phase nào — bổ sung khi backend xong phần chính. |
