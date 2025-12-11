# CHƯƠNG 4: CÀI ĐẶT CHƯƠNG TRÌNH

Tài liệu này trình bày chi tiết cấu trúc và cách vận hành của ứng dụng quản lý hồ sơ học sinh theo dạng báo cáo học thuật. Mỗi mục liên kết trực tiếp tới tệp mã nguồn để tra cứu và mô tả rõ dữ liệu, thuật toán, luồng xử lý.

## 4.1 Quy hoạch dữ liệu (Schema Construct)
- Thành phần: [base/FileIO/SchemaConstruct.cs](base/FileIO/SchemaConstruct.cs)
- Cách hoạt động: Khai báo `FieldType` (int, string, bool, datetime, blob), lớp `Field` (tên/kiểu/độ dài) và `Schema` (danh sách cột). Hỗ trợ: (1) Parse chuỗi `name:type[:maxLen]` thành schema, (2) tạo từ mảng chuỗi, (3) xuất ngược thành chuỗi để lưu metadata. Đây là nguồn sự thật cho mọi thao tác IO và chỉ mục.
- Ghi chú triển khai: `ToString()` bảo toàn thứ tự cột; trường string có `MaxLength` được pad cố định, blob được đánh dấu để xử lý tệp phụ; mọi thao tác so sánh kiểu dùng switch rõ ràng, tránh reflection để nhẹ và an toàn kiểu.

## 4.2 Quy tắc lưu trữ dữ liệu (Schema Instruction)
- Thành phần: [base/FileIO/SchemaInstruction.cs](base/FileIO/SchemaInstruction.cs)
- Cách hoạt động: Tính offset byte từng trường, tổng kích thước bản ghi và sinh `ByteRule` (BitArray) mô tả padding/ranh giới. Quy tắc này đảm bảo đọc/ghi cố định độ rộng, giảm phân mảnh và hỗ trợ truy cập ngẫu nhiên.
- Ghi chú triển khai: Kích thước chuỗi mặc định 50 ký tự nếu không khai báo; datetime lưu ticks (8 byte); blob cố định 128 ký tự (nhị phân hóa trong DataTypeConv). `ByteRule` được dùng cho cả đọc lẫn ghi để cắt bỏ padding thừa khi cần.

## 4.3 Hướng dẫn đọc/ghi metadata (MetaWriter)
- Thành phần: [base/FileIO/MetaWriter.cs](base/FileIO/MetaWriter.cs)
- Cách hoạt động: Ghi `metadata.meta` gồm (1) dòng schema text, (2) dòng quy tắc bit ở dạng hex (qua EDHex). File này cho phép tái dựng schema và hướng dẫn đọc/ghi mà không cần hard-code trong mã.
- Ghi chú triển khai: Metadata được ghi bằng `StreamWriter` UTF-8, mỗi bảng có thư mục riêng chứa `metadata.meta` và `data.bin`; catalog `__catalog` cũng dùng cùng định dạng để liệt kê các bảng người dùng.

## 4.4 Quản lý đọc/ghi dữ liệu (FileIOManager)
- Thành phần: [base/FileIO/FileIOManager.cs](base/FileIO/FileIOManager.cs)
- Cách hoạt động:
	1) Đọc metadata → chuyển hex rule về `BitArray` → đếm số trường.
	2) Ghi bản ghi: tuần tự từng trường, ghi độ dài (Int32) rồi dữ liệu; hỗ trợ ghi đơn và nhiều bản ghi liên tục.
	3) Đọc bản ghi: tìm offset theo chỉ số, đọc length-prefixed fields, giải mã theo `Schema`.
	4) Sửa: append bản ghi mới, sau đó xóa bản ghi cũ bằng dịch chuyển byte (compaction).
	5) Xóa: tìm offset bản ghi và compact tệp.
- Ghi chú triển khai: Định dạng length-prefixed giúp bỏ qua trường nhanh bằng seek; kiểm tra cắt cụt (truncated) để dừng khi tệp lỗi; `TryGetRecordOffsets` duyệt một lần để định vị bản ghi cần đọc/xóa, tránh đọc toàn bộ.

## 4.5 Công cụ hỗ trợ đọc/ghi (utils)
### 4.5.1 Quy định kho chứa bit padding (BitSchemaPad)
- Thành phần: [base/FileIO/utils/BitSchemaPad.cs](base/FileIO/utils/BitSchemaPad.cs)
- Cách hoạt động: Tạo `BitArray` đánh dấu biên trường dựa trên offsets và kích thước bản ghi; cắt bỏ padding thừa; chuyển đổi sang/ra hex qua EDHex để lưu trong metadata.
- Ghi chú triển khai: `GenerateBitPadding` nhận offsets và tổng kích thước, đặt bit 0 tại biên trường để phép đếm bit suy ra số cột; `BitTrim` loại bỏ bit đệm cuối để khớp độ dài thực tế.

