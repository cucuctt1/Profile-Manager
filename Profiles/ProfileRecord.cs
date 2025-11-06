using System;

namespace ProfileManager.Profiles
{
    public sealed class ProfileRecord
    {
        public int Id { get; set; }
        public string? HoVaTen { get; set; }
        public DateTime NamSinh { get; set; }
        public string? NoiSinh { get; set; }
        public string? QueQuan { get; set; }
        public string? Lop { get; set; }
        public string? TonGiao { get; set; }
        public string? GioiTinh { get; set; }
        public string? NienKhoa { get; set; }
        public string? MaSoBhyt { get; set; }
        public string? MaSoBhxh { get; set; }
        public string? DiaChi { get; set; }
        public string? Sdt { get; set; }
        public bool VaoDoan { get; set; }
        public bool VaoDang { get; set; }
    }
}
