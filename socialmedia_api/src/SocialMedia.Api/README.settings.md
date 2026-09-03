# Cấu hình bắt buộc của SocialMedia.Api

`appsettings.json` cố ý **không** khai các khóa dưới đây: khai sẵn giá trị rỗng ở đó
sẽ che mất giá trị mà môi trường (biến môi trường ở prod, `UseSetting` trong test)
truyền vào — nguồn nào ghi sau trong chuỗi cấu hình thì thắng, và `appsettings.json`
được nạp sau host configuration.

Thiếu khóa nào thì app dừng ngay lúc khởi động kèm thông báo rõ (`ValidateOnStart`).

| Khóa | Ví dụ | Ghi chú |
|---|---|---|
| `ConnectionStrings:Postgres` | `Host=localhost;Port=5432;Database=socialmedia;Username=socialmedia;Password=...` | local lấy từ `docker-compose.yml` |
| `ConnectionStrings:Redis` | `localhost:6379` | |
| `Jwt:Issuer` | `socialmedia-api` | |
| `Jwt:Audience` | `socialmedia-web` | |
| `Jwt:SecretKey` | chuỗi ≥ 32 byte | **secret** — prod đặt bằng biến môi trường `Jwt__SecretKey` |
| `Jwt:AccessTokenMinutes` | `15` | mặc định 15 (SPEC mục 4) |
| `Jwt:RefreshTokenDays` | `14` | |
| `Cors:AllowedOrigins` | `["http://localhost:3000"]` | origin của `socialmedia_web` |

Local: `appsettings.Development.json` (đã có sẵn giá trị trỏ về docker-compose).
Prod: biến môi trường qua Dokploy, không commit secret vào repo.
