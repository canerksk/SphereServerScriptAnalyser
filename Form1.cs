using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SphereServerScriptAnalyser
{
    public partial class Form1 : Form
    {
        private readonly Dictionary<string, FileReport> _reportsByPath = new(StringComparer.OrdinalIgnoreCase);

        public Form1()
        {
            InitializeComponent();

            /*
            lvProblems.Columns.AddRange(new[] { 
                chFile, 
                chIssueCount
            });
            */

        }

        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = Properties.Resources.SelectTheSphereScriptsFolder,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtFolder.Text = dlg.SelectedPath;
                await StartScanAsync(dlg.SelectedPath);
            }
        }


        private void lvProblems_ItemActivate(object sender, EventArgs e)
        {
            if (lvProblems.SelectedItems.Count == 0)
                return;

            var item = lvProblems.SelectedItems[0];
            if (item.Tag is FileReport fr)
            {
                using var dlg = new IssueViewerForm(fr);
                dlg.ShowDialog(this);
            }
        }


        // ------------------- Analys -------------------

        private async Task StartScanAsync(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(Properties.Resources.StringAValidFolder, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            lvProblems.Items.Clear();
            _reportsByPath.Clear();
            lblStatus.Text = Properties.Resources.Scanning;

            try
            {
                var result = await Task.Run(() => RunScan(folder));
                var problemFiles = result.Where(r => r.Issues.Count > 0).OrderBy(r => r.Path).ToList();

                foreach (var fr in problemFiles)
                {
                    var li = new ListViewItem(new[] { fr.Path, fr.Issues.Count.ToString() })
                    {
                        Tag = fr,
                        ForeColor = Color.DarkRed
                    };
                    lvProblems.Items.Add(li);
                    _reportsByPath[fr.Path] = fr;
                }

                lblStatus.Text = $"{Properties.Resources.AnalysisCompleted}. {result.Count} {Properties.Resources.FilesWereExamined}, {string.Format(Properties.Resources.ProblemsWereFoundInFiles0, problemFiles.Count)}<";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.ScanningError} {ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = $"{Properties.Resources.AnErrorOccurred}";
            }
        }

        // tr;
        // Temel kurallar (ZIP’teki .scp standartlarýna göre):
        // - IF / (ELSEIF|ELIF)* / (ELSE)? / ENDIF dengesi (stack)
        // - ELSEIF/ELIF ve ELSE yalnýzca açýk bir IF varken geçerli
        // - ENDIF yalnýzca açýk bir IF'i kapatýr
        // - [eof] son anlamlý satýrda case-insensitive
        // - Opsiyonel: WHILE/ENDWHILE, FOR/ENDFOR, SWITCH/ENDSWITCH dengesi
        // en;
        // Basic rules (according to .scp standards in ZIP):
        // - IF / (ELSEIF|ELIF)* / (ELSE)? / ENDIF balance (stack)
        // - ELSEIF/ELIF and ELSE are only valid when there is an open IF
        // - ENDIF only closes an open IF
        // - [eof] on the last significant line is case-insensitive
        // - Optional: WHILE/ENDWHILE, FOR/ENDFOR, SWITCH/ENDSWITCH balance

        private List<FileReport> RunScan(string root)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".scp" };

            var allFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Where(f => exts.Contains(Path.GetExtension(f))).ToList();

            var results = new List<FileReport>(allFiles.Count);

            // regexes (Developed in Sphere language)
            // COMNMENT LINE (satýr baþý ; veya //)
            var commentRegex = new Regex(@"^\s*(;|//)", RegexOptions.Compiled);

            // DIRECTIVES (case-insensitive)
            var ifRegex = new Regex(@"^\s*if\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var elseifRegex = new Regex(@"^\s*(elseif|elif)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var elseRegex = new Regex(@"^\s*else\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endifRegex = new Regex(@"^\s*endif\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Optional pairs (available in some scripts)
            var whileRegex = new Regex(@"^\s*while\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endwhileRegex = new Regex(@"^\s*endwhile\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var forRegex = new Regex(@"^\s*for\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endforRegex = new Regex(@"^\s*endfor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            // FOR family: They all end with ENDFOR
            var forOpenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "for",
                "forcharlayer",
                "forcharmemorytype",
                "forchars",
                "forclients",
                "forplayers",
                "forcont",
                "forcontid",
                "forconttype",
                "forinstances",
                "foritems",
                "forobjs"
            };

            var switchRegex = new Regex(@"^\s*switch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endswitchRegex = new Regex(@"^\s*endswitch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);


            // 1) Satýrdaki TÜM köþeli-parantez bloklarýný aday olarak yakalar (bozuk olanlar dahil)
            var headerCandidateRegex = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);

            // 2) GEÇERLÝ definition baþlýðý (tam kural):
            //    - '[' hemen ardýndan TYPE (boþluk yok)
            //    - TYPE ile NAME arasýnda TAM OLARAK 1 boþluk
            //    - NAME: [A-Za-z0-9_]+ (tek token)
            //    - ']' öncesinde boþluk YOK
            var validDefRegex = new Regex(
                @"\[(?<type>FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE) (?<name>[A-Za-z0-9_.]+)\]",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            );

            // 3) Duplicate için kullanacaðýmýz “tam eþleþme” regex’i (valid ile ayný)
            var defHeaderInlineRegex = validDefRegex;


            foreach (var path in allFiles)
            {
                var fr = new FileReport { Path = path };
                try
                {
                    string[] lines;
                    try { lines = File.ReadAllLines(path); }
                    catch { lines = File.ReadAllLines(path, System.Text.Encoding.Default); }

                    fr.Lines = lines.Length;

                    var defIndex = new Dictionary<string, int>();

                    //var defIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var stackIf = new Stack<int>();
                    var stackWhile = new Stack<int>();
                    var stackFor = new Stack<int>();
                    var stackSwitch = new Stack<int>();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var raw = lines[i];
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        // clear whitespace at the beginning of a line
                        var line = raw.TrimStart();

                        // line-in comments (only at the beginning ; or //)
                        if (line.StartsWith(";") || line.StartsWith("//"))
                            continue;

                        // ---- HEADER FORMAT & DUPLICATE CHECK ----

                        // 3.1) Önce satýrdaki TÜM köþeli baþlýk adaylarýný bul (bozuklar dahil)
                        // 3.1) Önce satýrdaki TÜM köþeli baþlýk adaylarýný bul (bozuklar dahil)
                        var candidates = headerCandidateRegex.Matches(line);
                        foreach (Match cand in candidates)
                        {
                            var token = cand.Value; // örn: "[FUNCTION getacc ]", "[ FUNCTION getacc]", "[FUNCTIONgetacc]"

                            // >>>>> ekle: [eof]’u tamamen görmezden gel
                            if (token.Equals("[eof]", StringComparison.OrdinalIgnoreCase))
                                continue;

                            // >>>>> ekle: sadece bizim TYPE listemizle baþlayan adaylarý incele
                            // (köþeli parantezden sonra boþluk olsa da TYPE varsa yakalar)

                            // typeHead: köþeli parantez sonrasý TYPE var mý? ( \b kullanmýyoruz ki "[FUNCTIONmytest]" gibi hatalý formlar da aday olsun )

                            // ESKÝ:
                            //var typeHead = Regex.Match(token, @"^\[\s*(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)\b", RegexOptions.IgnoreCase);

                            // YENÝ ( \b yok ):
                            var typeHead = Regex.Match(token, @"^\[\s*(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)", RegexOptions.IgnoreCase);

                            if (!typeHead.Success)
                                continue;

                            // 3.2) Geçerli mi? (tam þablon)
                            var vm = validDefRegex.Match(token);
                            if (vm.Success)
                            {
                                // Geçerli format -> duplicate kontrolüne býrak (aþaðýda 3.4’te)
                                continue;
                            }

                            // 3.3) Geçersiz format -> hangi kural bozulduysa issue üret
                            if (HasSpaceRightAfterOpenBracket(token))
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "SpaceAfterOpeningBracket", Message = $"'{token}' : '[' sonrasý boþluk olmamalý" });
                                continue;
                            }
                            if (MissingSpaceBetweenTypeAndName(token))
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "MissingSpaceBetweenTypeAndName", Message = $"'{token}' : TYPE ile NAME arasýnda tam 1 boþluk olmalý" });
                                continue;
                            }
                            if (MultipleSpacesBetweenTypeAndName(token))
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "MultipleSpacesBetweenTypeAndName", Message = $"'{token}' : TYPE ile NAME arasýnda 1’den fazla boþluk var" });
                                continue;
                            }
                            if (HasSpaceBeforeClosingBracket(token))
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "SpaceBeforeClosingBracket", Message = $"'{token}' : ']' öncesinde boþluk olmamalý" });
                                continue;
                            }
                            if (InvalidNameToken(token))
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "InvalidDefinitionName", Message = $"'{token}' : NAME yalnýzca A-Z, 0-9 veya '_' içermeli" });
                                continue;
                            }

                            fr.Issues.Add(new Issue { Line = i + 1, Type = "InvalidDefinitionHeader", Message = $"'{token}' : Geçersiz definition biçimi" });
                        }


                        // 3.4) Þimdi yalnýzca GEÇERLÝ baþlýklar üzerinden duplicate kontrolü yap
                        var valids = defHeaderInlineRegex.Matches(line);
                        foreach (Match dm in valids)
                        {
                            var type = dm.Groups["type"].Value;
                            var name = dm.Groups["name"].Value;

                            // Case-insensitive tam eþleþme anahtarý
                            var key = $"{type.ToUpperInvariant()}|{name.ToUpperInvariant()}";

                            if (defIndex.TryGetValue(key, out var firstLine))
                            {
                                fr.Issues.Add(new Issue
                                {
                                    Line = i + 1,
                                    Type = "DuplicateDefinition",
                                    Message = $"Duplicate [{type} {name}] (first at line {firstLine})"
                                });
                            }
                            else
                            {
                                defIndex[key] = i + 1;
                            }
                        }


                        // satýrýn ilk kelimesini (harflerden oluþan) yakala
                        // capture the first word (of letters) of the line
                        // ex: "elif (...)" -> "elif", "ENDIF" -> "ENDIF"
                        // ör: "elif (...)" -> "elif", "ENDIF  " -> "ENDIF"
                        var m = Regex.Match(line, @"^(?<kw>[A-Za-z]+)\b");
                        if (!m.Success)
                            continue;

                        var kw = m.Groups["kw"].Value.ToLowerInvariant();

                        // ---- IF family ----
                        if (kw == "if")
                        {
                            stackIf.Push(i + 1);
                            continue;
                        }
                        if (kw == "elseif" || kw == "elif")
                        {
                            if (stackIf.Count == 0)
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "ElseIfWithoutIf", Message = "elseif/elif without matching if" });
                            }
                            continue; // stackIf immutable
                        }
                        if (kw == "else")
                        {
                            if (stackIf.Count == 0)
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "ElseWithoutIf", Message = "else without matching if" });
                            }
                            continue; // stackIf immutable
                        }
                        if (kw == "endif")
                        {
                            if (stackIf.Count == 0)
                            {
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndIf", Message = "endif without matching if" });
                            }
                            else
                            {
                                stackIf.Pop();
                            }
                            continue;
                        }

                        // ---- (optional) other pairs ----
                        if (kw == "while")
                        {
                            stackWhile.Push(i + 1); continue;
                        }

                        if (kw == "endwhile")
                        {
                            if (stackWhile.Count == 0)
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndWhile", Message = "endwhile without matching while" });
                            else
                                stackWhile.Pop();
                            continue;
                        }

                        // ---- FOR family ----
                        // Açýlýþ: listedeki tüm varyantlar ayný stack'e push
                        if (forOpenSet.Contains(kw))
                        {
                            stackFor.Push(i + 1);
                            continue;
                        }

                        // Closed: only ENDFOR
                        if (kw == "endfor")
                        {
                            if (stackFor.Count == 0)
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndFor", Message = "endfor without matching for*" });
                            else
                                stackFor.Pop();
                            continue;
                        }

                        if (kw == "switch")
                        {
                            stackSwitch.Push(i + 1);
                            continue;
                        }

                        if (kw == "endswitch")
                        {
                            if (stackSwitch.Count == 0)
                                fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndSwitch", Message = "endswitch without matching switch" });
                            else
                                stackSwitch.Pop();
                            continue;
                        }

                        // other situations: no-op
                    }

                    // unclosed blocks
                    foreach (var ln in stackIf)
                        fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedIf", Message = "if var ama endif yok" });

                    foreach (var ln in stackWhile)
                        fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedWhile", Message = "While var ama endwhile yok" });

                    foreach (var ln in stackFor)
                        fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedFor", Message = "for* var ama endfor yok" });

                    foreach (var ln in stackSwitch)
                        fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedSwitch", Message = "switch var ama endswitch yok" });

                    // [eof] kontrolü — son anlamlý satýr (boþ/yorum deðil) [eof] olmalý (case-insensitive)
                    bool hasEof = false;
                    for (int j = lines.Length - 1; j >= 0; j--)
                    {
                        var s = lines[j].Trim();
                        if (s.Length == 0) continue;                 // boþ satýrý geç
                        if (s.StartsWith(";") || s.StartsWith("//")) continue; // yorum satýrýný geç
                        if (s.Equals("[eof]", StringComparison.OrdinalIgnoreCase))
                            hasEof = true;
                        break; // ilk anlamlý satýrý kontrol ettik
                    }
                    if (!hasEof)
                    {
                        fr.Issues.Add(new Issue
                        {
                            Line = lines.Length,
                            Type = "NOEOF",
                            Message = $"{Properties.Resources.ThereIsNoEofAtTheEndOfTheFile}" // [eof]/[EOF]/[Eof] hepsi kabul
                        });
                    }
                }
                catch (Exception ex)
                {
                    fr.Issues.Add(new Issue
                    {
                        Line = 0,
                        Type = "ReadError",
                        Message = $"{Properties.Resources.TheFileCouldNotBeRead} {ex.Message}"
                    });
                }

                results.Add(fr);
            }

            return results;
        }

        // kural: '[' iþaretinden hemen sonra boþluk/tabs olmamalý
        bool HasSpaceRightAfterOpenBracket(string token) => token.StartsWith("[ ") || token.StartsWith("[\t");

        bool MissingSpaceBetweenTypeAndName(string token) =>
            // [FUNCTIONgetacc] gibi: TYPE’ý bulup hemen NAME geliyor mu?
            Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)[A-Za-z0-9_.]+\]$", RegexOptions.IgnoreCase);

        bool MultipleSpacesBetweenTypeAndName(string token) =>
            Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)\s{2,}[A-Za-z0-9_.]+\]$", RegexOptions.IgnoreCase);

        bool HasSpaceBeforeClosingBracket(string token) =>
            Regex.IsMatch(token, @"\s\]$");

        bool InvalidNameToken(string token) =>
            // [TYPE   name*bad] gibi geçersiz karakter içeriyorsa
            Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)\s+([^\]\s]+)\]$", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)\s+[A-Za-z0-9_.]+\]$", RegexOptions.IgnoreCase);


        // ------------------- DTOs -------------------
        private class FileReport
        {
            public string Path { get; set; } = "";
            public int Lines { get; set; }
            public List<Issue> Issues { get; set; } = new();
        }

        private class Issue
        {
            public int Line { get; set; }
            public string Type { get; set; } = "";
            public string Message { get; set; } = "";
        }

        // ------------------- Detail Dlg -------------------
        private class IssueViewerForm : Form
        {
            private readonly FileReport _report;
            private ListBox lbIssues;
            private RichTextBox rtb;
            private Button btnOpenFile;

            public IssueViewerForm(FileReport report)
            {
                _report = report;
                InitUi();
                LoadFileAndIssues();
            }

            private void InitUi()
            {
                Text = $"{Properties.Resources.Detail} – {Path.GetFileName(_report.Path)}";
                Width = 1000;
                Height = 700;
                StartPosition = FormStartPosition.CenterParent;

                lbIssues = new ListBox
                {
                    Dock = DockStyle.Left,
                    Width = 320
                };
                lbIssues.SelectedIndexChanged += LbIssues_SelectedIndexChanged;

                rtb = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10),
                    ReadOnly = true,
                    WordWrap = false
                };

                btnOpenFile = new Button
                {
                    Text = Properties.Resources.OpenTheFile,
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                btnOpenFile.Click += BtnOpenFile_Click;

                Controls.Add(rtb);
                Controls.Add(lbIssues);
                Controls.Add(btnOpenFile);
            }

            private void LoadFileAndIssues()
            {
                try
                {
                    rtb.Text = File.ReadAllText(_report.Path);
                }
                catch (Exception ex)
                {
                    rtb.Text = $"{Properties.Resources.TheFileCouldNotBeRead} {ex.Message}";
                }

                lbIssues.Items.Clear();
                foreach (var issue in _report.Issues.OrderBy(i => i.Line))
                {
                    lbIssues.Items.Add($"L{issue.Line} – {issue.Type}: {issue.Message}");
                }

                // Automatically select and highlight the first issue if there is one
                if (lbIssues.Items.Count > 0)
                    lbIssues.SelectedIndex = 0;
            }

            private void LbIssues_SelectedIndexChanged(object sender, EventArgs e)
            {
                if (lbIssues.SelectedIndex < 0)
                    return;
                var issue = _report.Issues.OrderBy(i => i.Line).ElementAt(lbIssues.SelectedIndex);
                HighlightLine(issue.Line, Color.LightPink, Color.DarkRed);
            }

            private void HighlightLine(int lineNumber, Color backColor, Color foreColor)
            {
                if (lineNumber <= 0)
                    return;
                int zero = lineNumber - 1;
                int start = rtb.GetFirstCharIndexFromLine(zero);
                if (start < 0)
                    return;
                // Line length
                int length = (zero < rtb.Lines.Length) ? rtb.Lines[zero].Length : 0;

                // Reset all formatting first
                rtb.SelectAll();
                rtb.SelectionBackColor = Color.White;
                rtb.SelectionColor = Color.Black;

                // Set color the line
                rtb.Select(start, length);
                rtb.SelectionBackColor = backColor;
                rtb.SelectionColor = foreColor;

                // Scroll to view
                rtb.SelectionStart = start;
                rtb.ScrollToCaret();
            }

            private void BtnOpenFile_Click(object sender, EventArgs e)
            {
                try
                {
                    // open in default editor
                    var psi = new ProcessStartInfo
                    {
                        FileName = _report.Path,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Properties.Resources.CouldNotOpenFile} {ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
        }

        private async void btnScanAgain_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrEmpty(txtFolder.Text))
            {
                MessageBox.Show(Properties.Resources.NoFolderSelected, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            if (!Directory.Exists(txtFolder.Text))
            {
                MessageBox.Show(Properties.Resources.TheFolderPathIsIncorrect, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            await StartScanAsync(txtFolder.Text);
        }

        private void ApplyResourcesRecursive(Control root, ComponentResourceManager res)
        {
            res.ApplyResources(root, root.Name);
            foreach (Control c in root.Controls)
                ApplyResourcesRecursive(c, res);

            // Menü elemanlarýný da güncelle
            if (root is Form f && f.MainMenuStrip != null)
            {
                foreach (ToolStripItem it in f.MainMenuStrip.Items)
                    res.ApplyResources(it, it.Name);
                // Alt menüler:
                foreach (ToolStripMenuItem top in f.MainMenuStrip.Items)
                    foreach (ToolStripItem child in top.DropDownItems)
                        res.ApplyResources(child, child.Name);
            }
        }

        private void ChangeCulture(string cultureName)
        {
            try
            {
                // 1) Settings’e yaz
                Properties.Main.Default.Language = cultureName;
                Properties.Main.Default.Save();

                // 2) Kullanýcýya bilgi ver
                var result = MessageBox.Show(
                    Properties.Resources.RestartRequired,   // "Dil deðiþikliðinin uygulanmasý için uygulama yeniden baþlatýlacak. Devam edilsin mi?"
                    Properties.Resources.Info,              // "Bilgi"
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information
                );

                // 3) Onay verirse yeniden baþlat
                if (result == DialogResult.OK)
                {
                    Application.Restart();
                }
                // else hiçbir þey yapma (iptal ederse)

            }
            catch (Exception ex)
            {
                MessageBox.Show("Language change could not be saved: " + ex.Message,
                    Properties.Resources.Error,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



        private void mitrTR_Click(object sender, EventArgs e)
        {
            ChangeCulture("tr-TR");
        }

        private void mienUS_Click(object sender, EventArgs e)
        {
            ChangeCulture("en-US");
        }

        private void mifrCA_Click(object sender, EventArgs e)
        {
            ChangeCulture("fr-CA");
        }



    }
}