### 4.5.2 Chuyển đổi kiểu dữ liệu (DataTypeConv)
- Thành phần: [base/FileIO/utils/DataTypeConv.cs](base/FileIO/utils/DataTypeConv.cs)
- Cách hoạt động: Object <-> byte cho int/bool/datetime ticks/string (pad 0 theo maxLen)/blob. Đảm bảo tuần tự hóa nhất quán giữa lưu trữ và truy vấn.
- Ghi chú triển khai: String mã hóa UTF-8, pad bằng 0 tới maxLen; bool lưu 1 byte (`0x00`/`0x01`); datetime dùng `BitConverter.GetBytes(long ticks)`; blob là byte[] được giữ nguyên.

### 4.5.3 Chuyển đổi cơ số (EDHex)
- Thành phần: [base/FileIO/utils/EDHex.cs](base/FileIO/utils/EDHex.cs)
- Cách hoạt động: Mã hóa/giải mã `BitArray` thành chuỗi hex để nhúng vào metadata; giúp tệp meta ngắn gọn, dễ đọc.
- Ghi chú triển khai: Duyệt bit theo nhóm 4 để tạo hex; đảo chiều đúng thứ tự bit cao/thấp; dùng `StringBuilder` để giảm cấp phát.

## 4.6 Cây nhị phân (Binary Search Tree)
- Thành phần: [base/Index/BinarySearchTree.cs](base/Index/BinarySearchTree.cs)
- Cách hoạt động: Cây tìm kiếm nhị phân lưu khóa chuỗi và danh sách chỉ số bản ghi. Hỗ trợ chèn, xóa, tìm kiếm tiền tố, tìm kiếm phạm vi, duyệt inorder, và dựng lại cân bằng từ danh sách đã sắp xếp. Độ phức tạp trung bình O(log n); xấu nhất O(n) nếu lệch cân bằng.
- Ghi chú triển khai: `RebuildBalancedFrom` nhận danh sách đã sắp xếp để tạo cây gần cân bằng; `traverse()` trả về IEnumerable<KeyValuePair<string,List<int>>> inorder phục vụ `SearchTopK`.

## 4.7 Sáu thuật toán tìm kiếm (IndexManager)
- Thành phần: [base/Index/IndexManager.cs](base/Index/IndexManager.cs)
- Cách hoạt động: Đọc toàn bộ bảng, xây một BST cho trường được chọn và lưu trong bộ nhớ. Cung cấp:
	- `SearchExact`: khớp khóa chính xác.
	- `SearchPrefix`: khớp tiền tố.
	- `SearchRange` / `SearchRangeParallel`: truy vấn khoảng; bản song song chia đoạn khóa.
	- `SearchGreaterThan` / `SearchLessThan`: biên dưới/trên (tùy inclusive).
	- `SearchTopK`: lấy K chỉ số đầu theo thứ tự khóa (tăng/giảm).
	- `DropIndex/DropAll`: làm mới chỉ mục khi dữ liệu đổi.
- Ghi chú triển khai: Mỗi trường lập chỉ mục là một BST riêng lưu trong `indexes[fieldName]`; dữ liệu nằm trong bộ nhớ (không persist), cần rebuild sau khi ghi/xóa. `SearchRangeParallel` phân mảnh khóa và hợp nhất kết quả theo thứ tự.

## 4.8 Quản lý hành vi bảng (TableManager)
- Thành phần: [base/Table/TableManager.cs](base/Table/TableManager.cs)
- Cách hoạt động:
	- Tạo bảng: sinh thư mục, metadata, data, cập nhật catalog `__catalog`.
	- CRUD: chèn/cập nhật/xóa bản ghi; với blob thì quản lý thư mục `blobs` kèm đường dẫn.
	- Chỉ mục: đánh dấu “dirty” sau ghi/xóa; `EnsureIndex` sẽ gọi `IndexManager.BuildIndex` khi cần.
	- Truy vấn: chuyển request từ repository thành danh sách chỉ số hoặc bản ghi; hỗ trợ exact/prefix/range/topK.
	- Quản lý catalog: lưu siêu dữ liệu về bảng (tên, schema, đường dẫn) để khởi động lại dễ dàng.
- Ghi chú triển khai: Bảng catalog dùng chính TableManager để tự mô tả; `BuildIndex` gọi `IndexManager.BuildIndex` theo tên trường; blob lưu file riêng, còn trong record chỉ giữ tên file để tránh phình to bản ghi.

## 4.9 Kiểm tra độ hoàn thiện bảng (TableDiagnostics)
- Thành phần: [base/Table/TableDiagnostics.cs](base/Table/TableDiagnostics.cs)
- Cách hoạt động: Đọc metadata đối chiếu schema, quét dữ liệu, kiểm tra blob mồ côi/thiếu, dựng lại chỉ mục để đo thời gian và phát hiện lỗi cảnh báo. In báo cáo chi tiết (OK, ERROR, WARN) cho từng bảng.
- Ghi chú triển khai: So khớp dòng schema text với `Schema.ToString()`, kiểm tra rowcount thực tế vs metadata, rebuild index để phát hiện khóa mẫu thiếu, và kiểm tra orphan/missing blob qua danh sách file trong thư mục `blobs`.

