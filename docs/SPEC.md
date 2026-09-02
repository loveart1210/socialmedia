# SPEC — Yêu cầu nghiệp vụ (chưng cất từ BaoCao_Nhom5_v4.docx)

> Nguồn sự thật về NGHIỆP VỤ cho agent: schema, business rules, phân quyền, tiêu chí nghiệm thu.
> Kiến trúc/luật code xem `.claude/rules/`. Báo cáo gốc (đầy đủ, có sơ đồ) là nguồn cấp trên —
> file này lệch báo cáo thì sửa file này. Ánh xạ mục gốc ghi ở từng phần.

## 1. Danh mục Use Case (gốc: mục 2.2)

| ID | Use case | Actor | Priority |
|---|---|---|---|
| UC-01 | Đăng ký tài khoản | Guest | Must |
| UC-02 | Đăng nhập / Đăng xuất (JWT) | Guest, User | Must |
| UC-03 | Quản lý hồ sơ cá nhân | User | Must |
| UC-04 | Đăng bài viết (theo quyền riêng tư) | User | Must |
| UC-05 | Sửa / Xóa bài viết | User | Must |
| UC-06 | Bình luận (trả lời ≤ 3 cấp) | User | Must |
| UC-07 | Thả / gỡ cảm xúc | User | Must |
| UC-08 | Xem News Feed | User | Must |
| UC-09 | Xem trang cá nhân | Guest, User | Must |
| UC-10 | Gửi / hủy lời mời kết bạn | User | Must |
| UC-11 | Chấp nhận / từ chối lời mời | User | Must |
| UC-12 | Hủy kết bạn | User | Should |
| UC-13 | Theo dõi / bỏ theo dõi | User | Must |
| UC-15 | Nhắn tin 1-1 realtime (Sent/Delivered/Seen) | User | Must |
| UC-16 | Tìm kiếm người dùng (tiền tố, không dấu) | User | Should |
| UC-17 | Nhận và xem thông báo | User | Should |
| UC-18 | Báo cáo nội dung / người dùng | User | Should |
| UC-19 | Kiểm duyệt báo cáo (ẩn/gỡ nội dung) | Moderator | Should |
| UC-20 | Quản trị người dùng & vai trò | Admin | Should |

UC-14 (Chặn người dùng) **ngoài phạm vi** — thay bằng BR-09. Không có UC-14, giữ nguyên đánh số.

Actor: `Guest` (chỉ đăng ký/đăng nhập/xem public) · `User` · `Moderator` (xử lý báo cáo, không xóa
tài khoản/đổi vai trò) · `Admin` (mọi thao tác đều ghi audit log). Hệ ngoài: Email Service
(xác minh, đặt lại mật khẩu), Object Storage (ảnh, phục vụ qua pre-signed URL).

## 2. Business Rules (gốc: mục 2.4 — không có BR-04)

| ID | Quy tắc | Hiện thực / kiểm chứng |
|---|---|---|
| BR-01 | Bài viết phải có văn bản (≤ 5.000 ký tự) HOẶC ≥ 1 ảnh; tối đa 10 ảnh, mỗi ảnh ≤ 10MB | CHECK constraint + test API |
| BR-02 | Quyền xem bài: `public` — mọi người; `friends` — chỉ bạn bè; `private` — chỉ tác giả. Đánh giá **tại thời điểm đọc** | Integration test theo ma trận quyền |
| BR-03 | Mỗi cặp người dùng tối đa 1 quan hệ bạn bè (Pending/Accepted); không tự kết bạn | PK(user_min, user_max) + CHECK user_min < user_max |
| BR-05 | Mỗi người chỉ 1 cảm xúc trên 1 bài/bình luận; thả loại khác thì **thay thế** | PK(user, target_type, target_id) |
| BR-06 | Chỉ 2 thành viên của hội thoại được đọc/gửi tin | AuthZ test |
| BR-07 | Nội dung bị gỡ chuyển `hidden`: tác giả thấy kèm lý do, người khác không thấy | Test hiển thị theo vai trò |
| BR-08 | Bình luận tối đa 3 cấp; xóa thì hiển thị "Bình luận đã bị xóa", **giữ nhánh trả lời** | Kiểm ở tầng ứng dụng |
| BR-09 | Chỉ bạn bè mới tạo hội thoại và nhắn tin; hủy kết bạn → hội thoại **chỉ đọc** (thay tính năng chặn) | AuthZ test |

