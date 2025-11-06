using System;

namespace ProfileManager.Profiles
{
    public static class ProfileDataSeeder
    {
    private static readonly string[] Ho = { "Nguyen", "Tran", "Le", "Pham", "Hoang", "Phan", "Vu", "Vo", "Dang", "Bui" };
    private static readonly string[] Ten = { "An", "Binh", "Chi", "Dung", "Giang", "Hanh", "Khanh", "Linh", "Minh", "Phong", "Quyen", "Trang" };
    private static readonly string[] NoiSinh = { "Ha Noi", "Da Nang", "Ho Chi Minh", "Hai Phong", "Can Tho", "Hue" };
    private static readonly string[] Lop = { "12A1", "12A2", "11B1", "11B2", "10C1", "10C2" };
    private static readonly string[] TonGiao = { "Khong", "Phat giao", "Cong giao", "Cao Dai" };
    private static readonly string[] GioiTinh = { "Nam", "Nu" };
    private static readonly string[] NienKhoa = { "2023-2027", "2022-2026", "2021-2025", "2020-2024" };
    private static readonly string[] DiaChi = { "Quan 1", "Quan 3", "Quan 7", "Thanh Xuan", "Ha Dong", "Son Tra" };

        public static void Seed(ProfileRepository repository, int count)
        {
            if (count <= 0) return;
            var random = new Random();
            var nextId = repository.GetNextId();
            for (var i = 0; i < count; i++)
            {
                var ho = Ho[random.Next(Ho.Length)];
                var ten = Ten[random.Next(Ten.Length)];
                var name = string.Concat(ho, " ", ten);
                var year = DateTime.UtcNow.Year - random.Next(18, 25);
                var dob = new DateTime(year, random.Next(1, 13), random.Next(1, 28));
                var noiSinh = NoiSinh[random.Next(NoiSinh.Length)];
                var queQuan = NoiSinh[random.Next(NoiSinh.Length)];
                var lop = Lop[random.Next(Lop.Length)];
                var tonGiao = TonGiao[random.Next(TonGiao.Length)];
                var gioiTinh = GioiTinh[random.Next(GioiTinh.Length)];
                var nienKhoa = NienKhoa[random.Next(NienKhoa.Length)];
                var bhyt = string.Concat("BHYT", random.Next(100000, 999999).ToString());
                var bhxh = string.Concat("BHXH", random.Next(100000, 999999).ToString());
                var address = DiaChi[random.Next(DiaChi.Length)];
                var phone = string.Concat("09", random.Next(1000000, 9999999).ToString());
                var vaoDoan = random.NextDouble() > 0.4;
                var vaoDang = vaoDoan && random.NextDouble() > 0.7;
                var record = new ProfileRecord
                {
                    Id = nextId++,
                    HoVaTen = name,
                    NamSinh = dob,
                    NoiSinh = noiSinh,
                    QueQuan = queQuan,
                    Lop = lop,
                    TonGiao = tonGiao,
                    GioiTinh = gioiTinh,
                    NienKhoa = nienKhoa,
                    MaSoBhyt = bhyt,
                    MaSoBhxh = bhxh,
                    DiaChi = address,
                    Sdt = phone,
                    VaoDoan = vaoDoan,
                    VaoDang = vaoDang
                };
                repository.Add(record);
            }
        }
    }
}
