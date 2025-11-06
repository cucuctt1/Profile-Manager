using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ProfileManager.Profiles;

namespace ProfileManager.UI
{
    public sealed class ProfileEditorForm : Form
    {
        private readonly bool allowIdEdit;
        private NumericUpDown idInput = null!;
        private TextBox hoVaTenInput = null!;
        private DateTimePicker namSinhInput = null!;
        private TextBox noiSinhInput = null!;
        private TextBox queQuanInput = null!;
        private TextBox lopInput = null!;
        private TextBox tonGiaoInput = null!;
        private TextBox gioiTinhInput = null!;
        private TextBox nienKhoaInput = null!;
        private TextBox maBhytInput = null!;
        private TextBox maBhxhInput = null!;
        private TextBox diaChiInput = null!;
        private TextBox sdtInput = null!;
        private CheckBox vaoDoanInput = null!;
        private CheckBox vaoDangInput = null!;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProfileRecord Record { get; private set; }

        public ProfileEditorForm(ProfileRecord record, bool allowIdEdit)
        {
            Record = record;
            this.allowIdEdit = allowIdEdit;
            InitializeComponent();
            Load += OnLoad;
        }

        private void InitializeComponent()
        {
            Text = allowIdEdit ? "Add Profile" : "Edit Profile";
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Padding = new Padding(0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(20)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            FlowLayoutPanel CreateRow(string labelText, Control field)
            {
                var row = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = new Padding(0, layout.Controls.Count == 0 ? 0 : 10, 0, 0)
                };
                var label = new Label
                {
                    Text = labelText,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(0, 6, 16, 0),
                    MinimumSize = new Size(120, 0)
                };
                field.Margin = new Padding(0, 0, 0, 0);
                row.Controls.Add(label);
                row.Controls.Add(field);
                return row;
            }

            idInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 1000000,
                Width = 200,
                Enabled = allowIdEdit
            };
            hoVaTenInput = new TextBox { Width = 280 };
            namSinhInput = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Width = 160
            };
            noiSinhInput = new TextBox { Width = 280 };
            queQuanInput = new TextBox { Width = 280 };
            lopInput = new TextBox { Width = 160 };
            tonGiaoInput = new TextBox { Width = 160 };
            gioiTinhInput = new TextBox { Width = 160 };
            nienKhoaInput = new TextBox { Width = 160 };
            maBhytInput = new TextBox { Width = 200 };
            maBhxhInput = new TextBox { Width = 200 };
            diaChiInput = new TextBox { Width = 300 };
            sdtInput = new TextBox { Width = 180 };
            vaoDoanInput = new CheckBox { AutoSize = true };
            vaoDangInput = new CheckBox { AutoSize = true };

            layout.Controls.Add(CreateRow("ID", idInput));
            layout.Controls.Add(CreateRow("Họ và tên", hoVaTenInput));
            layout.Controls.Add(CreateRow("Năm sinh", namSinhInput));
            layout.Controls.Add(CreateRow("Nơi sinh", noiSinhInput));
            layout.Controls.Add(CreateRow("Quê quán", queQuanInput));
            layout.Controls.Add(CreateRow("Lớp", lopInput));
            layout.Controls.Add(CreateRow("Tôn giáo", tonGiaoInput));
            layout.Controls.Add(CreateRow("Giới tính", gioiTinhInput));
            layout.Controls.Add(CreateRow("Niên khóa", nienKhoaInput));
            layout.Controls.Add(CreateRow("Mã BHYT", maBhytInput));
            layout.Controls.Add(CreateRow("Mã BHXH", maBhxhInput));
            layout.Controls.Add(CreateRow("Địa chỉ", diaChiInput));
            layout.Controls.Add(CreateRow("SĐT", sdtInput));
            layout.Controls.Add(CreateRow("Vào đoàn", vaoDoanInput));
            layout.Controls.Add(CreateRow("Vào đảng", vaoDangInput));

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(20, 0, 20, 20),
                AutoSize = true
            };

            var saveButton = new Button { Text = "Save", AutoSize = true };
            saveButton.Click += (_, _) => TrySave();
            var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            Controls.Add(layout);
            Controls.Add(buttonPanel);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            idInput.Value = Record.Id < 1 ? 1 : Record.Id;
            hoVaTenInput.Text = Record.HoVaTen ?? string.Empty;
            var dob = Record.NamSinh == default ? DateTime.UtcNow.AddYears(-20) : Record.NamSinh;
            if (dob.Kind == DateTimeKind.Utc) dob = dob.ToLocalTime();
            namSinhInput.Value = dob;
            noiSinhInput.Text = Record.NoiSinh ?? string.Empty;
            queQuanInput.Text = Record.QueQuan ?? string.Empty;
            lopInput.Text = Record.Lop ?? string.Empty;
            tonGiaoInput.Text = Record.TonGiao ?? string.Empty;
            gioiTinhInput.Text = Record.GioiTinh ?? string.Empty;
            nienKhoaInput.Text = Record.NienKhoa ?? string.Empty;
            maBhytInput.Text = Record.MaSoBhyt ?? string.Empty;
            maBhxhInput.Text = Record.MaSoBhxh ?? string.Empty;
            diaChiInput.Text = Record.DiaChi ?? string.Empty;
            sdtInput.Text = Record.Sdt ?? string.Empty;
            vaoDoanInput.Checked = Record.VaoDoan;
            vaoDangInput.Checked = Record.VaoDang;
        }

        private void TrySave()
        {
            var name = hoVaTenInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "Họ và tên là bắt buộc.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                hoVaTenInput.Focus();
                return;
            }
            var dobLocal = namSinhInput.Value.Date;
            var dobUtc = DateTime.SpecifyKind(dobLocal, DateTimeKind.Local).ToUniversalTime();
            Record = new ProfileRecord
            {
                Id = (int)idInput.Value,
                HoVaTen = name,
                NamSinh = dobUtc,
                NoiSinh = string.IsNullOrWhiteSpace(noiSinhInput.Text) ? null : noiSinhInput.Text.Trim(),
                QueQuan = string.IsNullOrWhiteSpace(queQuanInput.Text) ? null : queQuanInput.Text.Trim(),
                Lop = string.IsNullOrWhiteSpace(lopInput.Text) ? null : lopInput.Text.Trim(),
                TonGiao = string.IsNullOrWhiteSpace(tonGiaoInput.Text) ? null : tonGiaoInput.Text.Trim(),
                GioiTinh = string.IsNullOrWhiteSpace(gioiTinhInput.Text) ? null : gioiTinhInput.Text.Trim(),
                NienKhoa = string.IsNullOrWhiteSpace(nienKhoaInput.Text) ? null : nienKhoaInput.Text.Trim(),
                MaSoBhyt = string.IsNullOrWhiteSpace(maBhytInput.Text) ? null : maBhytInput.Text.Trim(),
                MaSoBhxh = string.IsNullOrWhiteSpace(maBhxhInput.Text) ? null : maBhxhInput.Text.Trim(),
                DiaChi = string.IsNullOrWhiteSpace(diaChiInput.Text) ? null : diaChiInput.Text.Trim(),
                Sdt = string.IsNullOrWhiteSpace(sdtInput.Text) ? null : sdtInput.Text.Trim(),
                VaoDoan = vaoDoanInput.Checked,
                VaoDang = vaoDangInput.Checked
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
