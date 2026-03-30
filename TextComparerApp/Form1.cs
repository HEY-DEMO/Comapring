using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;

namespace TextComparerApp
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x000B;

        private TableLayoutPanel mainLayout = null!;
        private SplitContainer splitContainerInputs = null!;
        private TextBox txtOriginal = null!;
        private TextBox txtModified = null!;
        private RichTextBox rtbResult = null!;
        private FlowLayoutPanel actionPanel = null!;
        private Button btnCompare = null!;
        private Button btnClear = null!;
        private CheckBox chkIgnoreCase = null!;
        private Label lblStatus = null!;
        private System.Windows.Forms.Timer compareTimer = null!;

        private int pendingComparisons = 0;

        public Form1()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "Text Comparer App";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(600, 400);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40f)); // Inputs
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // Button
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60f)); // Output
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f)); // Status text

            splitContainerInputs = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = this.Width / 2
            };

            txtOriginal = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f)
            };
            txtOriginal.TextChanged += (s, e) => RestartTimer();
            var grpOriginal = new GroupBox { Dock = DockStyle.Fill, Text = "Original Text" };
            grpOriginal.Controls.Add(txtOriginal);

            txtModified = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f)
            };
            txtModified.TextChanged += (s, e) => RestartTimer();
            var grpModified = new GroupBox { Dock = DockStyle.Fill, Text = "Modified Text" };
            grpModified.Controls.Add(txtModified);

            splitContainerInputs.Panel1.Controls.Add(grpOriginal);
            splitContainerInputs.Panel2.Controls.Add(grpModified);

            btnCompare = new Button
            {
                Text = "Force Compare",
                Height = 35,
                Width = 130,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            btnCompare.Click += BtnCompare_Click;

            btnClear = new Button
            {
                Text = "Clear All",
                Height = 35,
                Width = 100,
                Font = new Font("Segoe UI", 10f)
            };
            btnClear.Click += BtnClear_Click;

            chkIgnoreCase = new CheckBox
            {
                Text = "Ignore Case",
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Margin = new Padding(15, 8, 0, 0)
            };
            chkIgnoreCase.CheckedChanged += (s, e) => RestartTimer();

            actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0)
            };
            actionPanel.Controls.Add(btnCompare);
            actionPanel.Controls.Add(btnClear);
            actionPanel.Controls.Add(chkIgnoreCase);

            compareTimer = new System.Windows.Forms.Timer { Interval = 500 };
            compareTimer.Tick += CompareTimer_Tick;

            rtbResult = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 11f),
                BackColor = Color.White
            };

            var legendPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            var lblDeletedLegend = new Label
            {
                Text = "Grey Strikethrough = Deleted (Missing in Modified Text)",
                Font = new Font("Segoe UI", 9f, FontStyle.Strikeout),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(5)
            };

            var lblInsertedLegend = new Label
            {
                Text = "Red Bold = Inserted (Missing in Original Text)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                BackColor = Color.FromArgb(255, 230, 230),
                AutoSize = true,
                Margin = new Padding(5)
            };

            legendPanel.Controls.Add(lblDeletedLegend);
            legendPanel.Controls.Add(lblInsertedLegend);

            var grpResult = new GroupBox { Dock = DockStyle.Fill, Text = "Comparison Result" };
            // Adding DocStyle controls requires understanding Z-Order. Add Fill last or use BringToFront
            grpResult.Controls.Add(rtbResult);
            grpResult.Controls.Add(legendPanel);
            rtbResult.BringToFront(); // Ensures Fill does not overlap Top

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ready",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f)
            };

            mainLayout.Controls.Add(splitContainerInputs, 0, 0);
            mainLayout.Controls.Add(actionPanel, 0, 1);
            mainLayout.Controls.Add(grpResult, 0, 2);
            mainLayout.Controls.Add(lblStatus, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Replace("\r", "").Replace("\n", "");
        }

        private string[] Tokenize(string text)
        {
            var tokens = Regex.Split(text, @"(\s+|[.,!?;:])");
            return tokens.Where(t => !string.IsNullOrEmpty(t)).ToArray();
        }

        private void RestartTimer()
        {
            compareTimer.Stop();
            compareTimer.Start();
            lblStatus.Text = "Waiting to compare...";
        }

        private void CompareTimer_Tick(object? sender, EventArgs e)
        {
            compareTimer.Stop();
            BtnCompare_Click(this, EventArgs.Empty);
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            txtOriginal.Clear();
            txtModified.Clear();
            rtbResult.Clear();
            lblStatus.Text = "Ready";
            compareTimer.Stop();
        }

        private async void BtnCompare_Click(object? sender, EventArgs e)
        {
            string original = Sanitize(txtOriginal.Text);
            string modified = Sanitize(txtModified.Text);
            bool ignoreCase = chkIgnoreCase.Checked;

            btnCompare.Enabled = false;
            lblStatus.Text = "Processing...";

            System.Threading.Interlocked.Increment(ref pendingComparisons);

            try
            {
                var diffResultBlock = await Task.Run(() => ComputeDiff(original, modified, ignoreCase));
                
                // Only render if this is the latest requested comparison
                if (pendingComparisons == 1)
                {
                    RenderDiff(diffResultBlock);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error";
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref pendingComparisons);
                btnCompare.Enabled = true;
            }
        }

        private DiffResult ComputeDiff(string original, string modified, bool ignoreCase)
        {
            var differ = new Differ();
            var chunker = new Func<string, string[]>(Tokenize);
            var diff = differ.CreateCustomDiffs(original, modified, false, ignoreCase, chunker);
            return diff;
        }

        private void RenderDiff(DiffResult diffResult)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RenderDiff(diffResult)));
                return;
            }

            rtbResult.SuspendLayout();
            rtbResult.Clear();

            int bpos = 0;
            var diffBlocks = diffResult.DiffBlocks;
            var oldPieces = diffResult.PiecesOld;
            var newPieces = diffResult.PiecesNew;

            var sbEqual = new StringBuilder();

            int totalInsertions = 0;
            int totalDeletions = 0;

            foreach (var block in diffBlocks)
            {
                // Gather equal tokens before this block
                sbEqual.Clear();
                while (bpos < block.InsertStartB)
                {
                    sbEqual.Append(newPieces[bpos]);
                    bpos++;
                }
                if (sbEqual.Length > 0)
                {
                    AppendTextWithStyle(sbEqual.ToString(), "Equal");
                }

                int bInsertCount = block.InsertCountB;
                int aDeleteCount = block.DeleteCountA;

                totalDeletions += aDeleteCount;
                totalInsertions += bInsertCount;

                string delText = "";
                if (aDeleteCount > 0)
                {
                    var deleteSb = new StringBuilder();
                    for (int i = 0; i < aDeleteCount; i++)
                        deleteSb.Append(oldPieces[block.DeleteStartA + i]);
                    delText = deleteSb.ToString();
                }

                string insText = "";
                if (bInsertCount > 0)
                {
                    var insertSb = new StringBuilder();
                    for (int i = 0; i < bInsertCount; i++)
                        insertSb.Append(newPieces[block.InsertStartB + i]);
                    insText = insertSb.ToString();
                }

                if (aDeleteCount > 0 && bInsertCount > 0)
                {
                    // Evaluate Nested Character-Level Diff
                    var charDiffer = new Differ();
                    var charDiffRes = charDiffer.CreateCustomDiffs(delText, insText, false, false, x => x.Select(c => c.ToString()).ToArray());

                    int changedChars = charDiffRes.DiffBlocks.Sum(b => Math.Max(b.DeleteCountA, b.InsertCountB));
                    int maxLen = Math.Max(delText.Length, insText.Length);

                    // If majority matches (changed chars <= half length), do character level
                    if (maxLen > 0 && changedChars <= maxLen / 2)
                    {
                        int cPos = 0;
                        foreach (var cBlock in charDiffRes.DiffBlocks)
                        {
                            while (cPos < cBlock.InsertStartB)
                            {
                                AppendTextWithStyle(charDiffRes.PiecesNew[cPos], "Equal");
                                cPos++;
                            }
                            for (int i = 0; i < cBlock.DeleteCountA; i++)
                                AppendTextWithStyle(charDiffRes.PiecesOld[cBlock.DeleteStartA + i], "Delete");
                            
                            for (int i = 0; i < cBlock.InsertCountB; i++)
                                AppendTextWithStyle(charDiffRes.PiecesNew[cBlock.InsertStartB + i], "Insert");
                            
                            cPos = cBlock.InsertStartB + cBlock.InsertCountB;
                        }
                        while (cPos < charDiffRes.PiecesNew.Count)
                        {
                            AppendTextWithStyle(charDiffRes.PiecesNew[cPos], "Equal");
                            cPos++;
                        }
                    }
                    else
                    {
                        // Fallback to word-level diff
                        AppendTextWithStyle(delText, "Delete");
                        AppendTextWithStyle(insText, "Insert");
                    }
                }
                else
                {
                    if (aDeleteCount > 0) AppendTextWithStyle(delText, "Delete");
                    if (bInsertCount > 0) AppendTextWithStyle(insText, "Insert");
                }

                bpos = block.InsertStartB + block.InsertCountB;
            }

            // Append any remaining equal parts at the end
            sbEqual.Clear();
            while (bpos < newPieces.Count)
            {
                sbEqual.Append(newPieces[bpos]);
                bpos++;
            }
            if (sbEqual.Length > 0)
            {
                AppendTextWithStyle(sbEqual.ToString(), "Equal");
            }

            rtbResult.ResumeLayout();
            rtbResult.Invalidate();

            lblStatus.Text = $"Done. Summary: {totalInsertions} Insertions | {totalDeletions} Deletions";
        }

        private void AppendTextWithStyle(string text, string type)
        {
            if (string.IsNullOrEmpty(text))
                return;

            rtbResult.Select(rtbResult.TextLength, 0);

            if (type == "Equal")
            {
                rtbResult.SelectionFont = new Font("Consolas", 11f, FontStyle.Regular);
                rtbResult.SelectionColor = Color.Black;
                rtbResult.SelectionBackColor = Color.White;
            }
            else if (type == "Delete")
            {
                rtbResult.SelectionFont = new Font("Consolas", 11f, FontStyle.Strikeout);
                rtbResult.SelectionColor = Color.Gray;
                rtbResult.SelectionBackColor = Color.White;
            }
            else if (type == "Insert")
            {
                rtbResult.SelectionFont = new Font("Consolas", 11f, FontStyle.Bold);
                rtbResult.SelectionColor = Color.DarkRed;
                rtbResult.SelectionBackColor = Color.FromArgb(255, 230, 230);
            }

            rtbResult.SelectedText = text;
        }
    }
}