## 4.10 Tạo dữ liệu mẫu (ProfileDataSeeder)
- Thành phần: [Profiles/ProfileDataSeeder.cs](Profiles/ProfileDataSeeder.cs)
- Cách hoạt động: Sinh ngẫu nhiên tên, quê quán, lớp, ngày sinh, mã BHYT/BHXH, cờ Đoàn/Đảng; ghi theo batch để giảm số lần IO; hỗ trợ tham số số lượng và batch size cho benchmark.
- Ghi chú triển khai: Dùng `Random` với seed thời gian; buffer batch trước khi flush vào `TableManager.InsertRecords`; phân phối lớp/tỉnh theo danh sách mẫu để dữ liệu trông tự nhiên hơn.

## 4.11 Định dạng kiểu dữ liệu hồ sơ (ProfileRecord)
- Thành phần: [Profiles/ProfileRecord.cs](Profiles/ProfileRecord.cs)
- Cách hoạt động: Lớp POCO chứa 15 trường; dùng cho binding UI và chuyển đổi sang mảng `object[]` trước khi ghi file; giữ nguyên kiểu mạnh (int, string, DateTime, bool) để tránh lỗi ép kiểu.
- Ghi chú triển khai: `ProfileRow` (record struct) đóng gói `Index + ProfileRecord` phục vụ hiển thị; việc chuyển đổi qua `ProfileRepository.ToValues` bảo đảm thứ tự cột khớp schema.

## 4.12 Thực thi thuật toán truy vấn (ProfileRepository)
- Thành phần: [Profiles/ProfileRepository.cs](Profiles/ProfileRepository.cs)
- Cách hoạt động: Bao bọc `TableManager`, chuẩn hóa tên trường (case-insensitive), đảm bảo index tồn tại khi tìm kiếm exact/range, cung cấp:
	- CRUD: Add/Update/Delete/WipeAll.
	- Tìm kiếm: exact (dựa index), partial (quét chuỗi), range (tuần tự/song song), lọc ngày.
	- Dựng lại bảng nếu schema lệch; tự động build index khi thiếu.
	- Trả về `ProfileRow` (chỉ số + bản ghi) để UI hiển thị nhanh.
- Ghi chú triển khai: `EnsureIndex` bỏ qua `namsinh` (date) để tránh index không cần thiết; tìm kiếm partial dùng chuỗi thường (`ToLowerInvariant`) và so sánh chứa; `SearchRangeParallelIds` trả về danh sách index để UI đọc thêm khi cần.

## 4.13 Giao diện chính quản lý hồ sơ (MainForm)
- Thành phần: [UI/MainForm.cs](UI/MainForm.cs)
- Cách hoạt động: WinForms chính với lưới dữ liệu, hộp tìm kiếm (exact/partial), nút CRUD, Seed/Wipe, mở `ProfileEditorForm`. Kết quả tìm kiếm hiển thị tức thời nhờ truy vấn index hoặc quét dữ liệu.
- Ghi chú triển khai: Sử dụng `BindingSource` để refresh lưới; các nút gọi repository tương ứng; seed/wipe gắn với `ProfileDataSeeder` và `WipeAll`; hộp thoại editor bật modal để tránh race.

## 4.14 Giao diện chỉnh sửa bản ghi (ProfileEditorForm)
- Thành phần: [UI/ProfileEditorForm.cs](UI/ProfileEditorForm.cs)
- Cách hoạt động: Form nhập/sửa một hồ sơ, ràng buộc giá trị, kiểm soát cho phép sửa ID (tùy chế độ), chuẩn hóa định dạng ngày/số điện thoại, trả về `ProfileRecord` cho repository xử lý.
- Ghi chú triển khai: Validate độ dài chuỗi theo schema, parse ngày bằng `DateTime.TryParse`, chuẩn hóa số điện thoại bỏ khoảng trắng; khi lưu trả về record mới để MainForm cập nhật.

## 4.15 Đo thời gian thực thi tìm kiếm (RangeScanBenchmark)
- Thành phần: [Benchmarks/RangeScanBenchmark.cs](Benchmarks/RangeScanBenchmark.cs)
- Cách hoạt động: Tạo dữ liệu mẫu kích thước 1k/10k/100k, chạy quét phạm vi tuần tự và song song, đo thời gian trung bình; kích hoạt bằng `--bench` trong `Program.cs`.
- Ghi chú triển khai: Dùng `Stopwatch` đo lặp lại nhiều lần, tính trung bình; phạm vi khóa được chọn theo các giá trị chuỗi sinh từ seeder; kết quả in ra console qua `ConsoleHelper.EnsureConsole` để xem trong chế độ `--bench`.
