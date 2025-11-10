using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ProfileManager.Profiles;

namespace ProfileManager.UI
{
    public sealed class MainForm : Form
    {
        private readonly ProfileRepository repository;
        private readonly BindingList<ProfileViewRow> viewRows = new();
        private readonly BindingSource bindingSource = new();
        private readonly string dataDirectory;
        private DataGridView grid = null!;
        private ComboBox fieldCombo = null!;
        private TextBox searchBox = null!;
        private CheckBox exactCheck = null!;
        private DateTimePicker fromPicker = null!;
        private DateTimePicker toPicker = null!;
        private Panel overlayPanel = null!;
        private Panel overlayContent = null!;
        private FlowLayoutPanel overlayFlow = null!;

        public MainForm()
        {
            dataDirectory = Path.Combine(AppContext.BaseDirectory, "profile_data");
            Directory.CreateDirectory(dataDirectory);
            repository = new ProfileRepository(dataDirectory);
            InitializeComponent();
            Load += OnLoaded;
        }

        private void InitializeComponent()
        {
            Text = "Profile Manager";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 640);

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 12, 12, 12)
            };

            fieldCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140,
                Margin = new Padding(0, 0, 12, 8)
            };
            fieldCombo.Items.AddRange(new object[] { "HoVaTen", "NamSinh", "NoiSinh", "QueQuan", "Lop", "TonGiao", "GioiTinh", "NienKhoa", "MaSoBhyt", "MaSoBhxh", "DiaChi", "Sdt", "VaoDoan", "VaoDang", "Id" });
            fieldCombo.SelectedIndex = 0;

            searchBox = new TextBox { Width = 220, Margin = new Padding(0, 0, 12, 8) };
            exactCheck = new CheckBox { Text = "Exact", AutoSize = true, Margin = new Padding(0, 4, 12, 8) };

            var searchButton = new Button { Text = "Search", AutoSize = true, Margin = new Padding(0, 0, 12, 8) };
            searchButton.Click += (_, _) => PerformSearch();

            var clearButton = new Button { Text = "Clear", AutoSize = true, Margin = new Padding(0, 0, 12, 8) };
            clearButton.Click += (_, _) => ClearSearch();

            fromPicker = new DateTimePicker
            {
                Width = 160,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Margin = new Padding(0, 0, 12, 8)
            };

            toPicker = new DateTimePicker
            {
                Width = 160,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Margin = new Padding(0, 0, 12, 8)
            };

            var rangeButton = new Button { Text = "Date Range", AutoSize = true, Margin = new Padding(0, 0, 12, 8) };
            rangeButton.Click += (_, _) => ApplyDateRange();

            var addButton = new Button { Text = "Add", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
            addButton.Click += (_, _) => AddRecord();

            var editButton = new Button { Text = "Edit", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
            editButton.Click += (_, _) => EditRecord();

            var deleteButton = new Button { Text = "Delete", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
            deleteButton.Click += (_, _) => DeleteRecord();

            var refreshButton = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(0, 0, 12, 8) };
            refreshButton.Click += (_, _) => ReloadAll();


            //unused
            // var overlayToggle = new Button { Text = "Templates", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            // overlayToggle.Click += (_, _) => ToggleOverlay();

            var debugGroup = new GroupBox
            {
                Text = "Debug",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 14, 10, 10),
                Margin = new Padding(8, 0, 12, 8)
            };
            var debugLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0)
            };
            var seedButton = new Button { Text = "Seed 50", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            seedButton.Click += (_, _) => SeedData();
            var wipeButton = new Button { Text = "Wipe", AutoSize = true };
            wipeButton.Click += (_, _) => WipeAll();
            // them compo va debug compo 
            debugLayout.Controls.Add(seedButton);
            debugLayout.Controls.Add(wipeButton);
            debugGroup.Controls.Add(debugLayout);

            topPanel.Controls.Add(fieldCombo);
            topPanel.Controls.Add(searchBox);
            topPanel.Controls.Add(exactCheck);
            topPanel.Controls.Add(searchButton);
            topPanel.Controls.Add(clearButton);
            topPanel.Controls.Add(fromPicker);
            topPanel.Controls.Add(toPicker);
            topPanel.Controls.Add(rangeButton);
            topPanel.Controls.Add(addButton);
            topPanel.Controls.Add(editButton);
            topPanel.Controls.Add(deleteButton);
            topPanel.Controls.Add(refreshButton);
            topPanel.Controls.Add(debugGroup);
            //topPanel.Controls.Add(overlayToggle);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Both
            };
            grid.CellDoubleClick += (_, _) => EditRecord();
            //////////////////////////////////////////////////// setup columns ////////////////////////////////////////////////////
            // spam de
            var indexColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ProfileViewRow.Index),
                Visible = false
            };
            var idColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = nameof(ProfileViewRow.Id),
                Width = 80
            };
            var nameColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Họ và tên",
                DataPropertyName = nameof(ProfileViewRow.HoVaTen),
                Width = 200
            };
            var emailColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Năm sinh",
                DataPropertyName = nameof(ProfileViewRow.NamSinh),
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
            };
            var roleColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Nơi sinh",
                DataPropertyName = nameof(ProfileViewRow.NoiSinh),
                Width = 180
            };
            var createdColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Quê quán",
                DataPropertyName = nameof(ProfileViewRow.QueQuan),
                Width = 180
            };
            var lopColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lớp",
                DataPropertyName = nameof(ProfileViewRow.Lop),
                Width = 100
            };
            var tonGiaoColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tôn giáo",
                DataPropertyName = nameof(ProfileViewRow.TonGiao),
                Width = 120
            };
            var gioiTinhColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Giới tính",
                DataPropertyName = nameof(ProfileViewRow.GioiTinh),
                Width = 100
            };
            var nienKhoaColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Niên khóa",
                DataPropertyName = nameof(ProfileViewRow.NienKhoa),
                Width = 140
            };
            var bhytColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã BHYT",
                DataPropertyName = nameof(ProfileViewRow.MaSoBhyt),
                Width = 140
            };
            var bhxhColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã BHXH",
                DataPropertyName = nameof(ProfileViewRow.MaSoBhxh),
                Width = 140
            };
            var diaChiColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Địa chỉ",
                DataPropertyName = nameof(ProfileViewRow.DiaChi),
                Width = 200
            };
            var sdtColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "SĐT",
                DataPropertyName = nameof(ProfileViewRow.Sdt),
                Width = 120
            };
            var vaoDoanColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Vào đoàn",
                DataPropertyName = nameof(ProfileViewRow.VaoDoan),
                Width = 90
            };
            var vaoDangColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Vào đảng",
                DataPropertyName = nameof(ProfileViewRow.VaoDang),
                Width = 90
            };
            ///////////////////////////////////////////////////////////////////////////////////////////////////
            grid.Columns.AddRange(indexColumn, idColumn, nameColumn, emailColumn, roleColumn, createdColumn, lopColumn, tonGiaoColumn, gioiTinhColumn, nienKhoaColumn, bhytColumn, bhxhColumn, diaChiColumn, sdtColumn, vaoDoanColumn, vaoDangColumn);
            bindingSource.DataSource = viewRows;
            grid.DataSource = bindingSource;

            overlayPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(220, 32, 32, 32)
            };
            overlayContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(32),
                AutoScroll = true
            };
            overlayContent.Resize += (_, _) => AdjustOverlayCards();
            var overlayContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 245, 250)
            };
            var overlayHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(16),
                WrapContents = false,
                BackColor = Color.FromArgb(240, 240, 240)
            };
            var overlayLabel = new Label
            {
                Text = "Dynamic Templates",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold)
            };
            var overlayClose = new Button
            {
                Text = "Close",
                AutoSize = true
            };
            overlayClose.Click += (_, _) => HideOverlay();
            overlayHeader.Controls.Add(overlayLabel);
            overlayHeader.Controls.Add(overlayClose);
            overlayContainer.Controls.Add(overlayContent);
            overlayContainer.Controls.Add(overlayHeader);
            overlayPanel.Controls.Add(overlayContainer);

            Controls.Add(overlayPanel);
            Controls.Add(grid);
            Controls.Add(topPanel);
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            ReloadAll();
        }

        private void ReloadAll()
        {
            BindRows(repository.GetAll());
        }

        private void PerformSearch()
        {
            var field = fieldCombo.SelectedItem?.ToString() ?? "HoVaTen";
            var value = searchBox.Text.Trim();
            var exact = exactCheck.Checked;
            var rows = repository.Search(field, value, exact);
            BindRows(rows);
        }

        private void ClearSearch()
        {
            searchBox.Text = string.Empty;
            exactCheck.Checked = false;
            fieldCombo.SelectedIndex = 0;
            ReloadAll();
        }

        private void ApplyDateRange()
        {
            DateTime? from = fromPicker.Checked ? fromPicker.Value.Date : null;
            DateTime? to = toPicker.Checked ? toPicker.Value.Date : null;
            var rows = repository.FilterByDate(from, to);
            BindRows(rows);
        }

        private void AddRecord()
        {
            var model = new ProfileRecord
            {
                Id = repository.GetNextId(),
                NamSinh = DateTime.UtcNow.AddYears(-20),
                VaoDoan = false,
                VaoDang = false
            };
            using var editor = new ProfileEditorForm(model, true);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            repository.Add(editor.Record);
            ReloadAll();
        }

        private void EditRecord()
        {
            var selected = GetCurrentRow();
            if (selected == null) return;
            var current = repository.GetByIndex(selected.Index);
            if (current == null) return;
            var model = new ProfileRecord
            {
                Id = current.Value.Record.Id,
                HoVaTen = current.Value.Record.HoVaTen,
                NamSinh = current.Value.Record.NamSinh,
                NoiSinh = current.Value.Record.NoiSinh,
                QueQuan = current.Value.Record.QueQuan,
                Lop = current.Value.Record.Lop,
                TonGiao = current.Value.Record.TonGiao,
                GioiTinh = current.Value.Record.GioiTinh,
                NienKhoa = current.Value.Record.NienKhoa,
                MaSoBhyt = current.Value.Record.MaSoBhyt,
                MaSoBhxh = current.Value.Record.MaSoBhxh,
                DiaChi = current.Value.Record.DiaChi,
                Sdt = current.Value.Record.Sdt,
                VaoDoan = current.Value.Record.VaoDoan,
                VaoDang = current.Value.Record.VaoDang
            };
            using var editor = new ProfileEditorForm(model, false);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            repository.Update(selected.Index, editor.Record);
            ReloadAll();
        }

        private void DeleteRecord()
        {
            var selected = GetCurrentRow();
            if (selected == null) return;
            var confirm = MessageBox.Show(this, "Delete selected profile?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            repository.Delete(selected.Index);
            ReloadAll();
        }

        private void SeedData()
        {
            var confirm = MessageBox.Show(this, "Seed 50 sample profiles?", "Seed Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            ProfileDataSeeder.Seed(repository, 50);
            ReloadAll();
        }

        private void WipeAll()
        {
            var confirm = MessageBox.Show(this, "Delete all profiles?", "Wipe Data", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            repository.WipeAll();
            ReloadAll();
        }

        private ProfileViewRow? GetCurrentRow()
        {
            if (grid.CurrentRow?.DataBoundItem is ProfileViewRow view) return view;
            return null;
        }

        private void BindRows(IReadOnlyList<ProfileRow> rows)
        {
            viewRows.RaiseListChangedEvents = false;
            viewRows.Clear();
            foreach (var row in rows)
            {
                var dob = row.Record.NamSinh;
                if (dob == default)
                {
                    dob = DateTime.UtcNow.AddYears(-20);
                }
                else if (dob.Kind == DateTimeKind.Utc)
                {
                    dob = dob.ToLocalTime();
                }
                viewRows.Add(new ProfileViewRow
                {
                    Index = row.Index,
                    Id = row.Record.Id,
                    HoVaTen = row.Record.HoVaTen ?? string.Empty,
                    NamSinh = dob.Date,
                    NoiSinh = row.Record.NoiSinh ?? string.Empty,
                    QueQuan = row.Record.QueQuan ?? string.Empty,
                    Lop = row.Record.Lop ?? string.Empty,
                    TonGiao = row.Record.TonGiao ?? string.Empty,
                    GioiTinh = row.Record.GioiTinh ?? string.Empty,
                    NienKhoa = row.Record.NienKhoa ?? string.Empty,
                    MaSoBhyt = row.Record.MaSoBhyt ?? string.Empty,
                    MaSoBhxh = row.Record.MaSoBhxh ?? string.Empty,
                    DiaChi = row.Record.DiaChi ?? string.Empty,
                    Sdt = row.Record.Sdt ?? string.Empty,
                    VaoDoan = row.Record.VaoDoan,
                    VaoDang = row.Record.VaoDang
                });
            }
            viewRows.RaiseListChangedEvents = true;
            bindingSource.ResetBindings(false);
        }


        //unused
        private void ToggleOverlay()
        {
            if (overlayPanel.Visible)
            {
                HideOverlay();
            }
            else
            {
                ShowOverlay();
            }
        }

        private void ShowOverlay()
        {
            overlayContent.Controls.Clear();
            overlayFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(16)
            };
            overlayFlow.Controls.Add(CreateTemplateCard("Profile Summary", "Placeholder for profile summary card"));
            overlayFlow.Controls.Add(CreateTemplateCard("Engagement", "Placeholder for engagement metrics"));
            overlayContent.Controls.Add(overlayFlow);
            AdjustOverlayCards();
            overlayPanel.Visible = true;
            overlayPanel.BringToFront();
        }

        private Control CreateTemplateCard(string title, string body)
        {
            var panel = new Panel
            {
                Width = 520,
                Height = 140,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            var titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold)
            };
            titleLabel.Location = new Point(0, 0);
            var bodyLabel = new Label
            {
                Text = body,
                AutoSize = true,
                MaximumSize = new Size(480, 0)
            };
            panel.Controls.Add(titleLabel);
            var y = titleLabel.Bottom + 12;
            bodyLabel.Location = new Point(0, y);
            panel.Controls.Add(bodyLabel);
            return panel;
        }

        private void HideOverlay()
        {
            overlayPanel.Visible = false;
        }

        private void AdjustOverlayCards()
        {
            if (overlayFlow == null) return;
            var available = Math.Max(overlayContent.ClientSize.Width - overlayFlow.Padding.Horizontal - 40, 280);
            foreach (Control control in overlayFlow.Controls)
            {
                control.Width = Math.Min(available, 600);
            }
        }

        private sealed class ProfileViewRow
        {
            public int Index { get; set; }
            public int Id { get; set; }
            public string HoVaTen { get; set; } = string.Empty;
            public DateTime NamSinh { get; set; }
            public string NoiSinh { get; set; } = string.Empty;
            public string QueQuan { get; set; } = string.Empty;
            public string Lop { get; set; } = string.Empty;
            public string TonGiao { get; set; } = string.Empty;
            public string GioiTinh { get; set; } = string.Empty;
            public string NienKhoa { get; set; } = string.Empty;
            public string MaSoBhyt { get; set; } = string.Empty;
            public string MaSoBhxh { get; set; } = string.Empty;
            public string DiaChi { get; set; } = string.Empty;
            public string Sdt { get; set; } = string.Empty;
            public bool VaoDoan { get; set; }
            public bool VaoDang { get; set; }
        }
    }
}