## 3. Mô hình dữ liệu (gốc: mục 5.2, 5.3, 5.5)

### 3.1 Danh mục domain object

| ID | Tên | Loại | Ghi chú | Bounded context |
|---|---|---|---|---|
| ENT-01 | User | Aggregate root | email, password băm, status, role | Identity |
| ENT-01a | Profile | Entity 1-1 User | thông tin hiển thị công khai | Profile |
| ENT-02 | Post | Aggregate root | nội dung, privacy, status, bộ đếm | Content |
| ENT-03 | Comment | Entity thuộc Post | tự tham chiếu ≤ 3 cấp | Content |
| ENT-04 | Friendship | Aggregate root | identity = cặp (user_min, user_max) | Social Graph |
| ENT-04a | Follow | Entity | một chiều follower→followee | Social Graph |
| ENT-05 | Reaction | Value Object | identity = (user, target_type, target_id) | Content |
| ENT-06 | Conversation | Aggregate root | 1-1 duy nhất mỗi cặp | Messaging |
| ENT-07 | Message | Entity thuộc Conversation | seq + Sent/Delivered/Seen | Messaging |
| ENT-08 | MediaAttachment | Entity | trỏ Object Storage | Content |
| ENT-09 | Notification | Entity | có thể gộp theo group_key | Notification |
| ENT-10 | Role | Reference data | User=1, Moderator=2, Admin=3 | Identity |
| ENT-11 | RefreshToken | Entity | xoay vòng, thu hồi được | Identity |
| ENT-12 | Report | Aggregate root | báo cáo + kết quả xử lý | Moderation |
| ENT-13 | AuditLog | Event append-only | thao tác Mod/Admin | Moderation |

### 3.2 Bảng chi tiết — `users` (ENT-01)

| Column | Type | Null | Constraint / Rule | Ghi chú |
|---|---|---|---|---|
| id | uuid | No | PK, uuid v7 | |
| email | citext | No | UQ, format email | PII |
| username | varchar(30) | No | UQ, `[a-z0-9_.]` | |
| password_hash | varchar(72) | No | BCrypt cost 12 | **không bao giờ trả ra API** |
| display_name | varchar(50) | No | 1–50 ký tự | |
| role_id | smallint | No | FK roles, RESTRICT | 1=User 2=Mod 3=Admin |
| status | varchar(20) | No | CK: pending, active, suspended, deactivated, anonymized | |
| failed_login_count | smallint | No | ≥ 0 | phục vụ lockout |
| locked_until | timestamptz | Yes | — | khóa tạm thời |
| created_at / updated_at | timestamptz | No | now() | |

### 3.3 Bảng chi tiết — `posts` (ENT-02)

| Column | Type | Null | Constraint / Rule | Ghi chú |
|---|---|---|---|---|
| id | uuid | No | PK, uuid v7 | tối ưu sắp feed |
| author_id | uuid | No | FK users | |
| content | text | Yes | CK ≤ 5000; NOT NULL nếu không có ảnh (BR-01) | |
| privacy | varchar(10) | No | CK: public, friends, private (BR-02) | |
| status | varchar(10) | No | CK: published, hidden, deleted (BR-07) | |
| comment_count | int | No | ≥ 0 | bộ đếm phi chuẩn hóa |
| reaction_counts | jsonb | No | default `'{}'` | `{"like":10,...}` |
| created_at | timestamptz | No | now() | khóa sắp xếp feed |

### 3.4 Các bảng còn lại — ràng buộc quan trọng

| Bảng | Ràng buộc |
|---|---|
| profiles | PK/FK user_id, 1-1 users; birthday là PII |
| friendships | PK(user_min, user_max); CK user_min < user_max (BR-03) |
| follows | PK(follower, followee); CK follower <> followee |
| comments | parent_id nullable; ≤ 3 cấp kiểm ở tầng ứng dụng (BR-08) |
| reactions | PK(user, target_type, target_id) (BR-05) |
| media_attachments | CK size_bytes ≤ 10485760; job dọn bản ghi mồ côi |
| conversations | UQ(user_a, user_b) + CK a < b — 1 hội thoại/cặp |
| messages | UQ(conversation_id, seq) — thứ tự; UQ(conversation_id, client_msg_id) — idempotency |
| notifications | UQ(recipient_id, group_key) — gộp thông báo |
| refresh_tokens | lưu **băm** của token; xoay vòng |
| reports | reason_code chuẩn hóa: spam, harassment, nudity, violence, other |
| audit_logs | append-only, không UPDATE/DELETE |

