using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Sources;

namespace Bible2PPT
{
    partial class MainForm
    {
        private void InitializeSettingsComponent()
        {
            var importButton = new Button
            {
                Text = "성경 본문 가져오기",
                Location = new Point(150, 24),
                Size = new Size(160, 35),
                UseVisualStyleBackColor = true,
            };
            importButton.Click += async (_, _) =>
            {
                if (workCts.Count > 0)
                {
                    MessageBox.Show(this, "PPT 제작이 끝난 뒤 본문을 교체하세요.", Text);
                    return;
                }
                var source = sourceComboBox.SelectedItem as LocalBible;
                if (source != null && await ImportLocalBibleAsync(source))
                {
                    SourceComboBox_SelectedValueChanged(sourceComboBox, EventArgs.Empty);
                }
            };
            settingsMultiPanelPage.Controls.Add(importButton);
        }

        private async Task<bool> ImportLocalBibleAsync(LocalBible source)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "사용할 개역개정 본문 파일 선택",
                Filter = "성경 본문 (*.json;*.txt;*.tsv)|*.json;*.txt;*.tsv|모든 파일 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            try
            {
                await LocalBibleImporter.ImportAsync(dialog.FileName, source.FilePath);
                source.Reload();
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, "본문 가져오기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppConfig.ContactUrl,
                UseShellExecute = true,
            });
        }

        private async void CleanCacheButton_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show("진행 중인 작업을 취소하고 프로그램을 다시 시작할까요?", Text, MessageBoxButtons.YesNo))
            {
                await _bibleService.ClearCachesAsync();
                // TODO: clear job history / alter online id as key
                Application.Restart();
            }
        }
    }
}
