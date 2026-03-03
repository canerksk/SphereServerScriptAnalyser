using FastColoredTextBoxNS;
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

        public enum RuleType
        {
            CheckEOF,
            CheckIfElse,
            CheckWhile,
            CheckFor,
            CheckSwitch,
            CheckDefHeaderFormat,
            CheckDuplicateDef,
            CheckLeadingWhitespace,
            CheckSpaceAfterBracket,
            CheckMissingSpace,
            CheckMultipleSpaces,
            CheckSpaceBeforeCloseBracket,
            CheckInvalidNameToken,
            CheckIfMissingSpace
        }

        private readonly Dictionary<RuleType, string> RuleLabels = new Dictionary<RuleType, string>
        {
            { RuleType.CheckEOF, "Check EOF" },
            { RuleType.CheckIfElse, "Check IF/ELSE/ENDIF" },
            { RuleType.CheckWhile, "Check WHILE/ENDWHILE" },
            { RuleType.CheckFor, "Check FOR/ENDFOR" },
            { RuleType.CheckSwitch, "Check SWITCH/ENDSWITCH" },
            { RuleType.CheckDefHeaderFormat, "Check Definition Header Format" },
            { RuleType.CheckDuplicateDef, "Check Duplicate Definition" },
            { RuleType.CheckLeadingWhitespace, "Check Leading whitespace" },
            { RuleType.CheckSpaceAfterBracket, "Check Space After Bracket" },
            { RuleType.CheckMissingSpace, "Check Missing Space Between TYPE and NAME" },
            { RuleType.CheckMultipleSpaces, "Check Multiple Spaces Between TYPE and NAME" },
            { RuleType.CheckSpaceBeforeCloseBracket, "Check Space Before Closing Bracket" },
            { RuleType.CheckInvalidNameToken, "Check Invalid Name Token" },
            { RuleType.CheckIfMissingSpace, "Check IF Statement Missing Space" }
        };


        public Form1()
        {
            InitializeComponent();

            clbRules.Items.AddRange(RuleLabels.Values.ToArray());
            for (int i = 0; i < clbRules.Items.Count; i++)
                clbRules.SetItemChecked(i, true);
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
        private List<RuleType> GetSelectedRules()
        {
            var selected = new List<RuleType>();
            foreach (string label in clbRules.CheckedItems)
            {
                var ruleType = RuleLabels.FirstOrDefault(x => x.Value == label).Key;
                selected.Add(ruleType);
            }
            return selected;
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
                var selectedRules = GetSelectedRules();
                var result = await Task.Run(() => RunScan(folder, selectedRules));

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

                lblStatus.Text = $"{Properties.Resources.AnalysisCompleted}. {result.Count} {Properties.Resources.FilesWereExamined}, {string.Format(Properties.Resources.ProblemsWereFoundInFiles0, problemFiles.Count)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.ScanningError} {ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = $"{Properties.Resources.AnErrorOccurred}";
            }
        }

        private List<FileReport> RunScan(string root, List<RuleType> activeRules)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".scp" };
            var allFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f))).ToList();

            var results = new List<FileReport>(allFiles.Count);

            // regexes (Developed in Sphere language)
            var commentRegex = new Regex(@"^\s*(;|//)", RegexOptions.Compiled);
            var ifRegex = new Regex(@"^\s*if\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var elseifRegex = new Regex(@"^\s*(elseif|elif)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var elseRegex = new Regex(@"^\s*else\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endifRegex = new Regex(@"^\s*endif\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var whileRegex = new Regex(@"^\s*while\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endwhileRegex = new Regex(@"^\s*endwhile\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var forRegex = new Regex(@"^\s*for\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endforRegex = new Regex(@"^\s*endfor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            var headerCandidateRegex = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);
            var validDefRegex = new Regex(
                @"\[(?<type>FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE) (?<name>[A-Za-z0-9_.]+)\]",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            );
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
                    var stackIf = new Stack<int>();
                    var stackWhile = new Stack<int>();
                    var stackFor = new Stack<int>();
                    var stackSwitch = new Stack<int>();
                    bool inCommentBlock = false; 


                    for (int i = 0; i < lines.Length; i++)
                    {
                        var raw = lines[i];
                        var trimmed = raw.TrimStart();

                        // [COMMENT] bloğu başlangıcı
                        if (trimmed.StartsWith("[COMMENT", StringComparison.OrdinalIgnoreCase))
                        {
                            inCommentBlock = true;
                            continue;
                        }
                        // Comment bloğu içindeysek ve [ ile başlayan başka bir satır görürsek, comment bloğu biter
                        if (inCommentBlock && trimmed.StartsWith("["))
                        {
                            inCommentBlock = false;
                            // Bu satırı normal işlemeye devam et, aşağıdaki kodlar çalışacak
                        }
                        // Hala comment bloğu içindeysek, bu satırı atla
                        if (inCommentBlock)
                            continue;

                        // Leading whitespace kontrolü
                        if (activeRules.Contains(RuleType.CheckLeadingWhitespace))
                        {
                            if (trimmed.StartsWith("[") && !raw.StartsWith("["))
                            {
                                fr.Issues.Add(new Issue
                                {
                                    Line = i + 1,
                                    Type = "LeadingWhitespaceBeforeBracket",
                                    Message = "Definition satır başında başlamalı. '[' öncesinde boşluk veya tab olmamalı."
                                });
                            }
                        }

                        if (string.IsNullOrWhiteSpace(raw))
                            continue;

                        var line = raw.TrimStart();

                        if (line.StartsWith(";") || line.StartsWith("//"))
                            continue;

                        // Definition header format
                        if (activeRules.Contains(RuleType.CheckDefHeaderFormat))
                        {
                            var candidates = headerCandidateRegex.Matches(line);
                            foreach (Match cand in candidates)
                            {
                                var token = cand.Value;
                                if (token.Equals("[eof]", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var typeHead = Regex.Match(token, @"^\[\s*(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)", RegexOptions.IgnoreCase);
                                if (!typeHead.Success)
                                    continue;

                                var vm = validDefRegex.Match(token);
                                if (vm.Success)
                                    continue;

                                // Space after bracket
                                if (activeRules.Contains(RuleType.CheckSpaceAfterBracket) && HasSpaceRightAfterOpenBracket(token))
                                {
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "SpaceAfterOpeningBracket", Message = $"'{token}' : '[' sonrası boşluk olmamalı" });
                                    continue;
                                }

                                // Missing space
                                if (activeRules.Contains(RuleType.CheckMissingSpace) && MissingSpaceBetweenTypeAndName(token))
                                {
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "MissingSpaceBetweenTypeAndName", Message = $"'{token}' : TYPE ile NAME arasında tam 1 boşluk olmalı" });
                                    continue;
                                }

                                // Multiple spaces
                                if (activeRules.Contains(RuleType.CheckMultipleSpaces) && MultipleSpacesBetweenTypeAndName(token))
                                {
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "MultipleSpacesBetweenTypeAndName", Message = $"'{token}' : TYPE ile NAME arasında 1'den fazla boşluk var" });
                                    continue;
                                }

                                // Space before closing bracket
                                if (activeRules.Contains(RuleType.CheckSpaceBeforeCloseBracket) && HasSpaceBeforeClosingBracket(token))
                                {
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "SpaceBeforeClosingBracket", Message = $"'{token}' : ']' öncesinde boşluk olmamalı" });
                                    continue;
                                }

                                // Invalid name token
                                if (activeRules.Contains(RuleType.CheckInvalidNameToken) && InvalidNameToken(token))
                                {
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "InvalidDefinitionName", Message = $"'{token}' : NAME yalnızca A-Z, 0-9 veya '_' içermeli" });
                                    continue;
                                }

                                fr.Issues.Add(new Issue { Line = i + 1, Type = "InvalidDefinitionHeader", Message = $"'{token}' : Geçersiz definition biçimi" });
                            }
                        }

                        // Duplicate definition
                        if (activeRules.Contains(RuleType.CheckDuplicateDef))
                        {
                            var valids = defHeaderInlineRegex.Matches(line);
                            foreach (Match dm in valids)
                            {
                                var type = dm.Groups["type"].Value;
                                var name = dm.Groups["name"].Value;
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
                        }

                        var m = Regex.Match(line, @"^(?<kw>[A-Za-z]+)\b");
                        if (!m.Success)
                            continue;

                        var kw = m.Groups["kw"].Value.ToLowerInvariant();

                        // IF/ELSE/ENDIF
                        if (activeRules.Contains(RuleType.CheckIfElse))
                        {
                            if (kw == "if")
                            {
                                // Gap control after IF
                                if (activeRules.Contains(RuleType.CheckIfMissingSpace))
                                {
                                    var afterKw = line.Substring(m.Length);
                                    if (afterKw.Length > 0 && !char.IsWhiteSpace(afterKw[0]))
                                    {
                                        string where = afterKw.StartsWith("(") ? "before '('"
                                          : afterKw.StartsWith("<") ? "before '<'"
                                          : "after 'if'";

                                        fr.Issues.Add(new Issue
                                        {
                                            Line = i + 1,
                                            Type = "IfMissingSpace",
                                            Message = $"'if' sonrası boşluk olmalı ({where}). Doğru kullanım: \"if (<expr>)\" veya \"if <expr>\""
                                        });
                                    }
                                }

                                stackIf.Push(i + 1);
                                continue;
                            }

                            if (kw == "elseif" || kw == "elif")
                            {
                                if (stackIf.Count == 0)
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "ElseIfWithoutIf", Message = "elseif/elif without matching if" });
                                continue;
                            }

                            if (kw == "else")
                            {
                                if (stackIf.Count == 0)
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "ElseWithoutIf", Message = "else without matching if" });
                                continue;
                            }

                            if (kw == "endif")
                            {
                                if (stackIf.Count == 0)
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndIf", Message = "endif without matching if" });
                                else
                                    stackIf.Pop();
                                continue;
                            }
                        }

                        // WHILE/ENDWHILE
                        if (activeRules.Contains(RuleType.CheckWhile))
                        {
                            if (kw == "while")
                            {
                                stackWhile.Push(i + 1);
                                continue;
                            }

                            if (kw == "endwhile")
                            {
                                if (stackWhile.Count == 0)
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndWhile", Message = "endwhile without matching while" });
                                else
                                    stackWhile.Pop();
                                continue;
                            }
                        }

                        // FOR/ENDFOR
                        if (activeRules.Contains(RuleType.CheckFor))
                        {
                            if (forOpenSet.Contains(kw))
                            {
                                stackFor.Push(i + 1);
                                continue;
                            }

                            if (kw == "endfor")
                            {
                                if (stackFor.Count == 0)
                                    fr.Issues.Add(new Issue { Line = i + 1, Type = "UnmatchedEndFor", Message = "endfor without matching for*" });
                                else
                                    stackFor.Pop();
                                continue;
                            }
                        }

                        // SWITCH/ENDSWITCH
                        if (activeRules.Contains(RuleType.CheckSwitch))
                        {
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
                        }
                    }

                    // Unclosed blocks (IF/ELSE/ENDIF)
                    if (activeRules.Contains(RuleType.CheckIfElse))
                    {
                        foreach (var ln in stackIf)
                            fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedIf", Message = "if var ama endif yok" });
                    }

                    // Unclosed blocks (WHILE)
                    if (activeRules.Contains(RuleType.CheckWhile))
                    {
                        foreach (var ln in stackWhile)
                            fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedWhile", Message = "While var ama endwhile yok" });
                    }

                    // Unclosed blocks (FOR)
                    if (activeRules.Contains(RuleType.CheckFor))
                    {
                        foreach (var ln in stackFor)
                            fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedFor", Message = "for* var ama endfor yok" });
                    }

                    // Unclosed blocks (SWITCH)
                    if (activeRules.Contains(RuleType.CheckSwitch))
                    {
                        foreach (var ln in stackSwitch)
                            fr.Issues.Add(new Issue { Line = ln, Type = "UnclosedSwitch", Message = "switch var ama endswitch yok" });
                    }

                    // EOF check
                    if (activeRules.Contains(RuleType.CheckEOF))
                    {
                        bool hasEof = false;
                        for (int j = lines.Length - 1; j >= 0; j--)
                        {
                            var s = lines[j].Trim();
                            if (s.Length == 0) continue;
                            if (s.StartsWith(";") || s.StartsWith("//")) continue;
                            if (s.Equals("[eof]", StringComparison.OrdinalIgnoreCase))
                                hasEof = true;
                            break;
                        }
                        if (!hasEof)
                        {
                            fr.Issues.Add(new Issue
                            {
                                Line = lines.Length,
                                Type = "NOEOF",
                                Message = $"{Properties.Resources.ThereIsNoEofAtTheEndOfTheFile}"
                            });
                        }
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

        bool HasSpaceRightAfterOpenBracket(string token) => token.StartsWith("[ ") || token.StartsWith("[\t");

        bool MissingSpaceBetweenTypeAndName(string token) =>
            Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)[A-Za-z0-9_.]+\]$", RegexOptions.IgnoreCase);

        bool MultipleSpacesBetweenTypeAndName(string token) =>
            Regex.IsMatch(token, @"^\[(FUNCTION|ITEMDEF|CHARDEF|DEFNAME|EVENTS|AREADEF|SPAWN|SPEECH|TEMPLATE)\s{2,}[A-Za-z0-9_.]+\]$", RegexOptions.IgnoreCase);

        bool HasSpaceBeforeClosingBracket(string token) =>
            Regex.IsMatch(token, @"\s\]$");

        bool InvalidNameToken(string token) =>
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
            private FastColoredTextBox fctb;
            private static Button btnOpenFile;
            public static string? _defaultEditorPath;
            private Button btnAddEof;

            public IssueViewerForm(FileReport report)
            {
                _report = report;
                InitUi();
                LoadFileAndIssues();

                this.Shown += (s, e) => {
                    if (lbIssues.Items.Count > 0 && lbIssues.SelectedIndex >= 0)
                        LbIssues_SelectedIndexChanged(lbIssues, EventArgs.Empty);
                };

                LoadEditorPreference();
                UpdateEofButtonState(); 
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

                btnAddEof = new Button
                {
                    Text = "Add [EOF]",
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                btnAddEof.Click += BtnAddEof_Click;

                fctb = new FastColoredTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    ShowLineNumbers = true,
                    WordWrap = false,
                    Font = new Font("Consolas", 10f)
                };

                btnOpenFile = new Button
                {
                    Text = Properties.Resources.OpenTheFile,
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                btnOpenFile.Click += BtnOpenFile_Click;

                Controls.Add(fctb);
                Controls.Add(lbIssues);
                Controls.Add(btnOpenFile);
                Controls.Add(btnAddEof);
            }

            private void LoadFileAndIssues()
            {
                string content;
                try { content = File.ReadAllText(_report.Path); }
                catch (Exception ex) { content = $"{Properties.Resources.TheFileCouldNotBeRead} {ex.Message}"; }

                fctb.Text = content;

                lbIssues.Items.Clear();
                foreach (var issue in _report.Issues.OrderBy(i => i.Line))
                    lbIssues.Items.Add($"L{issue.Line} – {issue.Type}");

                if (lbIssues.Items.Count > 0)
                {
                    lbIssues.SelectedIndex = 0;
                    LbIssues_SelectedIndexChanged(lbIssues, EventArgs.Empty);
                }

            }

            private void LbIssues_SelectedIndexChanged(object sender, EventArgs e)
            {
                if (lbIssues.SelectedIndex < 0)
                    return;
                var issue = _report.Issues.OrderBy(i => i.Line).ElementAt(lbIssues.SelectedIndex);

                HighlightLineFctb(issue.Line);

                int lineIdx = Math.Max(0, issue.Line - 1);
                fctb.Navigate(lineIdx);
                var lineText = lineIdx < fctb.LinesCount ? fctb.Lines[lineIdx] : string.Empty;
                fctb.Selection = new FastColoredTextBoxNS.Range(fctb, new Place(0, lineIdx), new Place(lineText.Length, lineIdx));
                fctb.DoSelectionVisible();
            }

            private readonly MarkerStyle _highlightStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(60, Color.Red)));

            private void HighlightLineFctb(int lineNumber)
            {
                if (lineNumber <= 0 || lineNumber > fctb.LinesCount)
                    return;

                int line = lineNumber - 1;
                fctb.Range.ClearStyle(_highlightStyle);
                string text = fctb.Lines[line];
                var range = new FastColoredTextBoxNS.Range(fctb, new Place(0, line), new Place(text.Length, line));
                range.SetStyle(_highlightStyle);
                fctb.Navigate(line);
                fctb.DoSelectionVisible();
            }

            private void HighlightLine(int lineNumber, Color backColor, Color foreColor)
            {
                if (lineNumber <= 0)
                    return;
                int zero = lineNumber - 1;
                int start = rtb.GetFirstCharIndexFromLine(zero);
                if (start < 0)
                    return;
                int length = (zero < rtb.Lines.Length) ? rtb.Lines[zero].Length : 0;

                rtb.SelectAll();
                rtb.SelectionBackColor = Color.White;
                rtb.SelectionColor = Color.Black;
                rtb.Select(start, length);
                rtb.SelectionBackColor = backColor;
                rtb.SelectionColor = foreColor;
                rtb.SelectionStart = start;
                rtb.ScrollToCaret();
            }

            private int GetSelectedIssueLineOrDefault()
            {
                if (_report?.Issues == null || _report.Issues.Count == 0)
                    return 1;

                if (lbIssues.SelectedIndex >= 0)
                    return _report.Issues.OrderBy(i => i.Line).ElementAt(lbIssues.SelectedIndex).Line;

                return _report.Issues.Min(i => i.Line);
            }

            private static string BuildEditorArgs(string editorExePath, string filePath, int line, int col = 1)
            {
                var exe = Path.GetFileName(editorExePath).ToLowerInvariant();

                if (exe is "code.exe" or "code-insiders.exe")
                    return $"--goto \"{filePath}:{line}:{col}\"";

                if (exe == "notepad++.exe")
                    return $"-n{line} -c{col} \"{filePath}\"";

                if (exe is "sublime_text.exe" or "subl.exe")
                    return $"\"{filePath}:{line}:{col}\"";

                if (exe == "devenv.exe")
                    return $"/Edit \"{filePath}\" /Command \"Edit.Goto {line}\"";

                return $"\"{filePath}\"";
            }

            //  check eof problem
            private bool HasNoEofIssue()
            {
                return _report.Issues.Any(i => i.Type == "NOEOF");
            }


            private void UpdateEofButtonState()
            {
                btnAddEof.Enabled = HasNoEofIssue();
            }

            private void BtnAddEof_Click(object sender, EventArgs e)
            {
                try
                {
                    //File.AppendAllText(_report.Path, Environment.NewLine + Environment.NewLine + "[EOF]" + Environment.NewLine);
                    File.AppendAllText(_report.Path, Environment.NewLine + Environment.NewLine + "[EOF]");
                    MessageBox.Show(Properties.Resources.AddEOFLineFile, Properties.Resources.Success, MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // remove NOEOF issue
                    _report.Issues.RemoveAll(i => i.Type == "NOEOF");

                    // reload
                    LoadFileAndIssues();
                    UpdateEofButtonState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Properties.Resources.FileUpdateFailed}:" + ex.Message, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void BtnOpenFile_Click(object sender, EventArgs e)
            {
                try
                {
                    int line = GetSelectedIssueLineOrDefault();
                    int col = 1;

                    if (!string.IsNullOrEmpty(_defaultEditorPath) && File.Exists(_defaultEditorPath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = _defaultEditorPath,
                            Arguments = BuildEditorArgs(_defaultEditorPath, _report.Path, line, col),
                            UseShellExecute = false
                        };
                        Process.Start(psi);
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _report.Path,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{Properties.Resources.CouldNotOpenFile} {ex.Message}",
                        Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            public void LoadEditorPreference()
            {
                _defaultEditorPath = string.IsNullOrWhiteSpace(Properties.Main.Default.DefaultEditorPath)
                    ? null
                    : Properties.Main.Default.DefaultEditorPath;
                UpdateOpenButtonText();
            }

            public static void SaveEditorPreference(string? path)
            {
                _defaultEditorPath = path;
                Properties.Main.Default.DefaultEditorPath = path ?? string.Empty;
                Properties.Main.Default.Save();
                UpdateOpenButtonText();
            }

            private static void UpdateOpenButtonText()
            {
                string suffix = "";

                if (!string.IsNullOrEmpty(_defaultEditorPath) && File.Exists(_defaultEditorPath))
                {
                    var name = Path.GetFileNameWithoutExtension(_defaultEditorPath);
                    suffix = $" ({name})";
                }

                if (btnOpenFile != null && !btnOpenFile.IsDisposed)
                {
                    btnOpenFile.Text = (Properties.Resources.OpenTheFile ?? "Open the file") + suffix;
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

            if (root is Form f && f.MainMenuStrip != null)
            {
                foreach (ToolStripItem it in f.MainMenuStrip.Items)
                    res.ApplyResources(it, it.Name);
                foreach (ToolStripMenuItem top in f.MainMenuStrip.Items)
                    foreach (ToolStripItem child in top.DropDownItems)
                        res.ApplyResources(child, child.Name);
            }
        }

        private void ChangeCulture(string cultureName)
        {
            try
            {
                Properties.Main.Default.Language = cultureName;
                Properties.Main.Default.Save();

                var result = MessageBox.Show(
                    Properties.Resources.RestartRequired,
                    Properties.Resources.Info,
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.OK)
                {
                    Application.Restart();
                }
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

        private void setDefaultEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = Properties.Resources.SelectTheEditorApp,
                Filter = $"{Properties.Resources.Applications} (*.exe)|*.exe|{Properties.Resources.All} (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
                IssueViewerForm.SaveEditorPreference(ofd.FileName);
        }

        private void resetDefaultEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Main.Default.DefaultEditorPath = string.Empty;
            Properties.Main.Default.Save();
        }
    }
}