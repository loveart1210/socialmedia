# Luật code — `socialmedia_web` (Next.js 15 + React 19)

## 1. Feature slice

```
features/<name>/
├─ api/
│  ├─ <name>Api.ts       # wrapper axios mỏng, 1:1 với endpoint, trả data đã unwrap
│  └─ queryKeys.ts       # factory key react-query (nguồn duy nhất)
├─ components/           # screen + modal + card của feature
├─ hooks/                # useXxx (query) / useXxxMutations (mutation)
├─ lib/                  # helper thuần: format thời gian tương đối, badge, đếm rút gọn (1,2k)
├─ types/index.ts        # nguồn sự thật cho shape của domain (mirror DTO backend)
├─ errors.ts             # map HTTP status → thông điệp hiển thị
└─ index.ts              # BARREL: bề mặt công khai của feature
```

Các slice: `auth` · `profile` · `feed` · `post` · `comments` · `reactions` ·
`chat` · `friends` · `notifications`.

- `app/**/page.tsx` chỉ mount screen của feature, không chứa logic.
- Slice không gọi mạng thì thiếu `api/`/`errors.ts` là bình thường. Slice **có**
  gọi mạng thì phải đủ `api/queryKeys.ts` + `errors.ts` + `index.ts`.
- Import chéo feature **đi qua barrel**. Chỉ deep-import khi barrel tạo vòng
  lặp — và **phải comment lý do** ngay tại dòng import.

## 2. Dữ liệu (TanStack Query)

- Mọi lời gọi mạng qua `api` từ `lib/axios.ts` (baseURL `/api/v1`, Bearer,
  refresh 401 single-flight — xem ARCHITECTURE.md mục 5). Không `fetch` thẳng.
- Key luôn lấy từ `queryKeys.ts`; thêm tham số lọc mới thì **thêm vào cả
  `serializeParams`**, nếu không cache sẽ va nhau.
- Feed / danh sách comment / lịch sử tin nhắn dùng **`useInfiniteQuery` +
  cursor** (khớp cursor-based paging của API, api.md mục 5). `getNextPageParam`
  đọc `nextCursor` từ response — không tự tính offset.
- Query phụ thuộc quan hệ (bài friends-only của người khác) dùng `enabled` để
  không bắn request chắc chắn 403.
- Mutation `onSuccess` invalidate đúng key bị ảnh hưởng, kể cả key của feature
  khác (unfriend phải invalidate `feedKeys.all` vì feed lọc theo quan hệ).
- Thả cảm xúc / kết bạn dùng **optimistic update** (`onMutate` snapshot +
  `onError` rollback) — chờ round-trip mới đổi icon là cảm giác lag rõ nhất
  của mạng xã hội. Mutation khác (đăng bài, gửi tin) chờ server, không optimistic.

## 3. Realtime (SignalR)

- Kết nối hub tập trung ở `lib/signalr.ts` + `SignalRProvider`
  (`app/layout.tsx`): **một connection mỗi hub cho cả app**, feature đăng ký
  handler qua hook (`useChatHub`, `useNotificationsHub`), không tự
  `new HubConnection` trong component.
- Sự kiện hub **không tự vẽ UI**: handler ghi vào cache react-query
  (`setQueryData` cho tin nhắn mới, `invalidateQueries` cho notification) —
  màn hình vẫn chỉ đọc từ query. Một nguồn sự thật phía client là cache.
- Reconnect dùng `withAutomaticReconnect`; sau khi nối lại phải
  `invalidateQueries` các key hội thoại đang mở để bù tin nhắn rơi lúc đứt.
- Token cho handshake lấy qua `accessTokenFactory` đọc từ token bridge của
  `lib/axios.ts` — không đọc storage riêng.

## 4. Giao diện

- **Chỉ dùng design token** khai trong `tailwind.config`, không hex thô trong
  className. Bảng token chốt khi dựng UI kit; quy ước tên theo mẫu
  `text-ink`, `text-muted`, `bg-surface`, `text-brand`, `text-danger`.