### 3.5 Aggregate & transaction boundary (gốc: 5.3)

| Aggregate | Invariant | Transaction |
|---|---|---|
| User | email duy nhất; password luôn băm; user khóa không đăng nhập được | tạo User + Profile cùng transaction |
| Post | BR-01, BR-05, BR-08; bộ đếm nhất quán với bản ghi thật | tạo/xóa bài + media atomically; cập nhật bộ đếm cùng transaction |
| Friendship | BR-03 | Pending→Accepted là 1 UPDATE |
| Conversation | 1 hội thoại/cặp; `seq` tăng đơn điệu; BR-06 | ghi tin + tăng seq + last_message atomically |
| Report | 1 báo cáo 1 kết luận; Open→Resolved/Dismissed một chiều | cập nhật kết luận + ghi AuditLog cùng transaction |

Feed chấp nhận hiển thị trễ ≤ 5s; trạng thái Delivered/Seen là eventual.

### 3.6 Index cho truy vấn nóng (gốc: 5.6)

| Truy vấn | Index |
|---|---|
| Feed (UC-08) / trang cá nhân (UC-09) | idx(author_id, created_at DESC) WHERE status='published'; cache Redis 30s trang đầu |
| Tải hội thoại (UC-15) | idx(conversation_id, seq DESC); cursor theo seq, **không OFFSET** |
| Tìm user (UC-16) | GIN pg_trgm trên unaccent(display_name) |
| Badge thông báo (UC-17) | partial index WHERE is_read = false |
| Kiểm tra quan hệ bạn bè | PK cặp + cache Redis TTL 60s (chấp nhận trễ 60s khi hủy kết bạn) |

### 3.7 Lifecycle & seed (gốc: 5.7 — phần chạm code)

- Seed `roles`, `reason_code` bằng EF Core migration, idempotent; admin đầu tiên tạo qua biến môi trường.
- Migration versioned, backward-compatible 1 phiên bản (expand–contract).
- Post/Comment soft-delete giữ 90 ngày rồi xóa cứng; tài khoản deactivated ẩn danh PII sau 30 ngày (job).
- Job đêm đối soát bộ đếm (`comment_count`, `reaction_counts`) với bản ghi thật.

## 4. Phân quyền (gốc: mục 6.7.2, 6.7.3)

Nguyên tắc: **default deny, least privilege**. `own` = chỉ tài nguyên của mình; `bạn` = chỉ khi là bạn bè.

| Permission | Guest | User | Moderator | Admin |
|---|---|---|---|---|
| post.read.public | ✔ | ✔ | ✔ | ✔ |
| post.read.friends | — | ✔(bạn) | ✔ | ✔ |
| post.create · comment.create · reaction.set · friend.request/respond · report.create | — | ✔ | ✔ | ✔ |
| post.update / post.delete | — | ✔(own) | — | ✔ |
| post.hide (BR-07) | — | — | ✔ | ✔ |
| message.send | — | ✔(bạn) | ✔(bạn) | ✔(bạn) |
| report.resolve | — | — | ✔ | ✔ |
| user.lock / user.unlock · role.assign · audit.read | — | — | — | ✔ |

JWT access token (HS256 ở MVP, TTL **15 phút**) — claims: `sub` (user_id), `role`, `iat/exp`, `jti`.
Luồng: login → access + refresh (lưu băm trong DB) → `/auth/refresh` xoay vòng; **phát hiện reuse
refresh token → thu hồi cả chuỗi**; đổi mật khẩu/đăng xuất → thu hồi toàn bộ phiên.
Lockout: sai 5 lần/15 phút → khóa 15 phút (`failed_login_count`, `locked_until`).

### Test phân quyền bắt buộc (gốc: 6.7.5)

