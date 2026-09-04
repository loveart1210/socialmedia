# Quy trình truyền nhận dữ liệu Frontend ↔ Backend

Quy trình chuẩn truyền nhận dữ liệu giữa Frontend và Backend theo kiến trúc
Clean / 3-Tier Architecture, chuẩn hóa thành 7 bước.

## 1. Khởi tạo yêu cầu (Frontend)

- **Hành động:** Người dùng thao tác UI → FE thu thập dữ liệu, đóng gói theo cấu
  trúc **Request DTO** (định dạng JSON).
- **Truyền đi:** Gửi HTTP Request tới **API Endpoint** (URL + phương thức
  `GET` / `POST` / `PUT` / `DELETE`).
- **Cấu trúc gói tin:**
  - **Header:** chứa token xác thực — `Authorization: Bearer <JWT_Token>`.
  - **Body:** chứa chuỗi JSON của Request DTO.

## 2. Xác thực cổng vào (Backend Middleware)

- **Hành động:** Chặn HTTP Request ngay tại cổng vào Backend, trước khi tới
  Controller.
- **Xử lý:** Giải mã token ở header để xác thực danh tính (Authentication) và
  quyền hạn (Authorization).
- **Kết quả:**
  - Thất bại → trả về `401 Unauthorized` hoặc `403 Forbidden` và ngắt luồng.
  - Hợp lệ → cho phép request đi tiếp vào Controller.

## 3. Tiếp nhận & kiểm tra cấu trúc (Controller Layer)

- **Hành động:** Binding (ép chuỗi) JSON body thành đối tượng **Request DTO**
  trong code.
- **Validation:** Kiểm tra ràng buộc dữ liệu đầu vào (not null, định dạng email,
  độ dài chuỗi…).
- **Kết quả:**
  - Vi phạm → trả về `400 Bad Request` lập tức.
  - Hợp lệ → chuyển **Request DTO** xuống tầng Service.

## 4. Xử lý nghiệp vụ & dữ liệu (Service Layer)

- **Hành động:** Đọc dữ liệu từ **Request DTO** và thực thi luật nghiệp vụ
  (business rules).
- **Tương tác DB:** Gọi `DbContext` để truy vấn, thêm, sửa, xóa dữ liệu thô đại
  diện bằng **Entity** (đối tượng tương ứng với bảng trong database).

## 5. Ánh xạ dữ liệu (Data Mapping)

- **Hành động:** Lấy kết quả **Entity** từ database sau khi xử lý xong.
- **Mapping:** Sao chép các trường thông tin an toàn từ **Entity** sang
  **Response DTO** (lọc bỏ thông tin nhạy cảm như `PasswordHash`,
  `InternalKey`…).

## 6. Đóng gói & trả phản hồi (Controller Response)

- **Hành động:** Controller nhận **Response DTO** từ Service.
- **Đóng gói:** Bọc **Response DTO** kèm mã trạng thái HTTP chuẩn (`200 OK`,
  `201 Created`…) → chuyển thành chuỗi JSON và gửi về client.

## 7. Cập nhật giao diện (Frontend UI)

- **Hành động:** FE nhận phản hồi JSON từ Backend.
- **Xử lý:**
  - Thành công → cập nhật state và re-render giao diện (hiển thị thông báo,
    chuyển trang).
  - Thất bại → bắt mã lỗi HTTP và hiển thị cảnh báo tương ứng lên màn hình.

## Bảng tổng hợp kiến trúc base code

| Thành phần | Bản chất | Nhiệm vụ chính trong base code |
| :--- | :--- | :--- |
| **API Endpoint** | Contract / Routing | Định nghĩa đường dẫn URL và phương thức HTTP công khai |
| **Middleware** | Security Filter | Kiểm tra JWT token, xử lý ngoại lệ tập trung (global exception) |
| **Controller** | Entry Gate | Hứng DTO, validate đầu vào, đóng gói mã trạng thái HTTP |
| **Request DTO** | Data Holder | Khai báo cấu trúc dữ liệu client **được phép gửi lên** |
| **Service** | Business Logic | Bộ não chính: tính toán, kiểm tra quy tắc, gọi database |
| **Entity** | Data Model | Ánh xạ 1-1 với bảng trong database (dùng với ORM / EF Core) |
| **Response DTO** | Data Holder | Khai báo cấu trúc dữ liệu client **được phép nhìn thấy** |