- Primitive ở `components/ui/` — **tái sử dụng trước khi tạo mới**. Bộ tối
  thiểu cho MVP: `Modal`, `Button`, `Input`, `Textarea`, `ConfirmDialog`,
  `Toast`, `Skeleton`, `Spinner`, `Avatar`, `Badge`, `DropdownMenu`,
  `ImageGrid` (lưới ảnh bài viết ≤ 10 ảnh), `Lightbox`.
- Modal form theo mẫu: `<Modal footer={<Button type="submit" form={FORM_ID} …/>}>`
  + `<form id={FORM_ID}>`.
- Upload ảnh: preview client-side bằng `URL.createObjectURL` (nhớ revoke),
  chặn sớm > 10MB hoặc > 10 ảnh **trước khi** gửi — server vẫn kiểm lại, client
  chặn chỉ để UX.
- Nút chỉ có icon (like, menu bài viết) phải có `aria-label`; nút mở/đóng khối
  phải có `aria-expanded`.

## 5. Trạng thái màn hình

Mỗi danh sách xử lý đủ 4 trạng thái, theo đúng thứ tự này:

```tsx
{isPending ? <ListSkeleton />
 : isError ? <ErrorPanel onRetry={refetch} />
 : isEmpty ? <EmptyState … />
 : <List items={items} />}
```

- Danh sách infinite thêm trạng thái thứ 5 ở đuôi: sentinel
  `IntersectionObserver` gọi `fetchNextPage`, hiển thị `Spinner` khi
  `isFetchingNextPage`.
- `EmptyState` phân biệt ngữ cảnh: feed trống (CTA kết bạn) khác trang cá nhân
  người khác chưa đăng gì (chỉ thông báo).
- `ErrorPanel` là primitive ở `components/ui/ErrorPanel.tsx`, nhãn nút truyền
  qua prop `retryLabel`.

## 6. React

- `"use client"` cho mọi component dùng hook/state; giữ `app/` càng "server"
  càng tốt.
- Không `useMemo`/`useCallback` khi dependency đổi mỗi lần render — comment
  nếu cố tình bỏ.
- State form được **reset trong `useEffect` theo `open`** để mở lại modal
  không mang theo dữ liệu cũ.
- Thời gian: server trả ISO UTC; hiển thị bằng helper `timeAgo()` trong
  `lib/` của feature (vài giây trước / 5 phút / hôm qua), tooltip là thời
  gian đầy đủ theo múi giờ máy.
- Ngôn ngữ: **MVP chỉ tiếng Việt, không dựng tầng i18n.** Riêng nhãn theo
  enum (loại cảm xúc, trạng thái lời mời) dùng `Record<Union, string>` để
  thêm giá trị enum là lỗi biên dịch, không phải chuỗi thiếu lúc chạy. Cần
  đa ngôn ngữ thì thêm sau theo mẫu của Edumate (types + dict).

## 7. Lỗi

```ts
export function postErrorMessage(error: unknown): string {
  switch (httpStatusOf(error)) {
    case 400: return "Nội dung không hợp lệ.";
    case 403: return "Bạn không có quyền thực hiện thao tác này.";
    case 404: return "Bài viết không tồn tại hoặc đã bị xoá.";
    case 413: return "Ảnh vượt quá dung lượng cho phép (10MB).";
    default:  return "Có lỗi xảy ra, thử lại sau.";
  }
}
```

- Hiển thị bằng `toast.error(...)`; lỗi validate tại chỗ render dưới field kèm
  `aria-describedby`.
- Body lỗi của API là **ProblemDetails** (api.md mục 6): `httpStatusOf` đọc
  status, cần chi tiết hơn thì đọc `error.response.data.detail` — không parse
  chuỗi message.

## 8. Kiểm tra

```bash
make typecheck-web                       # tsc --noEmit
make lint-web                            # next lint
pnpm --dir socialmedia_web exec next lint --file <path>   # lint nhanh file vừa sửa
```