| TC | Kịch bản | Kỳ vọng |
|---|---|---|
| TC-A01 | Gọi API bảo vệ không kèm JWT | 401 |
| TC-A02 | JWT hết hạn / chữ ký sai | 401 |
| TC-A03 | User A `PATCH /posts/{id của B}` | 403 (IDOR) |
| TC-A04 | Đọc `/conversations/{id}` khi không phải thành viên | 403 (IDOR) |
| TC-A05 | User thường gọi `/admin/*` | 403 |
| TC-A06 | User thường gọi `PATCH /reports/{id}` | 403 |
| TC-A07 | Gửi tin cho người không phải bạn bè (BR-09) | 403 |

## 5. Acceptance Criteria — Given/When/Then (gốc: mục 3.4)

### US-002 · Đăng nhập
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | tài khoản đã xác minh email | đúng email + mật khẩu | access (15p) + refresh token |
| AC-02 | email đúng, mật khẩu sai | đăng nhập | 401 "Sai thông tin", **không lộ email tồn tại**; failed_login_count++ |
| AC-03 | đã sai 5 lần liên tiếp | thử lần 6 | **423 Locked**, khóa 15 phút |
| AC-04 | chưa xác minh email | đúng mật khẩu | 403, gợi ý gửi lại email xác minh |

### US-004 · Đăng bài
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | đã đăng nhập, upload 3 ảnh | đăng bài public | 201; bài xuất hiện trên feed bạn bè |
| AC-02 | bài rỗng, không ảnh | đăng | 400 (BR-01), không tạo bài |
| AC-03 | chọn 11 ảnh | đăng | 400 "tối đa 10 ảnh" |
| AC-04 | JWT hết hạn | đăng | 401 |

### US-008 · News Feed
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | có bạn bè đã đăng bài | mở feed | 20 bài mới nhất, giảm dần theo thời gian + cursor kế tiếp |
| AC-02 | bài `friends` của người không phải bạn | mở feed | bài KHÔNG xuất hiện (BR-02) |
| AC-03 | bài bị Moderator gỡ | mở feed (không phải tác giả) | bài không xuất hiện (BR-07) |
| AC-04 | 1.000 người dùng đồng thời | tải feed | p95 ≤ 500ms |

### US-010 · Kết bạn
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | A, B chưa là bạn | A mời, B chấp nhận | Accepted; cả hai là bạn + được thông báo |
| AC-02 | đã có quan hệ | A gửi lại lời mời | 409 "đã tồn tại" |
| AC-03 | — | A gửi cho chính mình | 400 |
| AC-04 | lời mời gửi cho B | C bấm chấp nhận | 403 |

### US-015 · Nhắn tin realtime
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | A, B là bạn; B online | A gửi tin | B nhận ≤ 1s; Sent→Delivered→Seen |
| AC-02 | B offline | A gửi tin | tin lưu bền; B nhận + badge khi online lại |
| AC-03 | mất mạng, client gửi lại cùng clientMsgId | gửi lại | không tạo tin trùng (UQ conv, client_msg_id) |
| AC-04 | A, B không phải bạn | A gửi tin | 403; hội thoại chỉ đọc (BR-09) |

### US-019 · Kiểm duyệt
| AC | Given | When | Then |
|---|---|---|---|
| AC-01 | Moderator; báo cáo Open vi phạm | chọn "ẩn nội dung" | post=hidden (BR-07); report=Resolved; có audit_log |
| AC-02 | nội dung không vi phạm | chọn "bỏ qua" | report=Dismissed; nội dung giữ nguyên; có audit_log |
| AC-03 | User thường | gọi endpoint kiểm duyệt | 403 + audit |
| AC-04 | báo cáo đã xử lý | xử lý lại | 409 |

## 6. Ràng buộc phi chức năng chạm code

- Mật khẩu: BCrypt cost 12. Rate limit cho auth và đăng bài/bình luận (chống spam).
- Chống IDOR: authz theo resource (ownership/membership) ở tầng application — đây là rủi ro
  được đánh giá **Critical** trong threat model, TC-A03/A04 phải chạy trong CI.
- Ảnh: re-encode phía server (chống XSS qua file ảnh); phục vụ qua pre-signed URL.
- Email xác minh khi đăng ký, đặt lại mật khẩu — qua Email Service (adapter Infra).
