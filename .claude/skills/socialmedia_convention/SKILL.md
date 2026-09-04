---

## name: socialmedia-conventions
description: "Use when writing or reviewing code in this social media monorepo — architecture questions, where a new module/feature belongs, naming and file layout, entity/DTO/query-key conventions, or authorization patterns. Examples: Thêm module mới đặt ở đâu?, Quy ước entity của backend?, Reaction đa hình khai thế nào?, Kiến trúc dự án thế nào?"

# SocialMedia — kiến trúc & luật code

Repo là **monorepo 2 subproject**: `socialmedia_api` (ASP.NET Core .NET 10 +
EF Core/PostgreSQL + Redis, cổng 5000) · `socialmedia_web` (Next.js 15, cổng 3000).
Chỉ `socialmedia_api` chạm database; realtime (chat, notification) đi qua
SignalR hub của API; upload ảnh đi qua API rồi mới lên object storage.

## Đọc file nào


| Việc đang làm                                    | Đọc            |
| ------------------------------------------------ | -------------- |
| Yêu cầu nghiệp vụ (use case, ERD, tài liệu PTTK) | `docs/SPEC.md` |


Việc đang làm mâu thuẫn với docs → dừng lại hỏi; được duyệt thì sửa docs
trong cùng commit với code.

## Ba điều dễ sai nhất

1. **Không query chéo bảng của module khác** — `Comments` cần biết bài viết
  tồn tại thì inject `PostsService`, quyền theo quan hệ thì gọi
   `FriendshipsService.AreFriendsAsync`, không tự sờ bảng `pst_`/`frd_`.
2. **Không tin client** — `userId` lấy từ claim `sub`; độ sâu reply (≤ 3 cấp),
  giới hạn ảnh (≤ 10MB, ≤ 10 ảnh/bài, kiểm magic bytes) đều chặn ở service.
3. **Field không khai trong DTO → request 400** (`UnmappedMemberHandling =
  Disallow`), không phải bị bỏ qua; và không bao giờ trả entity ra ngoài —
   luôn map sang record response.



## Trước khi báo xong

`make check` (chưa có `socialmedia_web` thì chạy
`make build-api format-api test-api`). Có migration mới thì `make migrate-api`
trên DB local + review SQL trước khi commit.