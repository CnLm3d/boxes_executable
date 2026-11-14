using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace BoxesPySetup
{
    public class SetupWizardForm : Form
    {
        private Color cHeaderBg = Color.FromArgb(15, 23, 42);
        private Color cSidebarBg = Color.FromArgb(30, 41, 59);
        private Color cSidebarActive = Color.FromArgb(14, 165, 233);
        private Color cContentBg = Color.FromArgb(248, 250, 252);
        private Color cTextDark = Color.FromArgb(15, 23, 42);
        private Color cTextMuted = Color.FromArgb(100, 116, 139);

        private int currentStep = 0; // 0: Dil/Hoşgeldiniz, 1: Klasör, 2: Lazer/Malzeme, 3: Kısayol/Kurulum, 4: Bitiş
        private string currentLang = "tr";

        private Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Panel pnlSidebar;
        private Label[] lblSteps;

        private Panel pnlFooter;
        private Button btnBack;
        private Button btnNext;
        private Button btnCancel;

        private Panel pnlContentContainer;
        private Panel[] stepPanels;

        // Adım 0
        private Button btnLangCardTr;
        private Button btnLangCardEn;
        private Label lblWelcomeTitle;
        private Label lblWelcomeDesc;

        // Adım 1
        private TextBox txtInstallPath;
        private Button btnBrowsePath;
        private Label lblPathTitle;
        private Label lblPathDesc;
        private Label lblDiskSpace;

        // Adım 2 (Lazer Kesim & Malzeme Varsayılanları)
        private Label lblLaserTitle;
        private Label lblLaserDesc;
        private NumericUpDown numThickness;
        private NumericUpDown numBurn;
        private ComboBox cbFormat;
        private ComboBox cbInnerCorners;
        private NumericUpDown numReference;
        private CheckBox chkLabels;
        private CheckBox chkOutside;

        // Adım 3 (Kısayollar & Kurulum)
        private Label lblInstallTitle;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartMenuShortcut;
        private Label lblInstallSummary;
        private ProgressBar progressBar;
        private Label lblInstallStatus;

        // Adım 4 (Bitiş)
        private Label lblFinishTitle;
        private Label lblFinishDesc;
        private CheckBox chkLaunchNow;

        public SetupWizardForm()
        {
            InitializeComponentLayout();
            ApplyLanguage(currentLang);
            ShowStep(0);
        }

        private void InitializeComponentLayout()
        {
            this.Text = "Boxes.py Lazer Kesim & Kutu Stüdyosu - Kurulum Sihirbazı";
            this.Size = new Size(920, 620);
            this.MinimumSize = new Size(880, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = cContentBg;
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // 1. HEADER
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = cHeaderBg
            };

            Label lblBadge = new Label
            {
                Text = "BX",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = cSidebarActive,
                Size = new Size(42, 42),
                Location = new Point(18, 14),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlHeader.Controls.Add(lblBadge);

            lblHeaderTitle = new Label
            {
                Text = "Boxes.py Lazer Kesim & Kutu Stüdyosu (Tam Çevrimdışı Sürüm)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(70, 13),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label
            {
                Text = "196 Parametrik Kutu, Çekmece, Raf ve Dişli Üretici • SVG, DXF, LightBurn, GCode, PDF",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(72, 38),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            // 2. FOOTER
            pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = Color.FromArgb(241, 245, 249)
            };

            Panel lineTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(226, 232, 240)
            };
            pnlFooter.Controls.Add(lineTop);

            btnCancel = new Button
            {
                Text = "İptal",
                Size = new Size(100, 36),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => { if (ConfirmExit()) this.Close(); };
            pnlFooter.Controls.Add(btnCancel);

            btnNext = new Button
            {
                Text = "İleri >",
                Size = new Size(130, 36),
                BackColor = cSidebarActive,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold)
            };
            btnNext.Click += BtnNext_Click;
            pnlFooter.Controls.Add(btnNext);

            btnBack = new Button
            {
                Text = "< Geri",
                Size = new Size(100, 36),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                Enabled = false
            };
            btnBack.Click += BtnBack_Click;
            pnlFooter.Controls.Add(btnBack);

            pnlFooter.Resize += (s, e) => LayoutFooterButtons();

            // 3. SIDEBAR
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = cSidebarBg,
                Padding = new Padding(12, 16, 12, 16)
            };

            string[] stepTitles = new string[]
            {
                "1. Dil & Karşılama",
                "2. Kurulum Konumu",
                "3. Lazer & Malzeme",
                "4. Kurulumu Başlat",
                "5. Tamamlandı"
            };

            lblSteps = new Label[stepTitles.Length];
            for (int i = 0; i < stepTitles.Length; i++)
            {
                lblSteps[i] = new Label
                {
                    Text = stepTitles[i],
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    Location = new Point(14, 24 + (i * 52)),
                    Size = new Size(192, 36),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                pnlSidebar.Controls.Add(lblSteps[i]);
            }

            // 4. CONTENT CONTAINER
            pnlContentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true,
                Padding = new Padding(24, 20, 24, 20)
            };

            this.Controls.Add(pnlContentContainer);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlFooter);
            this.Controls.Add(pnlHeader);

            stepPanels = new Panel[5];
            for (int i = 0; i < 5; i++)
            {
                stepPanels[i] = new Panel
                {
                    Dock = DockStyle.Fill,
                    Visible = false,
                    BackColor = Color.White,
                    AutoScroll = true
                };
                pnlContentContainer.Controls.Add(stepPanels[i]);
            }

            BuildStep0_Welcome();
            BuildStep1_InstallPath();
            BuildStep2_LaserDefaults();
            BuildStep3_InstallProgress();
            BuildStep4_Finished();

            LayoutFooterButtons();
        }

        private void LayoutFooterButtons()
        {
            if (pnlFooter == null || btnCancel == null || btnNext == null || btnBack == null) return;
            int margin = 20;
            int gap = 12;
            int w = pnlFooter.ClientSize.Width;
            if (w <= 0) w = this.ClientSize.Width > 0 ? this.ClientSize.Width : 900;
            btnCancel.Location = new Point(w - btnCancel.Width - margin, 13);
            btnNext.Location = new Point(btnCancel.Left - btnNext.Width - gap, 13);
            btnBack.Location = new Point(btnNext.Left - btnBack.Width - gap, 13);
        }

        private void BuildStep0_Welcome()
        {
            Panel p = stepPanels[0];

            lblWelcomeTitle = new Label
            {
                Text = "Boxes.py Masaüstü Stüdyosu Kurulumuna Hoş Geldiniz",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = cTextDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            p.Controls.Add(lblWelcomeTitle);

            lblWelcomeDesc = new Label
            {
                Text = "Bu kurulum paketi, https://boxes.hackerspace-bamberg.de/ üzerindeki resmi Boxes.py sisteminin\ntamamını (196 jeneratör, görsel galeri, canlı SVG önizleme ve gömülü Python motoru) hiçbir ek kurulum\ngerektirmeden çevrimdışı (offline) çalışacak şekilde bilgisayarınıza kurar.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = cTextMuted,
                Location = new Point(14, 45),
                Size = new Size(630, 70)
            };
            p.Controls.Add(lblWelcomeDesc);

            btnLangCardTr = new Button
            {
                Text = "🇹🇷  TÜRKÇE ARAYÜZ (TR)\n\n• Türkçe Kategori ve Parametre Açıklamaları\n• Milimetre (mm) Lazer Kerf & Kalınlık Ayarları\n• SVG, DXF, LightBurn (.lbrn2), G-Code, PDF Çıktı",
                Location = new Point(15, 130),
                Size = new Size(305, 120),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(12),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            btnLangCardTr.Click += (s, e) => SetSelectedLanguage("tr");
            p.Controls.Add(btnLangCardTr);

            btnLangCardEn = new Button
            {
                Text = "🇬🇧  ENGLISH INTERFACE (EN)\n\n• Original Boxes.py Menus & Documentation\n• Direct Kerf (Burn) & Material Thickness Presets\n• SVG, DXF, LightBurn (.lbrn2), G-Code, PDF Export",
                Location = new Point(335, 130),
                Size = new Size(305, 120),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(12),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            btnLangCardEn.Click += (s, e) => SetSelectedLanguage("en");
            p.Controls.Add(btnLangCardEn);

            Label lblFeatureBox = new Label
            {
                Text = "✓ 196 Hazır Parametrik Şablon (Kutular, Esnek Living-Hinge Kutular, Çekmece/Gridfinity, Raflar, Dişliler)\n✓ Gömülü Python 3.11 Motoru (Bilgisayarınızda Python kurulu olmasına gerek yoktur)\n✓ Harici programa ihtiyaç duymadan doğrudan SVG, DXF, LightBurn (.lbrn2), G-Code, PDF ve PLT üretimi\n✓ Canlı Kesim Önizleme (Preview), Görsel Kutu Galerisi ve QR Kod Üretici",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(51, 65, 85),
                BackColor = Color.FromArgb(241, 245, 249),
                Location = new Point(15, 270),
                Size = new Size(625, 105),
                Padding = new Padding(14, 12, 14, 12)
            };
            p.Controls.Add(lblFeatureBox);

            UpdateLanguageCardStyles();
        }

        private void SetSelectedLanguage(string lang)
        {
            currentLang = lang;
            UpdateLanguageCardStyles();
            ApplyLanguage(lang);
        }

        private void UpdateLanguageCardStyles()
        {
            if (currentLang == "tr")
            {
                btnLangCardTr.BackColor = Color.FromArgb(224, 242, 254);
                btnLangCardTr.ForeColor = Color.FromArgb(3, 105, 161);
                btnLangCardTr.FlatAppearance.BorderColor = cSidebarActive;
                btnLangCardTr.FlatAppearance.BorderSize = 2;

                btnLangCardEn.BackColor = Color.White;
                btnLangCardEn.ForeColor = cTextDark;
                btnLangCardEn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                btnLangCardEn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                btnLangCardEn.BackColor = Color.FromArgb(224, 242, 254);
                btnLangCardEn.ForeColor = Color.FromArgb(3, 105, 161);
                btnLangCardEn.FlatAppearance.BorderColor = cSidebarActive;
                btnLangCardEn.FlatAppearance.BorderSize = 2;

                btnLangCardTr.BackColor = Color.White;
                btnLangCardTr.ForeColor = cTextDark;
                btnLangCardTr.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                btnLangCardTr.FlatAppearance.BorderSize = 1;
            }
        }

        private void BuildStep1_InstallPath()
        {
            Panel p = stepPanels[1];

            lblPathTitle = new Label
            {
                Text = "Kurulum Hedef Klasörünü Seçin",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = cTextDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            p.Controls.Add(lblPathTitle);

            lblPathDesc = new Label
            {
                Text = "Boxes.py Masaüstü Stüdyosu aşağıdaki klasöre kurulacaktır. Farklı bir konum seçmek için\n'Gözat...' butonunu kullanabilirsiniz.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = cTextMuted,
                Location = new Point(14, 45),
                Size = new Size(620, 50)
            };
            p.Controls.Add(lblPathDesc);

            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BoxesPy_Studio"
            );

            txtInstallPath = new TextBox
            {
                Text = defaultDir,
                Location = new Point(15, 110),
                Size = new Size(500, 30),
                Font = new Font("Segoe UI", 10f)
            };
            txtInstallPath.TextChanged += (s, e) => UpdateDiskSpaceInfo();
            p.Controls.Add(txtInstallPath);

            btnBrowsePath = new Button
            {
                Text = "Gözat...",
                Location = new Point(525, 108),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.System
            };
            btnBrowsePath.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtInstallPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallPath.Text = Path.Combine(fbd.SelectedPath, "BoxesPy_Studio");
                    }
                }
            };
            p.Controls.Add(btnBrowsePath);

            lblDiskSpace = new Label
            {
                Text = "Gerekli Disk Alanı: ~95 MB",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(15, 155),
                Size = new Size(600, 30)
            };
            p.Controls.Add(lblDiskSpace);
        }

        private void UpdateDiskSpaceInfo()
        {
            if (lblDiskSpace == null || txtInstallPath == null) return;
            try
            {
                string root = Path.GetPathRoot(txtInstallPath.Text.Trim());
                if (!string.IsNullOrEmpty(root))
                {
                    DriveInfo di = new DriveInfo(root);
                    double freeGb = Math.Round(di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                    lblDiskSpace.Text = currentLang == "tr"
                        ? string.Format("Gerekli Disk Alanı: ~95 MB   |   Kullanılabilir Boş Alan ({0}): {1} GB", root, freeGb)
                        : string.Format("Required Space: ~95 MB   |   Available Free Space ({0}): {1} GB", root, freeGb);
                }
            }
            catch { }
        }

        private void BuildStep2_LaserDefaults()
        {
            Panel p = stepPanels[2];

            lblLaserTitle = new Label
            {
                Text = "Lazer Kesici ve Malzeme Varsayılan Ayarları",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = cTextDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            p.Controls.Add(lblLaserTitle);

            lblLaserDesc = new Label
            {
                Text = "Kutu şablonlarını açtığınızda otomatik gelecek varsayılan malzeme kalınlığı, lazer yakma payı (burn/kerf)\nve çıktı formatını buradan belirleyebilirsiniz (daha sonra arayüzden istediğiniz zaman değiştirebilirsiniz):",
                Font = new Font("Segoe UI", 9.25f),
                ForeColor = cTextMuted,
                Location = new Point(14, 42),
                Size = new Size(630, 48)
            };
            p.Controls.Add(lblLaserDesc);

            int y = 100;
            int lblW = 280;
            int ctrlX = 305;

            // Malzeme kalınlığı
            p.Controls.Add(new Label { Text = "Varsayılan Malzeme Kalınlığı (thickness - mm):", Location = new Point(15, y + 4), Size = new Size(lblW, 24), Font = new Font("Segoe UI", 9.25f, FontStyle.Bold) });
            numThickness = new NumericUpDown { Location = new Point(ctrlX, y), Size = new Size(140, 28), DecimalPlaces = 2, Increment = 0.5m, Minimum = 0.5m, Maximum = 50.0m, Value = 3.00m };
            p.Controls.Add(numThickness);
            y += 42;

            // Burn (Kerf)
            p.Controls.Add(new Label { Text = "Lazer Yakma Payı / Kerf (burn - mm):", Location = new Point(15, y + 4), Size = new Size(lblW, 24), Font = new Font("Segoe UI", 9.25f, FontStyle.Bold) });
            numBurn = new NumericUpDown { Location = new Point(ctrlX, y), Size = new Size(140, 28), DecimalPlaces = 3, Increment = 0.01m, Minimum = 0.000m, Maximum = 2.000m, Value = 0.100m };
            p.Controls.Add(numBurn);
            y += 42;

            // Çıktı Formatı
            p.Controls.Add(new Label { Text = "Varsayılan Çıktı Formatı (format):", Location = new Point(15, y + 4), Size = new Size(lblW, 24), Font = new Font("Segoe UI", 9.25f, FontStyle.Bold) });
            cbFormat = new ComboBox { Location = new Point(ctrlX, y), Size = new Size(220, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cbFormat.Items.AddRange(new object[] { "svg", "dxf", "lbrn2", "gcode", "pdf", "plt", "ps", "svg_Ponoko" });
            cbFormat.SelectedIndex = 0;
            p.Controls.Add(cbFormat);
            y += 42;

            // İç köşe stili
            p.Controls.Add(new Label { Text = "İç Köşe Kesim Stili (inner_corners):", Location = new Point(15, y + 4), Size = new Size(lblW, 24), Font = new Font("Segoe UI", 9.25f, FontStyle.Bold) });
            cbInnerCorners = new ComboBox { Location = new Point(ctrlX, y), Size = new Size(220, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            cbInnerCorners.Items.AddRange(new object[] { "loop", "corner", "backarc" });
            cbInnerCorners.SelectedIndex = 0;
            p.Controls.Add(cbInnerCorners);
            y += 42;

            // Referans cetveli
            p.Controls.Add(new Label { Text = "Referans Ölçek Çubuğu (reference - mm):", Location = new Point(15, y + 4), Size = new Size(lblW, 24), Font = new Font("Segoe UI", 9.25f, FontStyle.Bold) });
            numReference = new NumericUpDown { Location = new Point(ctrlX, y), Size = new Size(140, 28), DecimalPlaces = 1, Increment = 10.0m, Minimum = 0.0m, Maximum = 500.0m, Value = 100.0m };
            p.Controls.Add(numReference);
            y += 44;

            chkLabels = new CheckBox
            {
                Text = "Parçaların üzerine adlarını/etiketlerini yaz (labels = 1)",
                Checked = true,
                Location = new Point(15, y),
                Size = new Size(500, 26),
                Font = new Font("Segoe UI", 9.25f)
            };
            p.Controls.Add(chkLabels);
            y += 30;

            chkOutside = new CheckBox
            {
                Text = "Kutu ölçülerini dıştan dışa ölçü olarak kabul et (outside = 1)",
                Checked = true,
                Location = new Point(15, y),
                Size = new Size(500, 26),
                Font = new Font("Segoe UI", 9.25f)
            };
            p.Controls.Add(chkOutside);
        }

        private void BuildStep3_InstallProgress()
        {
            Panel p = stepPanels[3];

            lblInstallTitle = new Label
            {
                Text = "Kısayol Seçenekleri ve Kurulumu Başlat",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = cTextDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            p.Controls.Add(lblInstallTitle);

            chkDesktopShortcut = new CheckBox
            {
                Text = "Masaüstüne 'Boxes.py Lazer Kutu Stüdyosu' Kısayolu Oluştur",
                Checked = true,
                Location = new Point(16, 55),
                Size = new Size(550, 28),
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold)
            };
            p.Controls.Add(chkDesktopShortcut);

            chkStartMenuShortcut = new CheckBox
            {
                Text = "Başlat Menüsüne Kısayol Oluştur",
                Checked = true,
                Location = new Point(16, 90),
                Size = new Size(550, 28),
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold)
            };
            p.Controls.Add(chkStartMenuShortcut);

            lblInstallSummary = new Label
            {
                Text = "",
                Location = new Point(16, 130),
                Size = new Size(620, 130),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 9f),
                Padding = new Padding(12)
            };
            p.Controls.Add(lblInstallSummary);

            progressBar = new ProgressBar
            {
                Location = new Point(16, 280),
                Size = new Size(620, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visible = false
            };
            p.Controls.Add(progressBar);

            lblInstallStatus = new Label
            {
                Text = "Hazırlanıyor...",
                Location = new Point(16, 316),
                Size = new Size(620, 28),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = cSidebarActive,
                Visible = false
            };
            p.Controls.Add(lblInstallStatus);
        }

        private void BuildStep4_Finished()
        {
            Panel p = stepPanels[4];

            lblFinishTitle = new Label
            {
                Text = "Tebrikler! Boxes.py Stüdyosu Başarıyla Kuruldu",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(12, 16),
                AutoSize = true
            };
            p.Controls.Add(lblFinishTitle);

            lblFinishDesc = new Label
            {
                Text = "Boxes.py Lazer Kesim ve Kutu Üretici tüm 196 parametrik şablonu, görsel galerisi ve\ngömülü Python motoruyla birlikte bilgisayarınıza kuruldu.\n\nArtık internet bağlantısına ihtiyaç duymadan masaüstü kısayolundan doğrudan kullanabilirsiniz.",
                Font = new Font("Segoe UI", 9.75f),
                ForeColor = cTextDark,
                Location = new Point(14, 58),
                Size = new Size(620, 95)
            };
            p.Controls.Add(lblFinishDesc);

            chkLaunchNow = new CheckBox
            {
                Text = "Boxes.py Lazer Kutu Stüdyosu'nu Şimdi Başlat",
                Checked = true,
                Location = new Point(16, 175),
                Size = new Size(520, 32),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = cSidebarActive
            };
            p.Controls.Add(chkLaunchNow);
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            for (int i = 0; i < stepPanels.Length; i++)
            {
                stepPanels[i].Visible = (i == step);
                if (i == step)
                {
                    lblSteps[i].BackColor = cSidebarActive;
                    lblSteps[i].ForeColor = Color.White;
                    lblSteps[i].Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
                }
                else if (i < step)
                {
                    lblSteps[i].BackColor = Color.Transparent;
                    lblSteps[i].ForeColor = Color.FromArgb(56, 189, 248);
                    lblSteps[i].Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
                }
                else
                {
                    lblSteps[i].BackColor = Color.Transparent;
                    lblSteps[i].ForeColor = Color.FromArgb(148, 163, 184);
                    lblSteps[i].Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
                }
            }

            btnBack.Enabled = (step > 0 && step < 4);
            if (step == 3)
            {
                btnNext.Text = currentLang == "tr" ? "Kurulumu Başlat" : "Install Now";
                lblInstallSummary.Text = string.Format(
                    "Hedef Klasör: {0}\nArayüz Dili: {1}\nMalzeme Kalınlığı: {2} mm   |   Lazer Kerf (Burn): {3} mm\nVarsayılan Çıktı: {4}   |   İç Köşe: {5}\nİçerik: 196 Parametrik Kutu Şablonu + Gömülü Python 3.11 + Tüm Çıktı Formatları",
                    txtInstallPath.Text.Trim(),
                    currentLang.ToUpperInvariant(),
                    numThickness.Value.ToString("0.00", CultureInfo.InvariantCulture),
                    numBurn.Value.ToString("0.000", CultureInfo.InvariantCulture),
                    cbFormat.SelectedItem,
                    cbInnerCorners.SelectedItem
                );
            }
            else if (step == 4)
            {
                btnNext.Text = currentLang == "tr" ? "Bitir" : "Finish";
                btnCancel.Enabled = false;
            }
            else
            {
                btnNext.Text = currentLang == "tr" ? "İleri >" : "Next >";
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (currentStep == 3)
            {
                RunInstallationProcess();
                return;
            }
            else if (currentStep == 4)
            {
                if (chkLaunchNow.Checked)
                {
                    string exePath = Path.Combine(txtInstallPath.Text.Trim(), "BoxesPy_Studio.exe");
                    if (File.Exists(exePath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                WorkingDirectory = txtInstallPath.Text.Trim()
                            });
                        }
                        catch { }
                    }
                }
                this.Close();
                return;
            }

            if (currentStep < 4)
            {
                ShowStep(currentStep + 1);
            }
        }

        private void BtnBack_Click(object sender, EventArgs e)
        {
            if (currentStep > 0)
            {
                ShowStep(currentStep - 1);
            }
        }

        private bool ConfirmExit()
        {
            if (currentStep >= 4) return true;
            string msg = currentLang == "tr"
                ? "Kurulum sihirbazından çıkmak istediğinize emin misiniz?"
                : "Are you sure you want to exit the setup wizard?";
            return MessageBox.Show(msg, "Çıkış", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void RunInstallationProcess()
        {
            btnNext.Enabled = false;
            btnBack.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            lblInstallStatus.Visible = true;
            progressBar.Value = 10;
            lblInstallStatus.Text = currentLang == "tr" ? "Kurulum klasörü hazırlanıyor..." : "Preparing target directory...";
            Application.DoEvents();

            string targetDir = txtInstallPath.Text.Trim();

            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                progressBar.Value = 25;
                lblInstallStatus.Text = currentLang == "tr"
                    ? "Boxes.py şablonları ve gömülü Python motoru ayıklanıyor..."
                    : "Extracting Boxes.py generators and embedded Python runtime...";
                Application.DoEvents();

                ExtractEmbeddedPayload(targetDir);

                progressBar.Value = 75;
                lblInstallStatus.Text = currentLang == "tr"
                    ? "Lazer ve malzeme varsayılan ayarları kaydediliyor..."
                    : "Writing laser and material configuration...";
                Application.DoEvents();

                WriteInitialConfigFile(targetDir);

                progressBar.Value = 90;
                lblInstallStatus.Text = currentLang == "tr"
                    ? "Masaüstü ve Başlat Menüsü kısayolları oluşturuluyor..."
                    : "Creating desktop and start menu shortcuts...";
                Application.DoEvents();

                string mainExe = Path.Combine(targetDir, "BoxesPy_Studio.exe");
                string appIco = Path.Combine(targetDir, "app.ico");

                if (chkDesktopShortcut.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string lnk = Path.Combine(desktop, "Boxes.py Lazer Kutu Studyosu.lnk");
                    CreateWindowsShortcut(lnk, mainExe, targetDir, appIco, "Boxes.py Lazer Kesim ve Parametrik Kutu Üretici");
                }

                if (chkStartMenuShortcut.Checked)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Boxes.py Studio");
                    if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                    string lnk = Path.Combine(startMenu, "Boxes.py Lazer Kutu Studyosu.lnk");
                    CreateWindowsShortcut(lnk, mainExe, targetDir, appIco, "Boxes.py Lazer Kesim ve Parametrik Kutu Üretici");
                }

                progressBar.Value = 100;
                lblInstallStatus.Text = currentLang == "tr" ? "Kurulum başarıyla tamamlandı!" : "Installation completed!";
                Application.DoEvents();

                System.Threading.Thread.Sleep(400);
                btnNext.Enabled = true;
                ShowStep(4);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kurulum sırasında hata oluştu:\n" + ex.Message, "Kurulum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnNext.Enabled = true;
                btnBack.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        public void ExtractEmbeddedPayload(string targetDir)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = null;
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                if (File.Exists(localZip))
                {
                    using (FileStream fs = new FileStream(localZip, FileMode.Open, FileAccess.Read))
                    {
                        ExtractZipStream(fs, targetDir);
                    }
                    return;
                }
                throw new FileNotFoundException("Gömülü 'payload.zip' paketi bulunamadı!");
            }

            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                ExtractZipStream(s, targetDir);
            }
        }

        private void ExtractZipStream(Stream s, string targetDir)
        {
            using (ZipArchive archive = new ZipArchive(s, ZipArchiveMode.Read))
            {
                int total = archive.Entries.Count;
                int count = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    count++;
                    if (count % 80 == 0 && progressBar != null && progressBar.Visible)
                    {
                        int pct = 25 + (int)((count / (double)Math.Max(1, total)) * 48);
                        progressBar.Value = Math.Min(74, pct);
                        Application.DoEvents();
                    }

                    string completeFileName = Path.Combine(targetDir, entry.FullName);
                    string directory = Path.GetDirectoryName(completeFileName);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(completeFileName, true);
                    }
                }
            }
        }

        public void WriteInitialConfigFile(string targetDir)
        {
            string fmt = cbFormat != null && cbFormat.SelectedItem != null ? cbFormat.SelectedItem.ToString() : "svg";
            string corners = cbInnerCorners != null && cbInnerCorners.SelectedItem != null ? cbInnerCorners.SelectedItem.ToString() : "loop";
            string thk = numThickness != null ? numThickness.Value.ToString("0.##", CultureInfo.InvariantCulture) : "3.0";
            string brn = numBurn != null ? numBurn.Value.ToString("0.###", CultureInfo.InvariantCulture) : "0.1";
            string rf = numReference != null ? numReference.Value.ToString("0.#", CultureInfo.InvariantCulture) : "100.0";
            string lbl = (chkLabels == null || chkLabels.Checked) ? "1" : "0";
            string outs = (chkOutside == null || chkOutside.Checked) ? "1" : "0";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"language\": \"" + currentLang + "\",");
            sb.AppendLine("  \"defaults\": {");
            sb.AppendLine("    \"thickness\": \"" + thk + "\",");
            sb.AppendLine("    \"burn\": \"" + brn + "\",");
            sb.AppendLine("    \"format\": \"" + fmt + "\",");
            sb.AppendLine("    \"inner_corners\": \"" + corners + "\",");
            sb.AppendLine("    \"reference\": \"" + rf + "\",");
            sb.AppendLine("    \"labels\": \"" + lbl + "\",");
            sb.AppendLine("    \"outside\": \"" + outs + "\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(targetDir, "initial_config.json"), sb.ToString(), Encoding.UTF8);
        }

        private void CreateWindowsShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string desc)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    object shell = Activator.CreateInstance(shellType);
                    object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                    Type scType = shortcut.GetType();

                    scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                    scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                    scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { desc });
                    if (File.Exists(iconPath))
                    {
                        scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                    }
                    scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                    return;
                }
            }
            catch { }
        }

        private void ApplyLanguage(string lang)
        {
            currentLang = lang;
            bool isTr = (lang == "tr");

            this.Text = isTr
                ? "Boxes.py Lazer Kesim & Kutu Stüdyosu - Kurulum Sihirbazı"
                : "Boxes.py Laser Cutter & Box Studio - Setup Wizard";

            lblHeaderTitle.Text = isTr
                ? "Boxes.py Lazer Kesim & Kutu Stüdyosu (Tam Çevrimdışı Sürüm)"
                : "Boxes.py Laser Cutter & Box Studio (Full Offline Edition)";

            btnBack.Text = isTr ? "< Geri" : "< Back";
            btnCancel.Text = isTr ? "İptal" : "Cancel";

            lblSteps[0].Text = isTr ? "1. Dil & Karşılama" : "1. Language & Welcome";
            lblSteps[1].Text = isTr ? "2. Kurulum Konumu" : "2. Install Directory";
            lblSteps[2].Text = isTr ? "3. Lazer & Malzeme" : "3. Laser & Material";
            lblSteps[3].Text = isTr ? "4. Kurulumu Başlat" : "4. Start Install";
            lblSteps[4].Text = isTr ? "5. Tamamlandı" : "5. Finished";

            UpdateDiskSpaceInfo();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args != null && args.Length >= 2 && args[0].ToLowerInvariant() == "--silent-install")
            {
                string targetDir = args[1];
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    SetupWizardForm form = new SetupWizardForm();
                    form.ExtractEmbeddedPayload(targetDir);
                    form.WriteInitialConfigFile(targetDir);
                    return 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(targetDir, "setup_error.txt"), ex.ToString());
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupWizardForm());
            return 0;
        }
    }
}
