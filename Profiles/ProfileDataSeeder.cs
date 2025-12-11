using System;


// tạo ngẫu nhiên dữ liệu dummy


namespace ProfileManager.Profiles
{
    public static class ProfileDataSeeder
    {
        //setup dummy data
        private static readonly string[] Ho = { "Nguyen", "Tran", "Le", "Pham", "Hoang", "Phan", "Vu", "Vo", "Dang", "Bui" };
        private static readonly string[] Ten = { "An", "Binh", "Chi", "Dung", "Giang", "Hanh", "Khanh", "Linh", "Minh", "Phong", "Quyen", "Trang" };
        private static readonly string[] NoiSinh = { "Ha Noi", "Da Nang", "Ho Chi Minh", "Hai Phong", "Can Tho", "Hue" };
        private static readonly string[] Lop = { "12A1", "12A2", "11B1", "11B2", "10C1", "10C2" };
        private static readonly string[] TonGiao = { "Khong", "Phat giao", "Cong giao", "Cao Dai" };
        private static readonly string[] GioiTinh = { "Nam", "Nu" };
        private static readonly string[] NienKhoa = { "2023-2027", "2022-2026", "2021-2025", "2020-2024" };
        private static readonly string[] DiaChi = { "Quan 1", "Quan 3", "Quan 7", "Thanh Xuan", "Ha Dong", "Son Tra" };
        ///
        public static void Seed(ProfileRepository repository, int count, int batchSize = 5000)
        {
            if (count <= 0) return;
            if (batchSize <= 0) batchSize = 5000;

            var random = new Random();
            var nextId = repository.GetNextId(); // lay id
            var buffer = new List<ProfileRecord>(batchSize);

            void Flush()
            {
                if (buffer.Count == 0) return;
                repository.AddRange(buffer);
                buffer.Clear();
            }

            for (var i = 0; i < count; i++)
            {
                var ho = Ho[random.Next(Ho.Length)];
                var ten = Ten[random.Next(Ten.Length)];
                var name = string.Concat(ho, " ", ten); // tao ho va ten

                var year = DateTime.UtcNow.Year - random.Next(18, 25); // tuoi
                var dob = new DateTime(year, random.Next(1, 13), random.Next(1, 28));   // nam sinh

                var noiSinh = NoiSinh[random.Next(NoiSinh.Length)]; // noi sinh
                var quequan = NoiSinh[random.Next(NoiSinh.Length)]; // que quan
                var lop = Lop[random.Next(Lop.Length)]; // lop
                var tonGiao = TonGiao[random.Next(TonGiao.Length)]; // ton giao
                var gioiTinh = GioiTinh[random.Next(GioiTinh.Length)]; // gioi tinh
                var nienkhoa = NienKhoa[random.Next(NienKhoa.Length)]; // nien khoa
                var bhyt = string.Concat("BHYT", random.Next(100000, 999999).ToString()); // random ma so bhyt  
                var bhxh = string.Concat("BHXH", random.Next(100000, 999999).ToString()); // random ma so bhxh
                var address = DiaChi[random.Next(DiaChi.Length)]; // random dia chi
                var sdt = string.Concat("09", random.Next(1000000, 9999999).ToString()); // random so dien thoai
                var vaoDoan = random.NextDouble() > 0.4; // random vao doan
                var vaoDang = vaoDoan && random.NextDouble() > 0.7; // random vao dang

                var record = new ProfileRecord
                {
                    Id = nextId++,
                    HoVaTen = name,
                    NamSinh = dob,
                    NoiSinh = noiSinh,
                    QueQuan = quequan,
                    Lop = lop,
                    TonGiao = tonGiao,
                    GioiTinh = gioiTinh,
                    NienKhoa = nienkhoa,
                    MaSoBhyt = bhyt,
                    MaSoBhxh = bhxh,
                    DiaChi = address,
                    Sdt = sdt,
                    VaoDoan = vaoDoan,
                    VaoDang = vaoDang
                };

                buffer.Add(record);
                if (buffer.Count >= batchSize) Flush();
            }

            Flush();
        }
    }
}
