using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bible2PPT.Sources;

namespace Bible2PPT;

partial class MainForm
{
    internal async Task CheckUiAsync()
    {
        async Task WaitForBooks(int expectedSelections = 1)
        {
            for (var attempt = 0; attempt < 300; attempt++)
            {
                if (bibleComboBox.Enabled && booksListView.Enabled && booksListView.Items.Count == 66 && biblesToBuild.Count == expectedSelections) return;
                await Task.Delay(50);
            }
            throw new InvalidOperationException($"UI Bible load did not finish: books={booksListView.Items.Count}, selected={biblesToBuild.Count}.");
        }
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        await WaitForBooks();
        // The upstream template-library page is unfinished and its button is
        // intentionally hidden. Test the navigation actually available to users.
        Require(!templatesNavButton.Visible, "Unfinished template-library navigation became visible.");
        var buttons = new[] { historyNavButton, settingsNavButton, buildNavButton };
        var pages = new[] { historyMultiPanelPage, settingsMultiPanelPage, buildMultiPanelPage };
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].PerformClick();
            Require(mainMultiPanel.SelectedPage == pages[index] && !buttons[index].Enabled,
                $"Navigation failed: button={buttons[index].Name}, selected={mainMultiPanel.SelectedPage.Name}, expected={pages[index].Name}.");
        }
        versesTextBox.Clear();
        booksSearchTextBox.Text = "요한일";
        AppendShortTitle();
        Require(versesTextBox.Text == "요일", "Book search/selection did not append IJohn abbreviation.");
        versesTextBox.AppendText(" 4:8");
        Require(versesTextBox.Text == "요일 4:8", "Verse input did not preserve the requested reference.");
        biblesRemoveIconButton.PerformClick();
        Require(biblesToBuild.Count == 0, "Bible remove button failed.");
        biblesAddIconButton.PerformClick();
        await WaitForBooks();
        settingsNavButton.PerformClick();
        Require(settingsMultiPanelPage.Controls.OfType<Button>().Any(button => button.Text == "성경 본문 가져오기"), "Bible import button is missing.");
        Require(numberOfVerseLinesPerSlideComboBox.SelectedIndex >= 0 && numberOfVerseLinesPerSlideComboBox.SelectedIndex <= 9, "Saved line limit is invalid.");
        buildNavButton.PerformClick();
        buildSplitChaptersIntoFilesCheckBox.Checked = !buildSplitChaptersIntoFilesCheckBox.Checked;
        Require(AppConfig.Context.SeperateByChapter == buildSplitChaptersIntoFilesCheckBox.Checked, "Split-chapter setting was not saved.");
        ExtractDefaultTemplate();
        Require(System.IO.File.Exists(AppConfig.TemplatePath), "Default PowerPoint template was not extracted.");
        using var archive = System.IO.Compression.ZipFile.OpenRead(AppConfig.TemplatePath);
        Require(archive.GetEntry("ppt/slides/slide1.xml") is not null, "Default template has no slide XML.");
        var importCombo = settingsMultiPanelPage.Controls.OfType<ComboBox>().Single(control => control.Name == "importTranslationComboBox");
        Require(importCombo.Items.Count == 3, "Settings must offer all three translations.");
        var sources = BibleSource.AvailableSources.Cast<LocalBible>().ToArray();
        var expected = 1;
        foreach (var source in sources)
        {
            var alreadyAdded = biblesToBuild.Any(bible => bible.SourceId == source.Id);
            sourceComboBox.SelectedItem = source;
            if (!alreadyAdded) expected++;
            await WaitForBooks(expected);
            Require(((Bible2PPT.Bibles.Bible)bibleComboBox.SelectedItem).Name == source.TranslationName,
                $"Selected translation name is incorrect: {source.TranslationName}.");
            importCombo.SelectedItem = source;
            Require(ReferenceEquals(importCombo.SelectedItem, source), "Import translation selector failed.");
        }
        Require(biblesToBuild.Count == 3 && biblesToBuild.Select(bible => bible.Id).Distinct().Count() == 3,
            "Three translations did not remain separate in the presentation selection.");
        sourceComboBox.SelectedItem = sources[0];
        await WaitForBooks(3);
    }
}
